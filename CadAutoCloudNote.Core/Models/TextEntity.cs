using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Geometry;

namespace CadAutoCloudNote.Core.Models
{
    /// <summary>
    /// 批注文字与引线关联图元数据
    /// </summary>
    public class TextEntity
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string NoteId { get; set; } = string.Empty;
        public string MTextHandle { get; set; } = string.Empty;
        public string FrameHandle { get; set; } = string.Empty; // 极简徽标外框 (如等腰三角形)
        public int LeaderType { get; set; } = 0; // 引线模式: 0=箭头, 1=点, 2=无引线

        public List<string> LeaderHandles { get; set; } = new List<string>();

        public string LeaderHandle
        {
            get => (LeaderHandles != null && LeaderHandles.Count > 0) ? LeaderHandles[0] : string.Empty;
            set
            {
                if (LeaderHandles == null) LeaderHandles = new List<string>();
                if (string.IsNullOrEmpty(value)) return;
                if (!LeaderHandles.Contains(value))
                {
                    if (LeaderHandles.Count == 0) LeaderHandles.Add(value);
                    else LeaderHandles[0] = value;
                }
            }
        }

        public Point3d InsertionPoint { get; set; } = Point3d.Origin;
        public double InsertionX { get => InsertionPoint.X; set => InsertionPoint = new Point3d(value, InsertionPoint.Y, InsertionPoint.Z); }
        public double InsertionY { get => InsertionPoint.Y; set => InsertionPoint = new Point3d(InsertionPoint.X, value, InsertionPoint.Z); }

        public double TextHeight { get; set; } = 3.5;
        public string FormattedContent { get; set; } = string.Empty;
        public string FormattedText { get => FormattedContent; set => FormattedContent = value; }
        public string PlainText { get; set; } = string.Empty;
    }
}
