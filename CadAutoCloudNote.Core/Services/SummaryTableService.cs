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
    /// 图纸内批注汇总表生成服务（绘制原生 CAD Table 对象）
    /// </summary>
    public static class SummaryTableService
    {
        public static ObjectId CreateSummaryTable(Database db, Point3d insertPoint, List<NoteRecord> notes)
        {
            if (db == null || notes == null || notes.Count == 0)
                return ObjectId.Null;

            var docState = ReactorManager.Instance.GetState(db);
            using (docState?.EnterInternalScope())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                    double scale = ScaleHelper.GetDrawingScale(db);
                    double rowHeight = 8.0 * scale;
                    double textHeight = 3.5 * scale;

                    Table table = new Table();
                    bool isTableAdded = false;
                    try
                    {
                        table.Position = insertPoint;
                        table.TableStyle = db.Tablestyle;

                        int numRows = notes.Count + 2; // 标题 + 表头 + 数据
                        int numCols = 7;

#if CAD_R17
                        table.NumRows = numRows;
                        table.NumColumns = numCols;
#else
                        table.SetSize(numRows, numCols);
#endif

                        // 列宽设置 (自适应工程闭环图面排版)
                        table.SetColumnWidth(0, 15.0 * scale); // 序号
                        table.SetColumnWidth(1, 25.0 * scale); // 专业
                        table.SetColumnWidth(2, 45.0 * scale); // 批注标题
                        table.SetColumnWidth(3, 25.0 * scale); // 当前状态
                        table.SetColumnWidth(4, 30.0 * scale); // 批注者
                        table.SetColumnWidth(5, 80.0 * scale); // 审查意见详情
                        table.SetColumnWidth(6, 80.0 * scale); // 整改答复/流转说明

                        // 1. 标题行
                        table.SetRowHeight(0, rowHeight * 1.5);
                        table.SetTextString(0, 0, "图 纸 审 查 与 整 改 闭 环 汇 总 表");
                        table.SetTextHeight(0, 0, textHeight * 1.4);

                        // 2. 表头行
                        table.SetRowHeight(1, rowHeight);
                        string[] headers = new string[] { "序号", "专业", "批注标题", "当前状态", "批注者", "审查意见详情", "整改答复/流转说明" };
                        for (int c = 0; c < numCols; c++)
                        {
                            table.SetTextString(1, c, headers[c]);
                            table.SetTextHeight(1, c, textHeight);
                        }

                        // 3. 数据行
                        for (int i = 0; i < notes.Count; i++)
                        {
                            int rowIdx = i + 2;
                            table.SetRowHeight(rowIdx, rowHeight);
                            var note = notes[i];

                            table.SetTextString(rowIdx, 0, note.SequenceNumber.ToString());
                            table.SetTextString(rowIdx, 1, note.Discipline ?? "通用");
                            table.SetTextString(rowIdx, 2, note.Title ?? string.Empty);
                            table.SetTextString(rowIdx, 3, NoteLifecycleStateExtensions.ToDisplayName(note.State));
                            table.SetTextString(rowIdx, 4, note.Assignee ?? note.CreatedBy ?? string.Empty);
                            table.SetTextString(rowIdx, 5, note.Content ?? string.Empty);

                            // 第 7 列: 最新整改答复 / 流转说明
                            string replyStr = string.Empty;
                            if (note.Replies != null && note.Replies.Count > 0)
                            {
                                var lastReply = note.Replies[note.Replies.Count - 1];
                                string author = !string.IsNullOrEmpty(lastReply.Author) ? lastReply.Author : "协同人";
                                string text = !string.IsNullOrEmpty(lastReply.Content) ? lastReply.Content : NoteLifecycleStateExtensions.ToDisplayName(lastReply.StateTransitionTo);
                                replyStr = string.Format("[{0}] {1}: {2}", NoteLifecycleStateExtensions.ToDisplayName(lastReply.StateTransitionTo), author, text);
                            }
                            else if (note.AuditLogs != null && note.AuditLogs.Count > 0)
                            {
                                for (int k = note.AuditLogs.Count - 1; k >= 0; k--)
                                {
                                    var log = note.AuditLogs[k];
                                    if (!string.IsNullOrEmpty(log.FieldChanges) || !string.IsNullOrEmpty(log.Action))
                                    {
                                        replyStr = string.Format("{0} ({1})", log.FieldChanges ?? log.Action, log.Operator ?? "系统");
                                        break;
                                    }
                                }
                            }
                            if (string.IsNullOrEmpty(replyStr))
                            {
                                replyStr = "-";
                            }
                            table.SetTextString(rowIdx, 6, replyStr);

                            for (int c = 0; c < numCols; c++)
                            {
                                table.SetTextHeight(rowIdx, c, textHeight);
                            }
                        }

                        table.GenerateLayout();

                        ObjectId tableId = btr.AppendEntity(table);
                        tr.AddNewlyCreatedDBObject(table, true);
                        isTableAdded = true;

                        tr.Commit();
                        return tableId;
                    }
                    finally
                    {
                        if (!isTableAdded && table != null)
                        {
                            try { table.Dispose(); } catch { }
                        }
                    }
                }
            }
        }
    }
}
