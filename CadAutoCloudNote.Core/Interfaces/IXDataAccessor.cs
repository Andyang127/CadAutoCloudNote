using System;
using Autodesk.AutoCAD.DatabaseServices;

namespace CadAutoCloudNote.Core.Interfaces
{
    /// <summary>
    /// 图元 XData 扩展数据读写抽象接口
    /// </summary>
    public interface IXDataAccessor
    {
        void EnsureRegApp(Database db, Transaction tr, string appName);
        string GetNoteId(DBObject obj, string appName);
        void SetNoteId(DBObject obj, string appName, string noteId);
        void RemoveNoteId(DBObject obj, string appName);
    }
}
