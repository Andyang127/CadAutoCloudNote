using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using CadAutoCloudNote.Core.Helpers;
using CadAutoCloudNote.Core.Models;
using CadAutoCloudNote.Core.Protocols;

namespace CadAutoCloudNote.Core.Reactors
{
    /// <summary>
    /// 图元复制、克隆及炸开探测与分流处理引擎
    /// </summary>
    public static class CloneDetection
    {
        public static bool ProcessAppendedTasks(Database db, Transaction tr, List<PendingTask> appendedTasks)
        {
            if (db == null || tr == null || appendedTasks == null || appendedTasks.Count == 0)
                return false;

            Dictionary<string, List<ObjectId>> noteCloneMap = new Dictionary<string, List<ObjectId>>();

            foreach (var task in appendedTasks)
            {
                if (task.TargetId.IsNull || task.TargetId.IsErased)
                    continue;

                try
                {
                    DBObject obj = tr.GetObject(task.TargetId, OpenMode.ForRead);
                    string noteId = XDataHelper.Instance.GetNoteId(obj, AppConstants.XDATA_APP_NAME);
                    if (!string.IsNullOrEmpty(noteId))
                    {
                        if (!noteCloneMap.ContainsKey(noteId))
                            noteCloneMap[noteId] = new List<ObjectId>();
                        noteCloneMap[noteId].Add(task.TargetId);
                    }
                }
                catch
                {
                    // 忽略无效对象
                }
            }

            if (noteCloneMap.Count == 0)
                return false;

            bool anyModified = false;
            foreach (var kvp in noteCloneMap)
            {
                string origNoteId = kvp.Key;
                List<ObjectId> clonedIds = kvp.Value;

                NoteRecord origNote = XRecordHelper.ReadNote(db, tr, origNoteId);
                if (origNote == null)
                {
                    // 原批注已不存在，清除克隆图元上的孤立标记
                    ClearXData(tr, clonedIds);
                    anyModified = true;
                    continue;
                }

                // 判断是否为完整成组克隆（例如至少有2个关联实体：云线+文字）
                if (clonedIds.Count >= 2)
                {
                    // 派生生成新批注副本
                    string newNoteId = Guid.NewGuid().ToString("N");
                    NoteRecord newNote = new NoteRecord
                    {
                        NoteId = newNoteId,
                        SequenceNumber = origNote.SequenceNumber + 1000,
                        Title = origNote.Title + " (副本)",
                        Content = origNote.Content,
                        State = NoteLifecycleState.Pending,
                        Discipline = origNote.Discipline,
                        Priority = origNote.Priority,
                        CreatedBy = Environment.UserName,
                        CreatedTime = DateTime.Now,
                        ModifiedTime = DateTime.Now,
                        Cloud = new CloudEntity
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            ShapeType = origNote.Cloud != null ? origNote.Cloud.ShapeType : CloudShapeType.Rectangle,
                            ArcLength = origNote.Cloud != null ? origNote.Cloud.ArcLength : 5.0
                        },
                        Text = new TextEntity
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            FormattedContent = origNote.Text != null ? origNote.Text.FormattedContent : origNote.Content
                        }
                    };

                    foreach (ObjectId id in clonedIds)
                    {
                        DBObject obj = tr.GetObject(id, OpenMode.ForWrite);
                        XDataHelper.Instance.SetNoteId(obj, AppConstants.XDATA_APP_NAME, newNoteId);
                        string hStr = id.Handle.ToString();
                        if (obj is Polyline poly)
                        {
                            if (poly.Closed)
                            {
                                newNote.Cloud.EntityHandles.Add(hStr);
                            }
                            else
                            {
                                if (!newNote.Text.LeaderHandles.Contains(hStr))
                                    newNote.Text.LeaderHandles.Add(hStr);
                            }
                        }
                        else if (obj is MText)
                        {
                            newNote.Text.MTextHandle = hStr;
                        }
                        else if (obj is Leader)
                        {
                            if (!newNote.Text.LeaderHandles.Contains(hStr))
                                newNote.Text.LeaderHandles.Add(hStr);
                        }
                    }

                    XRecordHelper.SaveNoteSlices(db, tr, newNote);
                    anyModified = true;
                }
                else
                {
                    // 仅单个图元被零散复制，清除其扩展数据，避免数据污染冲突
                    ClearXData(tr, clonedIds);
                    anyModified = true;
                }
            }

            return anyModified;
        }

        private static void ClearXData(Transaction tr, List<ObjectId> ids)
        {
            foreach (ObjectId id in ids)
            {
                if (!id.IsNull && !id.IsErased)
                {
                    try
                    {
                        DBObject obj = tr.GetObject(id, OpenMode.ForWrite);
                        XDataHelper.Instance.RemoveNoteId(obj, AppConstants.XDATA_APP_NAME);
                    }
                    catch { }
                }
            }
        }
    }
}
