using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace CadAutoCloudNote.Core.Interfaces
{
    /// <summary>
    /// CAD 宿主平台适配接口
    /// </summary>
    public interface ICadPlatformAdapter
    {
        string PlatformName { get; }
        string VersionString { get; }
        void ZoomToEntity(ObjectId id);
        void ZoomToWindow(Point3d min, Point3d max);
        void RunCompare(string dwgPath1, string dwgPath2);
    }
}
