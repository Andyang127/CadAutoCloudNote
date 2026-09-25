using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using CadAutoCloudNote.Core.Helpers;
using CadAutoCloudNote.Core.Models;
using CadAutoCloudNote.Core.Reactors;

namespace CadAutoCloudNote.Core.Services
{
    /// <summary>
    /// 历史版本数据迁移与字典结构平滑升级服务
    /// </summary>
    public static class MigrationService
    {
        public static int MigrateLegacyData(Database db)
        {
            if (db == null) return 0;

            int migratedCount = 0;
            var docState = ReactorManager.Instance.GetState(db);
            using (docState?.EnterInternalScope())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    DBDictionary nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
                    if (!nod.Contains(AppConstants.ROOT_DICT_NAME))
                    {
                        tr.Commit();
                        return 0;
                    }

                    DBDictionary rootDict = (DBDictionary)tr.GetObject(nod.GetAt(AppConstants.ROOT_DICT_NAME), OpenMode.ForRead);
                    if (rootDict.Contains(AppConstants.SUB_DICT_NAME))
                    {
                        // 发现旧版单一字典
                        DBDictionary oldSubDict = (DBDictionary)tr.GetObject(rootDict.GetAt(AppConstants.SUB_DICT_NAME), OpenMode.ForWrite);
                        // 收集并重排入分卷字典
                        List<NoteRecord> legacyNotes = XRecordHelper.ReadAllNotes(db, tr);
                        foreach (var note in legacyNotes)
                        {
                            XRecordHelper.SaveNoteSlices(db, tr, note);
                            migratedCount++;
                        }
                    }

                    tr.Commit();
                }
            }

            return migratedCount;
        }
    }
}
