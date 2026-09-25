using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using CadAutoCloudNote.Core.Helpers;
using CadAutoCloudNote.Core.Models;
using CadAutoCloudNote.Core.Protocols;
using CadAutoCloudNote.Core.Reactors;

namespace CadAutoCloudNote.Core.Services
{
    /// <summary>
    /// 批注协同流转与多轮审查回复服务
    /// </summary>
    public static class ReplyService
    {
        public static bool AddReply(
            Database db,
            string noteId,
            string content,
            string author,
            NoteLifecycleState newState,
            out string errorMessage)
        {
            errorMessage = string.Empty;
            if (db == null || string.IsNullOrEmpty(noteId))
            {
                errorMessage = "参数无效";
                return false;
            }

            var docState = ReactorManager.Instance.GetState(db);
            using (docState?.EnterInternalScope())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    NoteRecord note = XRecordHelper.ReadNote(db, tr, noteId);
                    if (note == null)
                    {
                        errorMessage = "未找到指定批注";
                        return false;
                    }

                    if (!ValidationService.ValidateStateTransition(note.State, newState, out errorMessage))
                    {
                        return false;
                    }

                    DateTime now = DateTime.Now;
                    string replyAuthor = string.IsNullOrEmpty(author) ? Environment.UserName : author;

                    NoteReply reply = new NoteReply
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Author = replyAuthor,
                        Timestamp = now,
                        Content = content,
                        TransitionState = newState
                    };

                    note.Replies.Add(reply);
                    NoteLifecycleState oldState = note.State;
                    note.State = newState;
                    note.ModifiedTime = now;

                    // 1. 同步图元颜色与文字内容
                    short newColor = NoteLifecycleStateExtensions.GetColorIndex(newState);
                    UpdateNoteEntitiesAppearance(db, tr, note, newColor);

                    // 2. 写入审计日志
                    string prevHash = note.AuditLogs.Count > 0 ? note.AuditLogs[note.AuditLogs.Count - 1].CurrentHash : string.Empty;
                    string diff = string.Format("状态流转: {0} -> {1} (办理人: {2}); 追加回复: {3}", 
                        oldState.ToDisplayName(), newState.ToDisplayName(), replyAuthor, content);
                    AuditEntry audit = AuditService.CreateEntry(prevHash, "REPLY", diff, replyAuthor);
                    note.AuditLogs.Add(audit);

                    // 3. 持久化
                    XRecordHelper.SaveNoteSlices(db, tr, note);

