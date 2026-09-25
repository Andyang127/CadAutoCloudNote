using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace CadAutoCloudNote.Core.Interfaces
{
    /// <summary>
    /// 云线与标注图形生成接口
    /// </summary>
    public interface ICloudDrawer
    {
        ObjectId DrawCloudRect(Database db, Transaction tr, BlockTableRecord btr, Point3d pt1, Point3d pt2, double arcLength, string layer);
        ObjectId DrawCloudPolygon(Database db, Transaction tr, BlockTableRecord btr, Point3dCollection points, double arcLength, string layer);
        ObjectId DrawLeader(Database db, Transaction tr, BlockTableRecord btr, Point3d startPt, Point3d endPt, string layer);
        ObjectId DrawMText(Database db, Transaction tr, BlockTableRecord btr, Point3d insertPt, string text, double textHeight, string layer);
    }
}
