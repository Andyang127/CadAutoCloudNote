using System;
using Autodesk.AutoCAD.DatabaseServices;

namespace CadAutoCloudNote.Core.Reactors
{
    public enum PendingTaskType
    {
        Erased,
        Modified,
        Appended
    }

    /// <summary>
    /// 反应器捕获的待处理图元变动任务
    /// </summary>
    public class PendingTask
    {
        public PendingTaskType TaskType { get; set; }
        public ObjectId TargetId { get; set; }
        public string HandleString { get; set; }
        public string NoteId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public PendingTask(PendingTaskType type, ObjectId id, string handle, string noteId)
        {
            TaskType = type;
            TargetId = id;
            HandleString = handle;
            NoteId = noteId;
        }
    }
}
