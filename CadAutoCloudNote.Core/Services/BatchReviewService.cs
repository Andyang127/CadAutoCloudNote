using System;
using System.Collections.Generic;
using System.IO;
using Autodesk.AutoCAD.DatabaseServices;
using CadAutoCloudNote.Core.Helpers;
using CadAutoCloudNote.Core.Models;
using CadAutoCloudNote.Core.Protocols;

namespace CadAutoCloudNote.Core.Services
{
    /// <summary>
    /// 批量多图审图合并与分析汇总服务（支持 Side-Database 后台免开图秒级分析）
    /// </summary>
    public static class BatchReviewService
    {
        public static BatchReviewSummary ScanDirectory(string directoryPath)
        {
            BatchReviewSummary summary = new BatchReviewSummary();
            if (!Directory.Exists(directoryPath))
                return summary;

            string[] dwgFiles = Directory.GetFiles(directoryPath, "*.dwg", SearchOption.TopDirectoryOnly);
            summary.TotalDrawings = dwgFiles.Length;

            foreach (string dwg in dwgFiles)
            {
                try
                {
                    using (Database sideDb = new Database(false, true))
                    {
                        sideDb.ReadDwgFile(dwg, FileShare.Read, true, string.Empty);
                        using (Transaction tr = sideDb.TransactionManager.StartTransaction())
                        {
                            List<NoteRecord> notes = XRecordHelper.ReadAllNotes(sideDb, tr);
                            summary.TotalNotes += notes.Count;
                            foreach (var n in notes)
                            {
                                if (n.State == NoteLifecycleState.Pending) summary.PendingNotes++;
                                else if (n.State == NoteLifecycleState.Resolved) summary.ResolvedNotes++;
                                else if (n.State == NoteLifecycleState.Closed) summary.ClosedNotes++;
                            }
                            tr.Commit();
                        }
                    }
                }
                catch
                {
                    // 容错处理受损或被独占锁定的图纸
                }
            }

            return summary;
        }
    }
}
