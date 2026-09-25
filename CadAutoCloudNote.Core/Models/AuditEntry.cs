using System;

namespace CadAutoCloudNote.Core.Models
{
    /// <summary>
    /// 链式防篡改哈希审计条目
    /// </summary>
    public class AuditEntry
    {
        public string EntryId { get; set; }
        public string Id { get => EntryId; set => EntryId = value; }
        public string NoteId { get; set; }
        public string OperationType { get; set; }    // Create, Modify, StatusChange, Reply, Clone, Erase
        public string Action { get => OperationType; set => OperationType = value; }
        public string OperatorName { get; set; }
        public string Operator { get => OperatorName; set => OperatorName = value; }
        public DateTime Timestamp { get; set; }
        public string FieldChanges { get; set; }     // 变更字段 JSON 或摘要
        public string DiffSummary { get => FieldChanges; set => FieldChanges = value; }
        public string PreviousHash { get; set; }     // 前序条目 SHA256 哈希
        public string CurrentHash { get; set; }      // 当前条目哈希值

        public AuditEntry()
        {
            EntryId = Guid.NewGuid().ToString("N");
            NoteId = "";
            OperationType = "Create";
            OperatorName = Environment.UserName;
            Timestamp = DateTime.Now;
            FieldChanges = "";
            PreviousHash = "0000000000000000000000000000000000000000000000000000000000000000";
            CurrentHash = "";
        }
    }
}
