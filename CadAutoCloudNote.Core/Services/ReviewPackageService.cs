using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using CadAutoCloudNote.Core.Helpers;
using CadAutoCloudNote.Core.Models;
using CadAutoCloudNote.Core.Protocols;

namespace CadAutoCloudNote.Core.Services
{
    /// <summary>
    /// 离线审查包 (.cloudnotepkg) 导出与导入同步服务
    /// </summary>
    public static class ReviewPackageService
    {
        public static bool ExportPackage(Database db, string filePath, out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                List<NoteRecord> notes = NoteService.Instance.GetAllNotes(db);
                ReviewPackageManifest manifest = new ReviewPackageManifest
                {
                    PackageId = Guid.NewGuid().ToString("N"),
                    PackageVersion = "1.0",
                    CreatedAt = DateTime.Now,
                    ExportedBy = Environment.UserName,
                    TotalNoteCount = notes.Count,
                    DrawingFingerprint = db.Filename ?? "UNKNOWN"
                };

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendFormat("  \"packageId\": \"{0}\",\n", manifest.PackageId);
                sb.AppendFormat("  \"packageVersion\": \"{0}\",\n", manifest.PackageVersion);
                sb.AppendFormat("  \"createdAt\": \"{0:o}\",\n", manifest.CreatedAt);
                sb.AppendFormat("  \"exportedBy\": \"{0}\",\n", manifest.ExportedBy);
                sb.AppendFormat("  \"totalNoteCount\": {0},\n", manifest.TotalNoteCount);
                sb.AppendFormat("  \"drawingFingerprint\": \"{0}\",\n", (manifest.DrawingFingerprint ?? string.Empty).Replace("\\", "\\\\"));
                sb.AppendLine("  \"notes\": [");

                for (int i = 0; i < notes.Count; i++)
                {
                    var n = notes[i];
                    sb.AppendLine("    {");
                    sb.AppendFormat("      \"noteId\": \"{0}\",\n", n.NoteId);
                    sb.AppendFormat("      \"seq\": {0},\n", n.SequenceNumber);
                    sb.AppendFormat("      \"title\": \"{0}\",\n", EscapeJson(n.Title));
                    sb.AppendFormat("      \"content\": \"{0}\",\n", EscapeJson(n.Content));
                    sb.AppendFormat("      \"state\": {0},\n", (int)n.State);
                    sb.AppendFormat("      \"discipline\": \"{0}\",\n", EscapeJson(n.Discipline));
                    sb.AppendFormat("      \"priority\": {0},\n", (int)n.Priority);
                    sb.AppendFormat("      \"createdBy\": \"{0}\",\n", EscapeJson(n.CreatedBy));
                    sb.AppendFormat("      \"assignee\": \"{0}\",\n", EscapeJson(n.Assignee));
                    sb.AppendFormat("      \"createdTime\": \"{0:o}\",\n", n.CreatedTime);
                    sb.AppendFormat("      \"modifiedTime\": \"{0:o}\"\n", n.ModifiedTime);
                    sb.Append("    }");
                    if (i < notes.Count - 1) sb.Append(",");
                    sb.AppendLine();
                }

                sb.AppendLine("  ]");
                sb.AppendLine("}");

                File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public static bool ImportPackage(Database db, string filePath, out int importedCount, out string errorMessage)
        {
            importedCount = 0;
            errorMessage = string.Empty;

            try
            {
                if (!File.Exists(filePath))
                {
                    errorMessage = "审查包文件不存在";
                    return false;
                }

                string json = File.ReadAllText(filePath, Encoding.UTF8);
                // 提取批注并合并入图纸
                List<NoteRecord> existing = NoteService.Instance.GetAllNotes(db);
                HashSet<string> existingIds = new HashSet<string>();
                foreach (var e in existing)
                {
                    existingIds.Add(e.NoteId);
                }

                // 简易解析：逐段提取 noteId
                string[] parts = json.Split(new string[] { "\"noteId\":" }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 1; i < parts.Length; i++)
                {
                    string segment = parts[i];
                    string noteId = ExtractValue(segment);
                    if (!string.IsNullOrEmpty(noteId) && !existingIds.Contains(noteId))
                    {
                        NoteRecord n = new NoteRecord
                        {
                            NoteId = noteId,
                            SequenceNumber = existing.Count + importedCount + 1,
                            Title = "导入批注 " + noteId.Substring(0, Math.Min(6, noteId.Length)),
                            Content = "从离线审查包导入",
                            State = NoteLifecycleState.Pending,
                            CreatedBy = Environment.UserName,
                            CreatedTime = DateTime.Now,
                            ModifiedTime = DateTime.Now
                        };

                        NoteService.Instance.SaveNote(db, n);
                        importedCount++;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        private static string ExtractValue(string chunk)
        {
            int firstQuote = chunk.IndexOf('"');
            if (firstQuote < 0) return null;
            int secondQuote = chunk.IndexOf('"', firstQuote + 1);
            if (secondQuote < 0) return null;
            return chunk.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\r", "\\r")
                    .Replace("\n", "\\n");
        }
    }
}
