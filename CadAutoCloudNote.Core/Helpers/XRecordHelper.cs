using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using CadAutoCloudNote.Core.Models;
using CadAutoCloudNote.Core.Protocols;

namespace CadAutoCloudNote.Core.Helpers
{
    /// <summary>
    /// XRecord 4+2 分片读写与自动分卷管理引擎
    /// </summary>
    public static class XRecordHelper
    {
        private const string ROOT_DICT_KEY = AppConstants.ROOT_DICT_NAME; // "CAD_CLOUD_NOTE_ROOT_DICT"
        private const string VOLUME_PREFIX = "CAD_NOTE_DICT_V";
        private const string INDEX_KEY = "NOTE_INDEX";

        /// <summary>
        /// 获取或创建根字典
        /// </summary>
        public static DBDictionary GetOrCreateRootDictionary(Database db, Transaction tr)
        {
            DBDictionary nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForWrite);
            if (nod.Contains(ROOT_DICT_KEY))
            {
                try
                {
                    ObjectId rootId = nod.GetAt(ROOT_DICT_KEY);
                    if (!rootId.IsNull && !rootId.IsErased)
                    {
                        return (DBDictionary)tr.GetObject(rootId, OpenMode.ForWrite);
                    }
                }
                catch { }
            }

            DBDictionary rootDict = new DBDictionary();
            nod.SetAt(ROOT_DICT_KEY, rootDict);
            tr.AddNewlyCreatedDBObject(rootDict, true);
            return rootDict;
        }

