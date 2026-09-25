using System;
using CadAutoCloudNote.Core.Protocols;

namespace CadAutoCloudNote.Core.Models
{
    /// <summary>
    /// 批注多轮回复数据
    /// </summary>
    public class NoteReply
    {
        public string ReplyId { get; set; }
        public string Id { get => ReplyId; set => ReplyId = value; }
        public string Author { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; }
        public NoteLifecycleState StateTransitionTo { get; set; }
        public NoteLifecycleState TransitionState { get => StateTransitionTo; set => StateTransitionTo = value; }

        public NoteReply()
        {
            ReplyId = Guid.NewGuid().ToString("N");
            Author = Environment.UserName;
            Content = "";
            Timestamp = DateTime.Now;
            StateTransitionTo = NoteLifecycleState.Pending;
        }

        public NoteReply(string author, string content, NoteLifecycleState newState)
        {
            ReplyId = Guid.NewGuid().ToString("N");
            Author = string.IsNullOrEmpty(author) ? Environment.UserName : author;
            Content = content ?? "";
            Timestamp = DateTime.Now;
            StateTransitionTo = newState;
        }
    }
}
