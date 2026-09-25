using System;
using System.Collections.Generic;

namespace CadAutoCloudNote.Core.Models
{
    /// <summary>
    /// 离线审查包 (.cloudnotepkg) 清单模型
    /// </summary>
    public class ReviewPackageManifest
    {
        public string PackageId { get; set; }
        public string PackageVersion { get; set; }
        public string SourceDrawingId { get; set; }
        public string SourceDrawingName { get; set; }
        public string DrawingFingerprint { get => SourceDrawingName; set => SourceDrawingName = value; }
        public string ExportedBy { get; set; }
        public DateTime ExportedAt { get; set; }
        public DateTime CreatedAt { get => ExportedAt; set => ExportedAt = value; }
        public int NoteCount { get; set; }
        public int TotalNoteCount { get => NoteCount; set => NoteCount = value; }
        public List<string> NoteIds { get; set; }

        public ReviewPackageManifest()
        {
            PackageId = Guid.NewGuid().ToString("N");
            PackageVersion = AppConstants.Version;
            SourceDrawingId = "";
            SourceDrawingName = "";
            ExportedBy = Environment.UserName;
            ExportedAt = DateTime.Now;
            NoteCount = 0;
            NoteIds = new List<string>();
        }
    }
}
