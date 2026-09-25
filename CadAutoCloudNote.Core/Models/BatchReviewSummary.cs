using System;
using System.Collections.Generic;

namespace CadAutoCloudNote.Core.Models
{
    /// <summary>
    /// 批量多图审图合并分析结果摘要
    /// </summary>
    public class BatchReviewSummary
    {
        public int TotalDrawingsProcessed { get; set; }
        public int TotalDrawings { get => TotalDrawingsProcessed; set => TotalDrawingsProcessed = value; }

        public int TotalNotesFound { get; set; }
        public int TotalNotes { get => TotalNotesFound; set => TotalNotesFound = value; }

        public int PendingCount { get; set; }
        public int PendingNotes { get => PendingCount; set => PendingCount = value; }

        public int ResolvedCount { get; set; }
        public int ResolvedNotes { get => ResolvedCount; set => ResolvedCount = value; }

        public int ClosedCount { get; set; }
        public int ClosedNotes { get => ClosedCount; set => ClosedCount = value; }

        public Dictionary<string, int> NotesByDiscipline { get; set; }
        public List<string> ErrorDrawings { get; set; }

        public BatchReviewSummary()
        {
            NotesByDiscipline = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            ErrorDrawings = new List<string>();
        }
    }
}
