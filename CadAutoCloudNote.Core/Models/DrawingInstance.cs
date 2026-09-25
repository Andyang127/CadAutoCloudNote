using System;

namespace CadAutoCloudNote.Core.Models
{
    /// <summary>
    /// 图纸指纹模型 (用于区分不同图纸环境、审查包对齐)
    /// </summary>
    public class DrawingInstance
    {
        public string DrawingId { get; set; }        // 首次打开或初始化时生成的唯一图纸 GUID
        public string FileName { get; set; }         // 文件名
        public string FilePath { get; set; }         // 完整路径
        public string DrawingTitle { get; set; }     // 图名
        public string DrawingNumber { get; set; }    // 图号
        public DateTime LastSavedAt { get; set; }
        public int TotalNotesCount { get; set; }

        public DrawingInstance()
        {
            DrawingId = Guid.NewGuid().ToString("N");
            FileName = "";
            FilePath = "";
            DrawingTitle = "";
            DrawingNumber = "";
            LastSavedAt = DateTime.Now;
            TotalNotesCount = 0;
        }
    }
}
