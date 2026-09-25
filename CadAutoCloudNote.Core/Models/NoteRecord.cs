using System;
using System.Collections.Generic;
using CadAutoCloudNote.Core.Protocols;

namespace CadAutoCloudNote.Core.Models
{
    /// <summary>
    /// 批注主体业务核心模型 (XRecord 业务真源)
    /// </summary>
    public class NoteRecord
    {
        // 核心唯一标识
        public string NoteId { get; set; }           // GUID 字符串
        public int SequenceNumber { get; set; }      // 图纸内自增编号，如 1, 2, 3...
        
        // 业务内容
        public string Title { get; set; }            // 批注简述/标题
        public string Content { get; set; }          // 详细审查意见与批注内容
        public NoteLifecycleState State { get; set; }// 状态：草稿、待办、已改、驳回、已闭环
        
        // 分类与人员
        public string Discipline { get; set; }       // 专业 (建筑/结构/给排水/暖通/电气/景观等)
        public NotePriority Priority { get; set; }   // 优先级 (Normal=一般, Important=重要, Urgent=紧急)
        public string CreatedBy { get; set; }        // 提出人/审查人
        public string Author { get => CreatedBy; set => CreatedBy = value; }
        public string Assignee { get; set; }         // 批注者 (向后兼容)
        
        // 时间与审计
        public DateTime CreatedTime { get; set; }
        public DateTime CreatedAt { get => CreatedTime; set => CreatedTime = value; }
        public DateTime ModifiedTime { get; set; }
        public DateTime UpdatedAt { get => ModifiedTime; set => ModifiedTime = value; }
        public ModificationOrigin LastOrigin { get; set; }
        public bool IsErased { get; set; } = false;
        public string TemplateId { get; set; } = "tpl_general"; // 关联的工程场景模板 ID
        public Dictionary<string, string> MetadataFields { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // 关联分片引用数据
        public CloudEntity Cloud { get; set; }       // 云线图形数据
        public TextEntity Text { get; set; }         // 文字标注数据
        public List<NoteReply> Replies { get; set; } // 多轮对话/整改回复列表
        public List<AuditEntry> AuditLogs { get; set; }// 哈希审计链
        public List<AuditEntry> AuditTrail { get => AuditLogs; set => AuditLogs = value; }

        public NoteRecord()
        {
            NoteId = Guid.NewGuid().ToString("N");
            SequenceNumber = 1;
            Title = string.Empty;
            Content = string.Empty;
            State = NoteLifecycleState.Pending;
            Discipline = "建筑";
            Priority = NotePriority.Normal;
            CreatedBy = Environment.UserName;
            Assignee = string.Empty;
            CreatedTime = DateTime.Now;
            ModifiedTime = DateTime.Now;
            LastOrigin = ModificationOrigin.User;
            IsErased = false;

            Cloud = new CloudEntity { NoteId = NoteId };
            Text = new TextEntity { NoteId = NoteId };
            Replies = new List<NoteReply>();
            AuditLogs = new List<AuditEntry>();
        }
    }
}