                    tr.Commit();
                    return true;
                }
            }
        }

        private static void UpdateNoteEntitiesAppearance(Database db, Transaction tr, NoteRecord note, short colorIndex)
        {
            // 更新云线颜色
            if (note.Cloud?.EntityHandles != null)
            {
                foreach (string hStr in note.Cloud.EntityHandles)
                {
                    TryUpdateColor(db, tr, hStr, colorIndex);
                }
            }

            // 更新引线与文字颜色
            if (note.Text != null)
            {
                if (note.Text.LeaderHandles != null && note.Text.LeaderHandles.Count > 0)
                {
                    foreach (string lh in note.Text.LeaderHandles)
                    {
                        TryUpdateColor(db, tr, lh, colorIndex);
                    }
                }
                else
                {
                    TryUpdateColor(db, tr, note.Text.LeaderHandle, colorIndex);
                }

                if (!string.IsNullOrEmpty(note.Text.FrameHandle))
                {
                    TryUpdateColor(db, tr, note.Text.FrameHandle, colorIndex);
                }

                if (!string.IsNullOrEmpty(note.Text.MTextHandle))
                {
                    try
                    {
                        if (XDataHelper.SafeGetObjectId(db, note.Text.MTextHandle, out ObjectId id) && !id.IsNull && !id.IsErased)
                        {
                            MText mtext = (MText)tr.GetObject(id, OpenMode.ForWrite);
                            mtext.ColorIndex = colorIndex;
                            string stateName = NoteLifecycleStateExtensions.ToDisplayName(note.State);
                            string displayTitle = !string.IsNullOrEmpty(note.Title) ? string.Format("[{0}] {1}", stateName, note.Title) : stateName;
                            bool isSimple = !string.IsNullOrEmpty(note.Text.FrameHandle) && string.IsNullOrEmpty(note.Content);
                            int seqForText = isSimple ? 0 : note.SequenceNumber;
                            mtext.Contents = NoteService.FormatNoteContents(seqForText, displayTitle, note.Content, note.ModifiedTime, isSimple);

                            // 状态流转后动态刷新文字位置与托线长度，杜绝文字重叠小三角或托线过短
                            Point3d textPt = note.Text.InsertionPoint;
                            double textHeight = mtext.TextHeight > 0 ? mtext.TextHeight : 3.5;
                            double triH = Math.Max(2.0, textHeight) * 1.8;
                            double triW = triH / 0.866025;
                            double textGap = textHeight * 0.4;
                            bool isLeft = mtext.Location.X < textPt.X;
                            double newWidth = NoteService.CalculateNoteContentWidth(displayTitle, textHeight, seqForText, isSimple, note.Content);
                            bool hasLine2 = isSimple || !string.IsNullOrEmpty(note.Content);

                            if (hasLine2)
                            {
                                mtext.Attachment = AttachmentPoint.MiddleLeft;
                                mtext.Location = isLeft
                                    ? new Point3d(textPt.X - (triW / 2.0) - textGap - newWidth, textPt.Y, 0)
                                    : new Point3d(textPt.X + (triW / 2.0) + textGap, textPt.Y, 0);
                            }
                            else
                            {
                                mtext.Attachment = AttachmentPoint.BottomLeft;
                                mtext.Location = isLeft
                                    ? new Point3d(textPt.X - (triW / 2.0) - textGap - newWidth, textPt.Y + textHeight * 0.18, 0)
                                    : new Point3d(textPt.X + (triW / 2.0) + textGap, textPt.Y + textHeight * 0.18, 0);
                            }
                            mtext.RecordGraphicsModified(true);

                            // 同步刷新主引线水平托线长度
                            string primaryLeaderHandle = (note.Text.LeaderHandles != null && note.Text.LeaderHandles.Count > 0)
                                ? note.Text.LeaderHandles[0]
                                : note.Text.LeaderHandle;
                            if (!string.IsNullOrEmpty(primaryLeaderHandle) && XDataHelper.SafeGetObjectId(db, primaryLeaderHandle, out ObjectId plId) && !plId.IsNull && !plId.IsErased)
                            {
                                Polyline leader = tr.GetObject(plId, OpenMode.ForWrite) as Polyline;
                                if (leader != null && leader.NumberOfVertices >= 3)
                                {
                                    double landingLength = NoteService.CalculateSimpleNoteLandingLength(displayTitle, textHeight, seqForText, isSimple, note.Content);
                                    Point3d newLandingEnd = isLeft
                                        ? new Point3d(textPt.X - landingLength, textPt.Y, 0)
                                        : new Point3d(textPt.X + landingLength, textPt.Y, 0);
                                    leader.SetPointAt(leader.NumberOfVertices - 1, new Point2d(newLandingEnd.X, newLandingEnd.Y));
                                    leader.RecordGraphicsModified(true);
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
        }

        private static void TryUpdateColor(Database db, Transaction tr, string handleStr, short colorIndex)
        {
            if (string.IsNullOrEmpty(handleStr)) return;
            try
            {
                if (XDataHelper.SafeGetObjectId(db, handleStr, out ObjectId id) && !id.IsNull && !id.IsErased)
                {
                    Entity ent = (Entity)tr.GetObject(id, OpenMode.ForWrite);
                    ent.ColorIndex = colorIndex;
                }
            }
            catch { }
        }
    }
}
