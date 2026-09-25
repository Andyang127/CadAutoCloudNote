using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Geometry;

namespace CadAutoCloudNote.Core.Models
{
    public enum CloudShapeType
    {
        Rectangle = 0,
        Polygon = 1,
        Freehand = 2,
        Converted = 3
    }

    /// <summary>
    /// 云线矩形角点对（兼容 .NET 2.0~8.0 全版本，替代 Tuple）
    /// </summary>
    public struct CloudRect
    {
        public Point3d Pt1;
        public Point3d Pt2;

        public CloudRect(Point3d pt1, Point3d pt2)
        {
            Pt1 = pt1;
            Pt2 = pt2;
        }
    }

    /// <summary>
    /// 云线关联图元数据
    /// </summary>
    public class CloudEntity
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string NoteId { get; set; } = string.Empty;
        public List<string> EntityHandles { get; set; } = new List<string>();
        public List<string> Handles { get => EntityHandles; set => EntityHandles = value; }

        public CloudShapeType ShapeType { get; set; } = CloudShapeType.Rectangle;
        public double ArcLength { get; set; } = 5.0;

        public Point3d MinPoint { get; set; } = Point3d.Origin;
        public Point3d MaxPoint { get; set; } = Point3d.Origin;

        public double MinX { get => MinPoint.X; set => MinPoint = new Point3d(value, MinPoint.Y, MinPoint.Z); }
        public double MinY { get => MinPoint.Y; set => MinPoint = new Point3d(MinPoint.X, value, MinPoint.Z); }
        public double MaxX { get => MaxPoint.X; set => MaxPoint = new Point3d(value, MaxPoint.Y, MaxPoint.Z); }
        public double MaxY { get => MaxPoint.Y; set => MaxPoint = new Point3d(MaxPoint.X, value, MaxPoint.Z); }
    }
}
