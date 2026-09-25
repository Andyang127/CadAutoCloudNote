using System;
using Autodesk.AutoCAD.DatabaseServices;
using CadAutoCloudNote.Core.Interfaces;

namespace CadAutoCloudNote.Core.Helpers
{
    /// <summary>
    /// 图元 XData 扩展数据读写网关
    /// </summary>
    public class XDataHelper : IXDataAccessor
    {
        private static readonly XDataHelper _instance = new XDataHelper();
        public static XDataHelper Instance => _instance;

        /// <summary>
        /// 确保指定扩展应用程序名称已在数据库中注册
        /// </summary>
        public void EnsureRegApp(Database db, Transaction tr, string appName)
        {
            if (db == null || tr == null || string.IsNullOrEmpty(appName))
                return;

            RegAppTable regTable = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
            if (!regTable.Has(appName))
            {
                regTable.UpgradeOpen();
                RegAppTableRecord regRecord = new RegAppTableRecord
                {
                    Name = appName
                };
                regTable.Add(regRecord);
                tr.AddNewlyCreatedDBObject(regRecord, true);
            }
        }

        /// <summary>
        /// 从图元读取绑定的 NoteId
        /// </summary>
        public string GetNoteId(DBObject obj, string appName)
        {
            if (obj == null || string.IsNullOrEmpty(appName))
                return null;

            try
            {
                using (ResultBuffer rb = obj.GetXDataForApplication(appName))
                {
                    if (rb == null)
                        return null;

                    TypedValue[] values = rb.AsArray();
                    for (int i = 0; i < values.Length; i++)
                    {
                        if (values[i].TypeCode == (int)DxfCode.ExtendedDataAsciiString)
                        {
                            return values[i].Value as string;
                        }
                    }
                }
            }
            catch
            {
                // 忽略非致命读取异常
            }

            return null;
        }

        /// <summary>
        /// 给图元设置 NoteId 扩展数据
        /// </summary>
        public void SetNoteId(DBObject obj, string appName, string noteId)
        {
            if (obj == null || string.IsNullOrEmpty(appName) || string.IsNullOrEmpty(noteId))
                return;

            using (ResultBuffer rb = new ResultBuffer(
                new TypedValue((int)DxfCode.ExtendedDataRegAppName, appName),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, noteId)))
            {
                obj.XData = rb;
            }
        }

        /// <summary>
        /// 清除图元上的 NoteId 扩展数据
        /// </summary>
        public void RemoveNoteId(DBObject obj, string appName)
        {
            if (obj == null || string.IsNullOrEmpty(appName))
                return;

            using (ResultBuffer rb = new ResultBuffer(
                new TypedValue((int)DxfCode.ExtendedDataRegAppName, appName)))
            {
                obj.XData = rb;
            }
        }

        /// <summary>
        /// 安全根据十六进制句柄解析 ObjectId（跨 AutoCAD 2007~2026 全代兼容）
        /// </summary>
        public static bool SafeGetObjectId(Database db, string handleStr, out ObjectId id)
        {
            id = ObjectId.Null;
            if (db == null || string.IsNullOrEmpty(handleStr)) return false;

            try
            {
                long val = Convert.ToInt64(handleStr, 16);
                Handle handle = new Handle(val);
#if CAD_R17
                id = db.GetObjectId(false, handle, 0);
                return !id.IsNull;
#else
                return db.TryGetObjectId(handle, out id);
#endif
            }
            catch
            {
                return false;
            }
        }
    }
}