        /// <summary>
        /// 获取所有分卷字典
        /// </summary>
        public static List<DBDictionary> GetAllVolumeDictionaries(Database db, Transaction tr, OpenMode mode = OpenMode.ForRead)
        {
            List<DBDictionary> volumes = new List<DBDictionary>();
            DBDictionary nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
            if (!nod.Contains(ROOT_DICT_KEY))
                return volumes;

            try
            {
                ObjectId rootId = nod.GetAt(ROOT_DICT_KEY);
                if (rootId.IsNull || rootId.IsErased) return volumes;

                DBDictionary rootDict = (DBDictionary)tr.GetObject(rootId, OpenMode.ForRead);
                foreach (DBDictionaryEntry entry in rootDict)
                {
                    if (entry.Key.StartsWith(VOLUME_PREFIX, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!entry.Value.IsNull && !entry.Value.IsErased)
                        {
                            try
                            {
                                DBDictionary volDict = (DBDictionary)tr.GetObject(entry.Value, mode);
                                volumes.Add(volDict);
                            }
                            catch { }
                        }
                    }
                }

                // 兼顾旧版单字典名
                if (rootDict.Contains(AppConstants.SUB_DICT_NAME))
                {
                    try
                    {
                        ObjectId oldId = rootDict.GetAt(AppConstants.SUB_DICT_NAME);
                        if (!oldId.IsNull && !oldId.IsErased)
                        {
                            try
                            {
                                DBDictionary defaultDict = (DBDictionary)tr.GetObject(oldId, mode);
                                volumes.Add(defaultDict);
                            }
                            catch
                            {
                                DBDictionary defaultDict = (DBDictionary)tr.GetObject(oldId, OpenMode.ForRead);
                                volumes.Add(defaultDict);
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }

            return volumes;
        }

        /// <summary>
        /// 获取或创建最新可用分卷字典（容量达到 50 条自动创建下一卷）
        /// </summary>
        public static DBDictionary GetOrCreateActiveVolumeDictionary(Database db, Transaction tr)
        {
            DBDictionary rootDict = GetOrCreateRootDictionary(db, tr);
            int maxVolIndex = 0;
            DBDictionary latestVol = null;

            foreach (DBDictionaryEntry entry in rootDict)
            {
                if (entry.Key.StartsWith(VOLUME_PREFIX, StringComparison.OrdinalIgnoreCase))
                {
                    string suffix = entry.Key.Substring(VOLUME_PREFIX.Length);
                    if (int.TryParse(suffix, out int idx))
                    {
                        if (idx > maxVolIndex)
                        {
                            maxVolIndex = idx;
                            latestVol = (DBDictionary)tr.GetObject(entry.Value, OpenMode.ForWrite);
                        }
                    }
                }
            }

            if (latestVol != null)
            {
                int noteCount = GetVolumeNoteCount(latestVol, tr);
                if (noteCount < CapacityPolicy.MAX_NOTES_PER_VOLUME)
                {
                    return latestVol;
                }
            }

            // 需要新建下一个卷
            int newVolIndex = maxVolIndex + 1;
            string newVolKey = string.Format("{0}{1:D3}", VOLUME_PREFIX, newVolIndex);
            DBDictionary newVol = new DBDictionary();
            rootDict.SetAt(newVolKey, newVol);
            tr.AddNewlyCreatedDBObject(newVol, true);
            return newVol;
        }

        private static int GetVolumeNoteCount(DBDictionary volDict, Transaction tr)
        {
            if (volDict == null || !volDict.Contains(INDEX_KEY))
                return 0;

            try
            {
                ObjectId idxId = volDict.GetAt(INDEX_KEY);
                if (idxId.IsNull || idxId.IsErased) return 0;
                Xrecord idxRecord = tr.GetObject(idxId, OpenMode.ForRead) as Xrecord;
                using (ResultBuffer rb = idxRecord?.Data)
                {
                    if (rb == null) return 0;
                    TypedValue[] arr = rb.AsArray();
                    return arr.Length;
                }
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 保存批注的 4+2 分片
        /// </summary>
        public static void SaveNoteSlices(Database db, Transaction tr, NoteRecord note)
        {
            // 先在已有卷中查找该 noteId
            List<DBDictionary> allVolumes = GetAllVolumeDictionaries(db, tr, OpenMode.ForWrite);
            DBDictionary targetVol = null;

            foreach (DBDictionary vol in allVolumes)
            {
                if (vol.Contains("NOTE_" + note.NoteId + "_MAIN"))
                {
                    targetVol = vol;
                    break;
                }
            }

            if (targetVol == null)
            {
                targetVol = GetOrCreateActiveVolumeDictionary(db, tr);
                // 更新该卷索引
                AddNoteToVolumeIndex(targetVol, tr, note.NoteId);
            }

            string prefix = "NOTE_" + note.NoteId + "_";

            // 1. MAIN 片段
            WriteXRecord(targetVol, tr, prefix + "MAIN", BuildMainSlice(note));

            // 2. CLOUDS 片段
            WriteXRecord(targetVol, tr, prefix + "CLOUDS", BuildCloudsSlice(note));

            // 3. TEXT 片段
            WriteXRecord(targetVol, tr, prefix + "TEXT", BuildTextSlice(note));

            // 4. REPLIES 片段
            WriteXRecord(targetVol, tr, prefix + "REPLIES", BuildRepliesSlice(note));

            // 5. AUDIT 片段
            WriteXRecord(targetVol, tr, prefix + "AUDIT", BuildAuditSlice(note));

            // 6. AI 片段
            WriteXRecord(targetVol, tr, prefix + "AI", BuildAiSlice(note));
        }

        /// <summary>
        /// 删除指定批注分片
        /// </summary>
        public static bool DeleteNoteSlices(Database db, Transaction tr, string noteId)
        {
            List<DBDictionary> allVolumes = GetAllVolumeDictionaries(db, tr, OpenMode.ForWrite);
            bool found = false;
            string prefix = "NOTE_" + noteId + "_";
            string[] slices = new string[] { "MAIN", "CLOUDS", "TEXT", "REPLIES", "AUDIT", "AI" };

            foreach (DBDictionary vol in allVolumes)
            {
                if (vol.Contains(prefix + "MAIN"))
                {
                    foreach (string slice in slices)
                    {
                        string key = prefix + slice;
                        if (vol.Contains(key))
                        {
                            try
                            {
                                ObjectId recId = vol.GetAt(key);
                                if (!recId.IsNull && !recId.IsErased)
                                {
                                    DBObject obj = tr.GetObject(recId, OpenMode.ForWrite);
                                    obj.Erase(true);
                                }
                                vol.Remove(key);
                            }
                            catch { }
                        }
                    }
                    RemoveNoteFromVolumeIndex(vol, tr, noteId);
                    found = true;
                    break;
                }
            }

            return found;
        }

        /// <summary>
        /// 读取单条批注
        /// </summary>
        public static NoteRecord ReadNote(Database db, Transaction tr, string noteId)
        {
            if (db == null || tr == null || string.IsNullOrEmpty(noteId)) return null;
            string cleanId = noteId.Trim();
            List<DBDictionary> allVolumes = GetAllVolumeDictionaries(db, tr);
            string targetKey = "NOTE_" + cleanId + "_MAIN";

            foreach (DBDictionary vol in allVolumes)
            {
                if (vol.Contains(targetKey))
                {
                    NoteRecord note = new NoteRecord();
                    note.NoteId = cleanId;

                    // 读取各分片
                    string prefix = "NOTE_" + cleanId + "_";
                    ParseMainSlice(ReadXRecord(vol, tr, prefix + "MAIN"), note);
                    ParseCloudsSlice(ReadXRecord(vol, tr, prefix + "CLOUDS"), note);
                    ParseTextSlice(ReadXRecord(vol, tr, prefix + "TEXT"), note);
                    ParseRepliesSlice(ReadXRecord(vol, tr, prefix + "REPLIES"), note);
                    ParseAuditSlice(ReadXRecord(vol, tr, prefix + "AUDIT"), note);

                    return note;
                }
            }

            // 兜底：大小写不敏感遍历所有分卷寻找匹配键
            foreach (DBDictionary vol in allVolumes)
            {
                foreach (DBDictionaryEntry entry in vol)
                {
                    if (entry.Key.StartsWith("NOTE_", StringComparison.OrdinalIgnoreCase) &&
                        entry.Key.EndsWith("_MAIN", StringComparison.OrdinalIgnoreCase))
                    {
                        string mid = entry.Key.Substring(5, entry.Key.Length - 10);
                        if (string.Equals(mid, cleanId, StringComparison.OrdinalIgnoreCase))
                        {
                            NoteRecord note = new NoteRecord();
                            note.NoteId = mid;
                            string prefix = "NOTE_" + mid + "_";
                            ParseMainSlice(ReadXRecord(vol, tr, prefix + "MAIN"), note);
                            ParseCloudsSlice(ReadXRecord(vol, tr, prefix + "CLOUDS"), note);
                            ParseTextSlice(ReadXRecord(vol, tr, prefix + "TEXT"), note);
                            ParseRepliesSlice(ReadXRecord(vol, tr, prefix + "REPLIES"), note);
                            ParseAuditSlice(ReadXRecord(vol, tr, prefix + "AUDIT"), note);
                            return note;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 读取所有批注
        /// </summary>
        public static List<NoteRecord> ReadAllNotes(Database db, Transaction tr)
        {
            List<NoteRecord> list = new List<NoteRecord>();
            List<DBDictionary> allVolumes = GetAllVolumeDictionaries(db, tr);

            foreach (DBDictionary vol in allVolumes)
            {
                if (vol == null || !vol.Contains(INDEX_KEY))
                    continue;

                try
                {
                    ObjectId idxId = vol.GetAt(INDEX_KEY);
                    if (idxId.IsNull || idxId.IsErased) continue;
                    Xrecord idxRecord = tr.GetObject(idxId, OpenMode.ForRead) as Xrecord;
                    if (idxRecord == null) continue;
                    using (ResultBuffer rb = idxRecord.Data)
                    {
                        if (rb == null) continue;
                        foreach (TypedValue tv in rb)
                        {
                            if (tv.TypeCode == (int)DxfCode.Text)
                            {
                                string noteId = tv.Value as string;
                                if (!string.IsNullOrEmpty(noteId))
                                {
                                    NoteRecord note = ReadNote(db, tr, noteId);
                                    if (note != null && !note.IsErased)
                                    {
                                        list.Add(note);
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            return list;
        }

        /// <summary>
        /// 从图纸字典中彻底物理删除批注记录分片与索引
        /// </summary>
        public static bool DeleteNote(Database db, Transaction tr, string noteId)
        {
            if (db == null || tr == null || string.IsNullOrEmpty(noteId)) return false;

            List<DBDictionary> allVolumes = GetAllVolumeDictionaries(db, tr, OpenMode.ForWrite);
            string prefix = "NOTE_" + noteId + "_";
            bool removed = false;

            foreach (DBDictionary vol in allVolumes)
            {
                if (vol.Contains(prefix + "MAIN"))
                {
                    RemoveNoteFromVolumeIndex(vol, tr, noteId);

                    string[] keys = new string[] { prefix + "MAIN", prefix + "CLOUDS", prefix + "TEXT", prefix + "REPLIES", prefix + "AUDIT" };
                    foreach (string k in keys)
                    {
                        if (vol.Contains(k))
                        {
                            try
                            {
                                ObjectId recId = vol.GetAt(k);
                                DBObject obj = tr.GetObject(recId, OpenMode.ForWrite);
                                obj.Erase();
                                vol.Remove(k);
                            }
                            catch { }
                        }
                    }
                    removed = true;
                }
            }
            return removed;
        }

        private static void WriteXRecord(DBDictionary dict, Transaction tr, string key, ResultBuffer rb)
        {
            if (dict == null || rb == null) return;
            using (rb)
            {
                try
                {
                    if (dict.Contains(key))
                    {
                        ObjectId recId = dict.GetAt(key);
                        if (!recId.IsNull && !recId.IsErased)
                        {
                            Xrecord rec = tr.GetObject(recId, OpenMode.ForWrite) as Xrecord;
                            if (rec != null)
                            {
                                rec.Data = rb;
                                return;
                            }
                        }
                    }
                }
                catch { }

                try
                {
                    Xrecord rec = new Xrecord();
                    rec.Data = rb;
                    dict.SetAt(key, rec);
                    tr.AddNewlyCreatedDBObject(rec, true);
                }
                catch { }
            }
        }

        private static ResultBuffer ReadXRecord(DBDictionary dict, Transaction tr, string key)
        {
            if (dict == null || !dict.Contains(key))
                return null;

            try
            {
                ObjectId recId = dict.GetAt(key);
                if (recId.IsNull || recId.IsErased) return null;
                Xrecord rec = tr.GetObject(recId, OpenMode.ForRead) as Xrecord;
                return rec?.Data;
            }
            catch
            {
                return null;
            }
        }

        private static void AddNoteToVolumeIndex(DBDictionary vol, Transaction tr, string noteId)
        {
            if (vol == null || string.IsNullOrEmpty(noteId)) return;
            List<string> ids = new List<string>();
            try
            {
                if (vol.Contains(INDEX_KEY))
                {
                    ObjectId recId = vol.GetAt(INDEX_KEY);
                    if (!recId.IsNull && !recId.IsErased)
                    {
                        Xrecord rec = tr.GetObject(recId, OpenMode.ForRead) as Xrecord;
                        using (ResultBuffer rb = rec?.Data)
                        {
                            if (rb != null)
                            {
                                foreach (TypedValue tv in rb)
                                {
                                    if (tv.TypeCode == (int)DxfCode.Text)
                                        ids.Add(tv.Value as string);
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            if (!ids.Contains(noteId))
            {
                ids.Add(noteId);
                ResultBuffer newRb = new ResultBuffer();
                foreach (string id in ids)
                {
                    newRb.Add(new TypedValue((int)DxfCode.Text, id));
                }
                WriteXRecord(vol, tr, INDEX_KEY, newRb);
            }
        }

        private static void RemoveNoteFromVolumeIndex(DBDictionary vol, Transaction tr, string noteId)
        {
            if (vol == null || string.IsNullOrEmpty(noteId)) return;
            try
            {
                if (vol.Contains(INDEX_KEY))
                {
                    List<string> ids = new List<string>();
                    ObjectId recId = vol.GetAt(INDEX_KEY);
                    if (!recId.IsNull && !recId.IsErased)
                    {
                        Xrecord rec = tr.GetObject(recId, OpenMode.ForRead) as Xrecord;
                        using (ResultBuffer rb = rec?.Data)
                        {
                            if (rb != null)
                            {
                                foreach (TypedValue tv in rb)
                                {
                                    if (tv.TypeCode == (int)DxfCode.Text)
                                    {
                                        string s = tv.Value as string;
                                        if (s != noteId) ids.Add(s);
                                    }
                                }
                            }
                        }
                    }

                    ResultBuffer newRb = new ResultBuffer();
                    foreach (string id in ids)
                    {
                        newRb.Add(new TypedValue((int)DxfCode.Text, id));
                    }
                    WriteXRecord(vol, tr, INDEX_KEY, newRb);
                }
            }
            catch { }
        }

        // --- 分片编解码 ---

        private static ResultBuffer BuildMainSlice(NoteRecord note)
        {
            ResultBuffer rb = new ResultBuffer();
            rb.Add(new TypedValue((int)DxfCode.Text, note.NoteId ?? string.Empty));
            rb.Add(new TypedValue((int)DxfCode.Int32, note.SequenceNumber));
            rb.Add(new TypedValue((int)DxfCode.Text, note.Title ?? string.Empty));
            rb.Add(new TypedValue((int)DxfCode.Text, note.Content ?? string.Empty));
            rb.Add(new TypedValue((int)DxfCode.Int32, (int)note.State));
            rb.Add(new TypedValue((int)DxfCode.Text, note.Discipline ?? string.Empty));
            rb.Add(new TypedValue((int)DxfCode.Int32, (int)note.Priority));
            rb.Add(new TypedValue((int)DxfCode.Text, note.CreatedBy ?? string.Empty));
            rb.Add(new TypedValue((int)DxfCode.Text, note.Assignee ?? string.Empty));
            rb.Add(new TypedValue((int)DxfCode.Text, note.CreatedTime.ToString("o")));
            rb.Add(new TypedValue((int)DxfCode.Text, note.ModifiedTime.ToString("o")));
            rb.Add(new TypedValue((int)DxfCode.Int32, note.IsErased ? 1 : 0));
            rb.Add(new TypedValue((int)DxfCode.Text, note.TemplateId ?? "tpl_general"));
            return rb;
        }

        private static void ParseMainSlice(ResultBuffer rb, NoteRecord note)
        {
            if (rb == null) return;
            using (rb)
            {
                TypedValue[] arr = rb.AsArray();
                if (arr.Length > 0) note.NoteId = arr[0].Value as string;
                if (arr.Length > 1 && arr[1].Value is int seq) note.SequenceNumber = seq;
                if (arr.Length > 2) note.Title = arr[2].Value as string;
                if (arr.Length > 3) note.Content = arr[3].Value as string;
                if (arr.Length > 4 && arr[4].Value is int st) note.State = (NoteLifecycleState)st;
                if (arr.Length > 5) note.Discipline = arr[5].Value as string;
                if (arr.Length > 6 && arr[6].Value is int pr) note.Priority = (NotePriority)pr;
                if (arr.Length > 7) note.CreatedBy = arr[7].Value as string;
                if (arr.Length > 8) note.Assignee = arr[8].Value as string;
                if (arr.Length > 9 && DateTime.TryParse(arr[9].Value as string, out DateTime ct)) note.CreatedTime = ct;
                if (arr.Length > 10 && DateTime.TryParse(arr[10].Value as string, out DateTime mt)) note.ModifiedTime = mt;
                if (arr.Length > 11 && arr[11].Value is int er) note.IsErased = (er == 1);
                if (arr.Length > 12 && arr[12].Value is string tpl && !string.IsNullOrEmpty(tpl)) note.TemplateId = tpl;
            }
        }

        private static ResultBuffer BuildCloudsSlice(NoteRecord note)
        {
            ResultBuffer rb = new ResultBuffer();
            if (note.Cloud != null)
            {
                rb.Add(new TypedValue((int)DxfCode.Text, note.Cloud.Id ?? string.Empty));
                rb.Add(new TypedValue((int)DxfCode.Int32, (int)note.Cloud.ShapeType));
                rb.Add(new TypedValue((int)DxfCode.Real, note.Cloud.ArcLength));
                rb.Add(new TypedValue((int)DxfCode.XCoordinate, note.Cloud.MinPoint));
                rb.Add(new TypedValue((int)DxfCode.XCoordinate + 1, note.Cloud.MaxPoint));
                string handles = string.Join(",", note.Cloud.EntityHandles.ToArray());
                rb.Add(new TypedValue((int)DxfCode.Text, handles));
            }
            return rb;
        }

        private static void ParseCloudsSlice(ResultBuffer rb, NoteRecord note)
        {
            if (rb == null) return;
            using (rb)
            {
                TypedValue[] arr = rb.AsArray();
                if (arr.Length >= 6)
                {
                    note.Cloud = new CloudEntity();
                    note.Cloud.Id = arr[0].Value as string;
                    if (arr[1].Value is int st) note.Cloud.ShapeType = (CloudShapeType)st;
                    if (arr[2].Value is double al) note.Cloud.ArcLength = al;
                    if (arr[3].Value is Point3d p1) note.Cloud.MinPoint = p1;
                    if (arr[4].Value is Point3d p2) note.Cloud.MaxPoint = p2;
                    string handles = arr[5].Value as string;
                    if (!string.IsNullOrEmpty(handles))
                    {
                        string[] parts = handles.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        note.Cloud.EntityHandles = new List<string>(parts);
                    }
                }
            }
        }

        private static ResultBuffer BuildTextSlice(NoteRecord note)
        {
            ResultBuffer rb = new ResultBuffer();
            if (note.Text != null)
            {
                rb.Add(new TypedValue((int)DxfCode.Text, note.Text.Id ?? string.Empty));
                rb.Add(new TypedValue((int)DxfCode.Text, note.Text.MTextHandle ?? string.Empty));
                string leaderHandlesStr = (note.Text.LeaderHandles != null && note.Text.LeaderHandles.Count > 0)
                    ? string.Join(",", note.Text.LeaderHandles.ToArray())
                    : (note.Text.LeaderHandle ?? string.Empty);
                rb.Add(new TypedValue((int)DxfCode.Text, leaderHandlesStr));
                rb.Add(new TypedValue((int)DxfCode.Real, note.Text.TextHeight));
                rb.Add(new TypedValue((int)DxfCode.XCoordinate, note.Text.InsertionPoint));
                rb.Add(new TypedValue((int)DxfCode.Text, note.Text.FormattedContent ?? string.Empty));
                rb.Add(new TypedValue((int)DxfCode.Int32, note.Text.LeaderType));
                rb.Add(new TypedValue((int)DxfCode.Text, note.Text.FrameHandle ?? string.Empty));
            }
            return rb;
        }

        private static void ParseTextSlice(ResultBuffer rb, NoteRecord note)
        {
            if (rb == null) return;
            using (rb)
            {
                TypedValue[] arr = rb.AsArray();
                if (arr.Length >= 6)
                {
                    note.Text = new TextEntity();
                    note.Text.Id = arr[0].Value as string;
                    note.Text.MTextHandle = arr[1].Value as string;
                    string ldrStr = arr[2].Value as string ?? string.Empty;
                    note.Text.LeaderHandles = new List<string>();
                    if (!string.IsNullOrEmpty(ldrStr))
                    {
                        var parts = ldrStr.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var p in parts)
                        {
                            string trimmed = p.Trim();
                            if (!string.IsNullOrEmpty(trimmed) && !note.Text.LeaderHandles.Contains(trimmed))
                            {
                                note.Text.LeaderHandles.Add(trimmed);
                            }
                        }
                    }
                    if (arr[3].Value is double th) note.Text.TextHeight = th;
                    if (arr[4].Value is Point3d ip) note.Text.InsertionPoint = ip;
                    note.Text.FormattedContent = arr[5].Value as string;
                    if (arr.Length >= 7 && arr[6].Value is int lt) note.Text.LeaderType = lt;
                    if (arr.Length >= 8 && arr[7].Value is string fh) note.Text.FrameHandle = fh;
                }
            }
        }

        private static ResultBuffer BuildRepliesSlice(NoteRecord note)
        {
            ResultBuffer rb = new ResultBuffer();
            if (note.Replies != null)
            {
                rb.Add(new TypedValue((int)DxfCode.Int32, note.Replies.Count));
                foreach (NoteReply reply in note.Replies)
                {
                    rb.Add(new TypedValue((int)DxfCode.Text, reply.Id ?? string.Empty));
                    rb.Add(new TypedValue((int)DxfCode.Text, reply.Author ?? string.Empty));
                    rb.Add(new TypedValue((int)DxfCode.Text, reply.Timestamp.ToString("o")));
                    rb.Add(new TypedValue((int)DxfCode.Text, reply.Content ?? string.Empty));
                    rb.Add(new TypedValue((int)DxfCode.Int32, (int)reply.TransitionState));
                }
            }
            return rb;
        }

        private static void ParseRepliesSlice(ResultBuffer rb, NoteRecord note)
        {
            if (rb == null) return;
            using (rb)
            {
                TypedValue[] arr = rb.AsArray();
                if (arr.Length > 0 && arr[0].Value is int count)
                {
                    note.Replies = new List<NoteReply>();
                    int idx = 1;
                    for (int i = 0; i < count; i++)
                    {
                        if (idx + 4 < arr.Length)
                        {
                            NoteReply r = new NoteReply();
                            r.Id = arr[idx].Value as string;
                            r.Author = arr[idx + 1].Value as string;
                            if (DateTime.TryParse(arr[idx + 2].Value as string, out DateTime ts)) r.Timestamp = ts;
                            r.Content = arr[idx + 3].Value as string;
                            if (arr[idx + 4].Value is int st) r.TransitionState = (NoteLifecycleState)st;
                            note.Replies.Add(r);
                            idx += 5;
                        }
                    }
                }
            }
        }

        private static ResultBuffer BuildAuditSlice(NoteRecord note)
        {
            ResultBuffer rb = new ResultBuffer();
            if (note.AuditLogs != null)
            {
                rb.Add(new TypedValue((int)DxfCode.Int32, note.AuditLogs.Count));
                foreach (AuditEntry entry in note.AuditLogs)
                {
                    rb.Add(new TypedValue((int)DxfCode.Text, entry.Id ?? string.Empty));
                    rb.Add(new TypedValue((int)DxfCode.Text, entry.Operator ?? string.Empty));
                    rb.Add(new TypedValue((int)DxfCode.Text, entry.Timestamp.ToString("o")));
                    rb.Add(new TypedValue((int)DxfCode.Text, entry.Action ?? string.Empty));
                    rb.Add(new TypedValue((int)DxfCode.Text, entry.DiffSummary ?? string.Empty));
                    rb.Add(new TypedValue((int)DxfCode.Text, entry.CurrentHash ?? string.Empty));
                    rb.Add(new TypedValue((int)DxfCode.Text, entry.PreviousHash ?? string.Empty));
                }
            }
            return rb;
        }

        private static void ParseAuditSlice(ResultBuffer rb, NoteRecord note)
        {
            if (rb == null) return;
            using (rb)
            {
                TypedValue[] arr = rb.AsArray();
                if (arr.Length > 0 && arr[0].Value is int count)
                {
                    note.AuditLogs = new List<AuditEntry>();
                    int idx = 1;
                    for (int i = 0; i < count; i++)
                    {
                        if (idx + 6 < arr.Length)
                        {
                            AuditEntry e = new AuditEntry();
                            e.Id = arr[idx].Value as string;
                            e.Operator = arr[idx + 1].Value as string;
                            if (DateTime.TryParse(arr[idx + 2].Value as string, out DateTime ts)) e.Timestamp = ts;
                            e.Action = arr[idx + 3].Value as string;
                            e.DiffSummary = arr[idx + 4].Value as string;
                            e.CurrentHash = arr[idx + 5].Value as string;
                            e.PreviousHash = arr[idx + 6].Value as string;
                            note.AuditLogs.Add(e);
                            idx += 7;
                        }
                    }
                }
            }
        }

        private static ResultBuffer BuildAiSlice(NoteRecord note)
        {
            ResultBuffer rb = new ResultBuffer();
            rb.Add(new TypedValue((int)DxfCode.Text, "AI_SLICE_V1"));
            return rb;
        }
    }
}
