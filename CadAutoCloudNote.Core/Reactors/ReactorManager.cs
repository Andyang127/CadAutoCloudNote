using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.ApplicationServices;
#if CAD_R17 || CAD_R18
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
#else
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
#endif
using Autodesk.AutoCAD.Geometry;
using CadAutoCloudNote.Core.Config;
using CadAutoCloudNote.Core.Helpers;
using CadAutoCloudNote.Core.Models;
using CadAutoCloudNote.Core.Services;

namespace CadAutoCloudNote.Core.Reactors
{
    /// <summary>
    /// 反应器总控管理器：负责多文档生命周期监听、事务隔离与延迟任务批处理
    /// </summary>
    public class ReactorManager
    {
        private static ReactorManager _instance;
        private static readonly object _syncRoot = new object();
        public static ReactorManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_syncRoot)
                    {
                        if (_instance == null) _instance = new ReactorManager();
                    }
                }
                return _instance;
            }
        }

        private readonly Dictionary<Database, DocumentState> _docStates = new Dictionary<Database, DocumentState>();
        private bool _isInitialized = false;

        public DocumentState GetState(Database db)
        {
            if (db == null) return null;
            lock (_docStates)
            {
                if (!_docStates.TryGetValue(db, out DocumentState state))
                {
                    state = new DocumentState(db);
                    _docStates[db] = state;
                    AttachDatabase(db, state);
                }
                return state;
            }
        }

        public void Initialize()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            try
            {
                var docMgr = AcApp.DocumentManager;
                if (docMgr != null)
                {
                    docMgr.DocumentCreated += DocMgr_DocumentCreated;
                    docMgr.DocumentToBeDestroyed += DocMgr_DocumentToBeDestroyed;

                    foreach (Document doc in docMgr)
                    {
                        AttachDocument(doc);
                    }
                }
            }
            catch { }
        }

        public void Terminate()
        {
            try
            {
                var docMgr = AcApp.DocumentManager;
                if (docMgr != null)
                {
                    docMgr.DocumentCreated -= DocMgr_DocumentCreated;
                    docMgr.DocumentToBeDestroyed -= DocMgr_DocumentToBeDestroyed;

                    foreach (Document doc in docMgr)
                    {
                        DetachDocument(doc);
                    }
                }
                _docStates.Clear();
            }
            catch { }
        }

        private void AttachDocument(Document doc)
        {
            if (doc?.Database == null) return;
            GetState(doc.Database);
            doc.CommandWillStart += Doc_CommandWillStart;
            doc.CommandEnded += Doc_CommandEnded;
            doc.CommandCancelled += Doc_CommandCancelled;
            doc.CommandFailed += Doc_CommandFailed;
        }

        private void DetachDocument(Document doc)
        {
            if (doc == null) return;
            if (doc.Database != null)
            {
                DetachDatabase(doc.Database);
            }
            doc.CommandWillStart -= Doc_CommandWillStart;
            doc.CommandEnded -= Doc_CommandEnded;
            doc.CommandCancelled -= Doc_CommandCancelled;
            doc.CommandFailed -= Doc_CommandFailed;
        }

        private void DocMgr_DocumentCreated(object sender, DocumentCollectionEventArgs e)
        {
            AttachDocument(e.Document);
        }

        private void DocMgr_DocumentToBeDestroyed(object sender, DocumentCollectionEventArgs e)
        {
            DetachDocument(e.Document);
        }

        private void AttachDatabase(Database db, DocumentState state)
        {
            try
            {
                db.ObjectErased += Db_ObjectErased;
                db.ObjectModified += Db_ObjectModified;
                db.ObjectAppended += Db_ObjectAppended;
            }
            catch { }
        }

        private void DetachDatabase(Database db)
        {
            try
            {
                db.ObjectErased -= Db_ObjectErased;
                db.ObjectModified -= Db_ObjectModified;
                db.ObjectAppended -= Db_ObjectAppended;
                lock (_docStates)
                {
                    _docStates.Remove(db);
                }
            }
            catch { }
        }

        public static bool IsUndoOrRedoCommand(string cmdName)
        {
            if (string.IsNullOrEmpty(cmdName)) return false;
            string c = cmdName.Trim().TrimStart('_', '.').ToUpperInvariant();
            return c == "U" || c == "UNDO" || c == "-UNDO" || c == "REDO" || c == "MREDO" || c == "OOPS";
        }

        private void Doc_CommandWillStart(object sender, CommandEventArgs e)
        {
            Document doc = sender as Document;
            if (doc?.Database == null) return;
            DocumentState state = GetState(doc.Database);
            if (state == null) return;

            string cmd = e.GlobalCommandName;
            state.ActiveCommand = cmd;
            if (IsUndoOrRedoCommand(cmd))
            {
                state.IsCommandUndoing = true;
                lock (state.PendingTasks)
                {
                    state.PendingTasks.Clear();
                }
            }
        }

        private void Doc_CommandCancelled(object sender, CommandEventArgs e)
        {
            Document doc = sender as Document;
            if (doc?.Database == null) return;
            DocumentState state = GetState(doc.Database);
            if (state == null) return;

            state.ActiveCommand = null;
            state.IsCommandUndoing = false;
            lock (state.PendingTasks)
            {
                state.PendingTasks.Clear();
            }
        }

        private void Doc_CommandFailed(object sender, CommandEventArgs e)
        {
            Doc_CommandCancelled(sender, e);
        }

        private void Db_ObjectErased(object sender, ObjectErasedEventArgs e)
        {
            Database db = sender as Database;
            if (db == null) return;
            DocumentState state = GetState(db);
            if (state == null || state.IsInternalOperation || state.IsCommandUndoing) return;

            // 仅在图元真正被删除时进入候选队列
            if (e.Erased)
            {
                string noteId = null;
                try
                {
                    noteId = XDataHelper.Instance.GetNoteId(e.DBObject, AppConstants.XDATA_APP_NAME);
                }
                catch { }

                // 彻底过滤：非云线批注图元绝不入队！防止普通删除命令污染反应器队列与撤销链
                if (string.IsNullOrEmpty(noteId)) return;

                lock (state.PendingTasks)
                {
                    state.PendingTasks.Add(new PendingTask(PendingTaskType.Erased, e.DBObject.ObjectId, e.DBObject.Handle.ToString(), noteId));
                }
            }
        }

        private void Db_ObjectModified(object sender, ObjectEventArgs e)
        {
            Database db = sender as Database;
            if (db == null) return;
            DocumentState state = GetState(db);
            if (state == null || state.IsInternalOperation || state.IsCommandUndoing) return;

            string noteId = null;
            try
            {
                noteId = XDataHelper.Instance.GetNoteId(e.DBObject, AppConstants.XDATA_APP_NAME);
            }
            catch { }

            // 彻底过滤：非云线批注图元绝不入队！防止普通图元修改污染反应器队列与撤销链
            if (string.IsNullOrEmpty(noteId)) return;

            lock (state.PendingTasks)
            {
                state.PendingTasks.Add(new PendingTask(PendingTaskType.Modified, e.DBObject.ObjectId, e.DBObject.Handle.ToString(), noteId));
            }
        }

        private void Db_ObjectAppended(object sender, ObjectEventArgs e)
        {
            Database db = sender as Database;
            if (db == null) return;
            DocumentState state = GetState(db);
            if (state == null || state.IsInternalOperation || state.IsCommandUndoing) return;

            string noteId = null;
            try
            {
                noteId = XDataHelper.Instance.GetNoteId(e.DBObject, AppConstants.XDATA_APP_NAME);
            }
            catch { }

            // 彻底过滤：非云线批注图元绝不入队！防止普通绘制命令 (LINE, CIRCLE 等) 污染队列
            if (string.IsNullOrEmpty(noteId)) return;

            lock (state.PendingTasks)
            {
                state.PendingTasks.Add(new PendingTask(PendingTaskType.Appended, e.DBObject.ObjectId, e.DBObject.Handle.ToString(), noteId));
            }
        }

        private void Doc_CommandEnded(object sender, CommandEventArgs e)
        {
            Document doc = sender as Document;
            if (doc?.Database == null) return;
            DocumentState state = GetState(doc.Database);
            if (state == null) return;

            bool wasUndoing = state.IsCommandUndoing || IsUndoOrRedoCommand(e.GlobalCommandName);
            state.ActiveCommand = null;
            state.IsCommandUndoing = false;

            if (wasUndoing)
            {
                lock (state.PendingTasks)
                {
                    state.PendingTasks.Clear();
                }
                // 撤销/重做完成后，仅刷新看板 UI 呈现，绝不开启任何事务或写库！
                try
                {
                    CadAutoCloudNote.Core.UI.NoteUIProvider.Instance.RefreshPalette();
                }
                catch { }
                return;
            }

            FlushPendingTasks(doc.Database);
        }

        public void FlushPendingTasks(Database db)
        {
            DocumentState state = GetState(db);
            if (state == null || state.IsInternalOperation || state.IsCommandUndoing)
                return;

            List<PendingTask> tasksToProcess;
            lock (state.PendingTasks)
            {
                if (state.PendingTasks.Count == 0) return;
                tasksToProcess = new List<PendingTask>(state.PendingTasks);
                state.PendingTasks.Clear();
            }

            // 冲突消解
            tasksToProcess = PendingTaskConflictResolver.Resolve(tasksToProcess);
            if (tasksToProcess.Count == 0)
                return;

            using (state.EnterInternalScope())
            {
                IDisposable docLock = null;
                try
                {
                    Document doc = AcApp.DocumentManager?.MdiActiveDocument;
                    if (doc != null && doc.Database == db)
                    {
                        try { docLock = doc.LockDocument(); } catch { }
                    }

                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        List<PendingTask> appendedTasks = new List<PendingTask>();
                        List<PendingTask> erasedTasks = new List<PendingTask>();
                        List<PendingTask> modifiedTasks = new List<PendingTask>();

                        foreach (var task in tasksToProcess)
                        {
                            if (task.TaskType == PendingTaskType.Appended)
                                appendedTasks.Add(task);
                            else if (task.TaskType == PendingTaskType.Erased)
                                erasedTasks.Add(task);
                            else if (task.TaskType == PendingTaskType.Modified)
                                modifiedTasks.Add(task);
                        }

                        bool anyModified = false;

                        // 1. 处理克隆候选
                        if (appendedTasks.Count > 0)
                        {
                            if (CloneDetection.ProcessAppendedTasks(db, tr, appendedTasks))
                                anyModified = true;
                        }

                        // 2. 处理删除逻辑
                        if (erasedTasks.Count > 0)
                        {
                            if (ProcessErasedTasks(db, tr, erasedTasks))
                                anyModified = true;
                        }

                        // 3. 处理修改联动逻辑 (文字/云线移动时更新引线与数据)
                        if (modifiedTasks.Count > 0)
                        {
                            if (ProcessModifiedTasks(db, tr, modifiedTasks))
                                anyModified = true;
                        }

                        // 极其重要：只有在确实对数据库实体或 XRecord 产生了有效修改时，才提交事务！
                        // 杜绝空事务提交对 AutoCAD 撤销栈 (Undo Stack) 的破坏与无限撤销死循环！
                        if (anyModified)
                        {
                            tr.Commit();

                            if (modifiedTasks.Count > 0)
                            {
                                try
                                {
#if CAD_R17 || CAD_R18
                                    Document activeDoc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
#else
                                    Document activeDoc = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;
#endif
                                    activeDoc?.Editor?.UpdateScreen();
                                }
                                catch { }
                            }
                        }
                    }
                }
                catch
                {
                    // 保证反应器内部异常不会中断 CAD 宿主命令流
                }
                finally
                {
                    docLock?.Dispose();
                }
            }
        }

        private bool ProcessErasedTasks(Database db, Transaction tr, List<PendingTask> erasedTasks)
        {
            HashSet<string> notesToCheck = new HashSet<string>();
            HashSet<string> erasedHandles = new HashSet<string>();
            foreach (var task in erasedTasks)
            {
                if (!string.IsNullOrEmpty(task.HandleString))
                {
                    erasedHandles.Add(task.HandleString);
                }
                if (!string.IsNullOrEmpty(task.NoteId))
                {
                    notesToCheck.Add(task.NoteId);
                }
            }

            if (notesToCheck.Count == 0) return false;

            bool anyDeleted = false;
            foreach (string noteId in notesToCheck)
            {
                NoteRecord note = XRecordHelper.ReadNote(db, tr, noteId);
                if (note == null) continue;

                // 检查关键图元是否已被用户手动 Erase 删除
                bool isFrameErased = !string.IsNullOrEmpty(note.Text?.FrameHandle) && erasedHandles.Contains(note.Text.FrameHandle);
                bool isTextErased = !string.IsNullOrEmpty(note.Text?.MTextHandle) && erasedHandles.Contains(note.Text.MTextHandle);
                bool isLeaderErased = false;
                if (note.Text != null)
                {
                    if (!string.IsNullOrEmpty(note.Text.LeaderHandle) && erasedHandles.Contains(note.Text.LeaderHandle))
                    {
                        isLeaderErased = true;
                    }
                    if (note.Text.LeaderHandles != null)
                    {
                        foreach (string lh in note.Text.LeaderHandles)
                        {
                            if (erasedHandles.Contains(lh))
                            {
                                isLeaderErased = true;
                                break;
                            }
                        }
                    }
                }

                // 检查云线是否全部被删
                bool hasAliveCloud = false;
                if (note.Cloud?.EntityHandles != null)
                {
                    foreach (string h in note.Cloud.EntityHandles)
                    {
                        if (!erasedHandles.Contains(h) && XDataHelper.SafeGetObjectId(db, h, out ObjectId cId) && !cId.IsNull && !cId.IsErased)
                        {
                            hasAliveCloud = true;
                            break;
                        }
                    }
                }

                bool hasAliveText = false;
                if (note.Text != null && !isTextErased)
                {
                    if (!string.IsNullOrEmpty(note.Text.MTextHandle) && XDataHelper.SafeGetObjectId(db, note.Text.MTextHandle, out ObjectId tId) && !tId.IsNull && !tId.IsErased)
                    {
                        hasAliveText = true;
                    }
                }

                // 级联删除触发条件：用户删除引线、三角形徽标、文字、或全部云线任一部分，即级联抹除全套批注
                if (isFrameErased || isLeaderErased || isTextErased || !hasAliveText || !hasAliveCloud)
                {
                    // 1. 级联清理所有残留图元 (避免孤儿引线、徽标或云线漂浮)
                    if (note.Text?.LeaderHandles != null)
                    {
                        foreach (string lh in note.Text.LeaderHandles)
                        {
                            if (XDataHelper.SafeGetObjectId(db, lh, out ObjectId lId) && !lId.IsNull && !lId.IsErased)
                            {
                                try { tr.GetObject(lId, OpenMode.ForWrite).Erase(); } catch { }
                            }
                        }
                    }
                    if (!string.IsNullOrEmpty(note.Text?.LeaderHandle) && XDataHelper.SafeGetObjectId(db, note.Text.LeaderHandle, out ObjectId sLId) && !sLId.IsNull && !sLId.IsErased)
                    {
                        try { tr.GetObject(sLId, OpenMode.ForWrite).Erase(); } catch { }
                    }
                    if (!string.IsNullOrEmpty(note.Text?.MTextHandle) && XDataHelper.SafeGetObjectId(db, note.Text.MTextHandle, out ObjectId mId) && !mId.IsNull && !mId.IsErased)
                    {
                        try { tr.GetObject(mId, OpenMode.ForWrite).Erase(); } catch { }
                    }
                    if (!string.IsNullOrEmpty(note.Text?.FrameHandle) && XDataHelper.SafeGetObjectId(db, note.Text.FrameHandle, out ObjectId frId) && !frId.IsNull && !frId.IsErased)
                    {
                        try { tr.GetObject(frId, OpenMode.ForWrite).Erase(); } catch { }
                    }
                    if (note.Cloud?.EntityHandles != null)
                    {
                        foreach (string ch in note.Cloud.EntityHandles)
                        {
                            if (XDataHelper.SafeGetObjectId(db, ch, out ObjectId cId) && !cId.IsNull && !cId.IsErased)
                            {
                                try { tr.GetObject(cId, OpenMode.ForWrite).Erase(); } catch { }
                            }
                        }
                    }

                    // 2. 从图纸 XRecord 物理抹除记录
                    XRecordHelper.DeleteNote(db, tr, noteId);
                    anyDeleted = true;

                    try
                    {
                        var ed = AcApp.DocumentManager.MdiActiveDocument?.Editor;
                        ed?.WriteMessage(string.Format("\n[提示] 检测到批注 {0} 关联图元已手动删除，已同步移出看板与数据记录。\n", note.Title));
                    }
                    catch { }
                }
            }

            if (anyDeleted)
            {
                try
                {
                    CadAutoCloudNote.Core.UI.NoteUIProvider.Instance.RefreshPalette();
                }
                catch { }
            }

            return anyDeleted;
        }

        private bool ProcessModifiedTasks(Database db, Transaction tr, List<PendingTask> modifiedTasks)
        {
            HashSet<string> processedNotes = new HashSet<string>();
            bool anyModified = false;

            foreach (var task in modifiedTasks)
            {
                if (task.TargetId.IsNull || task.TargetId.IsErased) continue;

                try
                {
                    DBObject obj = tr.GetObject(task.TargetId, OpenMode.ForRead);
                    string noteId = XDataHelper.Instance.GetNoteId(obj, AppConstants.XDATA_APP_NAME);
                    if (string.IsNullOrEmpty(noteId) || processedNotes.Contains(noteId)) continue;

                    NoteRecord note = XRecordHelper.ReadNote(db, tr, noteId);
                    if (note == null) continue;

                    string handle = obj.Handle.ToString();
                    bool isMText = (note.Text != null && note.Text.MTextHandle == handle);
                    bool isFrame = (note.Text != null && note.Text.FrameHandle == handle);
                    bool isCloud = (note.Cloud != null && note.Cloud.EntityHandles != null && note.Cloud.EntityHandles.Contains(handle));
                    bool isLeader = (note.Text != null && (
                        (note.Text.LeaderHandles != null && note.Text.LeaderHandles.Contains(handle)) ||
                        note.Text.LeaderHandle == handle));

                    // 当文字、三角形徽标、云线或引线发生位移/拉伸等修改时，更新引线关联
                    if (!isMText && !isCloud && !isFrame && !isLeader) continue;

                    processedNotes.Add(noteId);
                    if (UpdateNoteAssociativity(db, tr, note, handle))
                    {
                        anyModified = true;
                    }
                }
                catch { }
            }

            return anyModified;
        }

        private bool UpdateNoteAssociativity(Database db, Transaction tr, NoteRecord note, string modifiedHandle = null)
        {
            try
            {
                // 1. 获取最新文字与徽标位置
                Point3d currentTextPt = note.Text != null ? note.Text.InsertionPoint : Point3d.Origin;
                double textHeight = note.Text != null ? Math.Max(2.5, note.Text.TextHeight) : 3.5;
                double triH = Math.Max(2.0, textHeight) * 1.8;
                double baseWidth = triH / 0.866025;
                double textGap = textHeight * 0.4;

                ObjectId fId = ObjectId.Null;
                bool hasFrame = note.Text != null && !string.IsNullOrEmpty(note.Text.FrameHandle)
                    && XDataHelper.SafeGetObjectId(db, note.Text.FrameHandle, out fId) && !fId.IsNull && !fId.IsErased;

                ObjectId mtextId = ObjectId.Null;
                bool hasMText = note.Text != null && !string.IsNullOrEmpty(note.Text.MTextHandle)
                    && XDataHelper.SafeGetObjectId(db, note.Text.MTextHandle, out mtextId) && !mtextId.IsNull && !mtextId.IsErased;

                // 检查修改源是否为引线（用户误点引线末端端头夹点拖动，或执行了 MOVE 引线操作）
                bool isLeaderModified = false;
                Point3d leaderEndPt = Point3d.Origin;
                if (!string.IsNullOrEmpty(modifiedHandle) && note.Text != null)
                {
                    bool matchesLeader = (note.Text.LeaderHandles != null && note.Text.LeaderHandles.Contains(modifiedHandle))
                                         || note.Text.LeaderHandle == modifiedHandle;
                    if (matchesLeader && XDataHelper.SafeGetObjectId(db, modifiedHandle, out ObjectId modLeaderId) && !modLeaderId.IsNull && !modLeaderId.IsErased)
                    {
                        Polyline modLeader = tr.GetObject(modLeaderId, OpenMode.ForWrite) as Polyline;
                        if (modLeader != null && modLeader.NumberOfVertices >= 2)
                        {
                            // 若有水平托线，转折点位于倒数第 2 个顶点，否则在末端
                            int kneeIdx = (hasFrame && modLeader.NumberOfVertices >= 3) ? modLeader.NumberOfVertices - 2 : modLeader.NumberOfVertices - 1;
                            leaderEndPt = modLeader.GetPoint3dAt(kneeIdx);
                            if (leaderEndPt.DistanceTo(note.Text.InsertionPoint) > 1e-3)
                            {
                                isLeaderModified = true;
                            }
                        }
                    }
                }

                // 判断是否在云线左侧
                Point3d approxCloudCenter = new Point3d((note.Cloud.MinPoint.X + note.Cloud.MaxPoint.X) / 2.0, (note.Cloud.MinPoint.Y + note.Cloud.MaxPoint.Y) / 2.0, 0);
                bool isLeft = currentTextPt.X < approxCloudCenter.X;

                if (isLeaderModified)
                {
                    // 用户通过引线转折点移动了整个批注：以转折点为最新基准点同步驱动文字和三角形徽标
                    currentTextPt = leaderEndPt;
                    note.Text.InsertionPoint = currentTextPt;
                    isLeft = currentTextPt.X < approxCloudCenter.X;

                    if (hasMText)
                    {
                        MText mtext = tr.GetObject(mtextId, OpenMode.ForWrite) as MText;
                        if (mtext != null)
                        {
                            if (hasFrame)
                            {
                                string rawTitle = !string.IsNullOrEmpty(note.Title) ? note.Title : (!string.IsNullOrEmpty(note.Content) ? note.Content : NoteService.GetDefaultSimpleTitle());
                                bool isSimple = string.IsNullOrEmpty(note.Content);
                                int seqForText = isSimple ? 0 : note.SequenceNumber;
                                double contentWidth = NoteService.CalculateNoteContentWidth(rawTitle, textHeight, seqForText, isSimple, note.Content);
                                bool hasLine2 = isSimple || !string.IsNullOrEmpty(note.Content);

                                if (hasLine2)
                                {
                                    mtext.Attachment = AttachmentPoint.MiddleLeft;
                                    mtext.Location = isLeft
                                        ? new Point3d(currentTextPt.X - (baseWidth / 2.0) - textGap - contentWidth, currentTextPt.Y, 0)
                                        : new Point3d(currentTextPt.X + (baseWidth / 2.0) + textGap, currentTextPt.Y, 0);
                                }
                                else
                                {
                                    mtext.Attachment = AttachmentPoint.BottomLeft;
                                    mtext.Location = isLeft
                                        ? new Point3d(currentTextPt.X - (baseWidth / 2.0) - textGap - contentWidth, currentTextPt.Y + textHeight * 0.18, 0)
                                        : new Point3d(currentTextPt.X + (baseWidth / 2.0) + textGap, currentTextPt.Y + textHeight * 0.18, 0);
                                }
                            }
                            else
                            {
                                mtext.Location = currentTextPt;
                            }
                            mtext.RecordGraphicsModified(true);
                        }
                    }

                    if (hasFrame)
                    {
                        Polyline tri = tr.GetObject(fId, OpenMode.ForWrite) as Polyline;
                        if (tri != null && tri.NumberOfVertices >= 3)
                        {
                            tri.SetPointAt(0, new Point2d(currentTextPt.X, currentTextPt.Y + triH * (2.0 / 3.0)));
                            tri.SetPointAt(1, new Point2d(currentTextPt.X - baseWidth / 2.0, currentTextPt.Y - triH * (1.0 / 3.0)));
                            tri.SetPointAt(2, new Point2d(currentTextPt.X + baseWidth / 2.0, currentTextPt.Y - triH * (1.0 / 3.0)));
                            tri.RecordGraphicsModified(true);
                        }
                    }
                }
                else if (hasMText)
                {
                    MText mtext = tr.GetObject(mtextId, OpenMode.ForWrite) as MText;
                    if (mtext != null)
                    {
                        if (hasFrame)
                        {
                            Polyline tri = tr.GetObject(fId, OpenMode.ForWrite) as Polyline;
                            if (tri != null && tri.NumberOfVertices >= 3)
                            {
                                string rawTitle = !string.IsNullOrEmpty(note.Title) ? note.Title : (!string.IsNullOrEmpty(note.Content) ? note.Content : NoteService.GetDefaultSimpleTitle());
                                bool isSimple = string.IsNullOrEmpty(note.Content);
                                int seqForText = isSimple ? 0 : note.SequenceNumber;
                                double contentWidth = NoteService.CalculateNoteContentWidth(rawTitle, textHeight, seqForText, isSimple, note.Content);
                                bool hasLine2 = isSimple || !string.IsNullOrEmpty(note.Content);

                                Point2d triApex = tri.GetPoint2dAt(0);
                                Point3d triCentroidPt = new Point3d(triApex.X, triApex.Y - triH * (2.0 / 3.0), 0);

                                if (triCentroidPt.DistanceTo(note.Text.InsertionPoint) > 1e-3)
                                {
                                    // 用户移动了小三角徽标
                                    currentTextPt = triCentroidPt;
                                    isLeft = currentTextPt.X < approxCloudCenter.X;
                                    if (hasLine2)
                                    {
                                        mtext.Attachment = AttachmentPoint.MiddleLeft;
                                        mtext.Location = isLeft
                                            ? new Point3d(currentTextPt.X - (baseWidth / 2.0) - textGap - contentWidth, currentTextPt.Y, 0)
                                            : new Point3d(currentTextPt.X + (baseWidth / 2.0) + textGap, currentTextPt.Y, 0);
                                    }
                                    else
                                    {
                                        mtext.Attachment = AttachmentPoint.BottomLeft;
                                        mtext.Location = isLeft
                                            ? new Point3d(currentTextPt.X - (baseWidth / 2.0) - textGap - contentWidth, currentTextPt.Y + textHeight * 0.18, 0)
                                            : new Point3d(currentTextPt.X + (baseWidth / 2.0) + textGap, currentTextPt.Y + textHeight * 0.18, 0);
                                    }
                                }
                                else
                                {
                                    // 用户移动了文字或初始同步
                                    isLeft = mtext.Location.X < approxCloudCenter.X;
                                    if (hasLine2)
                                    {
                                        if (mtext.Attachment != AttachmentPoint.MiddleLeft)
                                        {
                                            mtext.Attachment = AttachmentPoint.MiddleLeft;
                                        }
                                        currentTextPt = isLeft
                                            ? new Point3d(mtext.Location.X + contentWidth + (baseWidth / 2.0) + textGap, mtext.Location.Y, 0)
                                            : new Point3d(mtext.Location.X - (baseWidth / 2.0) - textGap, mtext.Location.Y, 0);
                                    }
                                    else
                                    {
                                        if (mtext.Attachment != AttachmentPoint.BottomLeft)
                                        {
                                            mtext.Attachment = AttachmentPoint.BottomLeft;
                                        }
                                        currentTextPt = isLeft
                                            ? new Point3d(mtext.Location.X + contentWidth + (baseWidth / 2.0) + textGap, mtext.Location.Y - textHeight * 0.18, 0)
                                            : new Point3d(mtext.Location.X - (baseWidth / 2.0) - textGap, mtext.Location.Y - textHeight * 0.18, 0);
                                    }
                                    tri.SetPointAt(0, new Point2d(currentTextPt.X, currentTextPt.Y + triH * (2.0 / 3.0)));
                                    tri.SetPointAt(1, new Point2d(currentTextPt.X - baseWidth / 2.0, currentTextPt.Y - triH * (1.0 / 3.0)));
                                    tri.SetPointAt(2, new Point2d(currentTextPt.X + baseWidth / 2.0, currentTextPt.Y - triH * (1.0 / 3.0)));
                                }
                                tri.RecordGraphicsModified(true);
                            }
                        }
                        else
                        {
                            currentTextPt = mtext.Location;
                        }
                        mtext.RecordGraphicsModified(true);
                        note.Text.InsertionPoint = currentTextPt;
                    }
                }

                // 2. 获取每个云线的最新几何包围盒并更新聚合包络
                List<Extents3d> cloudBoxes = new List<Extents3d>();
                double totalMinX = double.MaxValue, totalMinY = double.MaxValue;
                double totalMaxX = double.MinValue, totalMaxY = double.MinValue;

                if (note.Cloud?.EntityHandles != null)
                {
                    foreach (string hStr in note.Cloud.EntityHandles)
                    {
                        if (XDataHelper.SafeGetObjectId(db, hStr, out ObjectId cId) && !cId.IsNull && !cId.IsErased)
                        {
                            Polyline cPoly = tr.GetObject(cId, OpenMode.ForRead) as Polyline;
                            if (cPoly != null)
                            {
                                try
                                {
                                    Extents3d ext = cPoly.GeometricExtents;
                                    cloudBoxes.Add(ext);
                                    totalMinX = Math.Min(totalMinX, ext.MinPoint.X);
                                    totalMinY = Math.Min(totalMinY, ext.MinPoint.Y);
                                    totalMaxX = Math.Max(totalMaxX, ext.MaxPoint.X);
                                    totalMaxY = Math.Max(totalMaxY, ext.MaxPoint.Y);
                                }
                                catch { }
                            }
                        }
                    }
                }

                if (cloudBoxes.Count > 0)
                {
                    note.Cloud.MinPoint = new Point3d(totalMinX, totalMinY, 0);
                    note.Cloud.MaxPoint = new Point3d(totalMaxX, totalMaxY, 0);
                }

                // 3. 更新所有分枝引线顶点
                List<string> leaderHandles = note.Text?.LeaderHandles;
                if ((leaderHandles == null || leaderHandles.Count == 0) && !string.IsNullOrEmpty(note.Text?.LeaderHandle))
                {
                    leaderHandles = new List<string> { note.Text.LeaderHandle };
                }

                if (leaderHandles != null && leaderHandles.Count > 0)
                {
                    double drawingScale = ScaleHelper.GetDrawingScale(db);
                    var settings = ConfigManager.Instance.CurrentSettings;
                    double arrowSize = Math.Max(2.0, settings.ArrowSizeRatio * drawingScale);
                    int leaderType = note.Text != null ? note.Text.LeaderType : settings.LeaderType;

                    // 计算极简批注三角形外边缘相切折点及托线末端
                    // 水平托线的作用是稳固承托文字，其伸展方向必须严格由文字相对三角形方位 (isLeft) 决定，杜绝反向悬空！
                    bool leadLeft = isLeft;

                    Point3d kneePt = currentTextPt;

                    Point3d? landingEnd = null;
                    if (hasFrame)
                    {
                        string rawTitle = !string.IsNullOrEmpty(note.Title) ? note.Title : (!string.IsNullOrEmpty(note.Content) ? note.Content : NoteService.GetDefaultSimpleTitle());
                        bool isSimple = string.IsNullOrEmpty(note.Content);
                        int seqForText = isSimple ? 0 : note.SequenceNumber;
                        double landingLength = NoteService.CalculateSimpleNoteLandingLength(rawTitle, textHeight, seqForText, isSimple, note.Content);
                        landingEnd = leadLeft ? new Point3d(currentTextPt.X - landingLength, currentTextPt.Y, 0) : new Point3d(currentTextPt.X + landingLength, currentTextPt.Y, 0);
                    }

                    for (int i = 0; i < leaderHandles.Count; i++)
                    {
                        string lHandle = leaderHandles[i];
                        if (XDataHelper.SafeGetObjectId(db, lHandle, out ObjectId lId) && !lId.IsNull && !lId.IsErased)
                        {
                            Polyline leader = tr.GetObject(lId, OpenMode.ForWrite) as Polyline;
                            if (leader != null)
                            {
                                Extents3d box = (i < cloudBoxes.Count) ? cloudBoxes[i] : new Extents3d(note.Cloud.MinPoint, note.Cloud.MaxPoint);
                                Point3d edgePt = NoteService.GetClosestPointOnCloud(box.MinPoint, box.MaxPoint, kneePt);
                                double dist = edgePt.DistanceTo(kneePt);

                                // 只有主引线 (i == 0) 保留水平托线，分枝引线直接汇交至相切折点 kneePt
                                Point3d? curLanding = (i == 0 && hasFrame) ? landingEnd : null;

                                if (dist > 1e-4)
                                {
                                    Vector3d u = (kneePt - edgePt) / dist;

                                    if (leaderType == 1)
                                    {
                                        // 1 = 点引线 (Donut 实心圆点 + 直线段)
                                        double dotRadius = Math.Max(0.75, arrowSize * 0.25);
                                        Point3d pA = edgePt - u * (dotRadius / 2.0);
                                        Point3d pB = edgePt + u * (dotRadius / 2.0);
                                        int targetCount = curLanding.HasValue ? 5 : 4;

                                        while (leader.NumberOfVertices < targetCount)
                                        {
                                            leader.AddVertexAt(leader.NumberOfVertices, new Point2d(kneePt.X, kneePt.Y), 0, 0, 0);
                                        }
                                        while (leader.NumberOfVertices > targetCount)
                                        {
                                            leader.RemoveVertexAt(leader.NumberOfVertices - 1);
                                        }

                                        leader.SetPointAt(0, new Point2d(pA.X, pA.Y));
                                        leader.SetPointAt(1, new Point2d(pB.X, pB.Y));
                                        leader.SetPointAt(2, new Point2d(pA.X, pA.Y));
                                        leader.SetPointAt(3, new Point2d(kneePt.X, kneePt.Y));

                                        leader.SetBulgeAt(0, 1.0);
                                        leader.SetBulgeAt(1, 1.0);
                                        leader.SetBulgeAt(2, 0.0);
                                        leader.SetBulgeAt(3, 0.0);

                                        leader.SetStartWidthAt(0, dotRadius);
                                        leader.SetEndWidthAt(0, dotRadius);
                                        leader.SetStartWidthAt(1, dotRadius);
                                        leader.SetEndWidthAt(1, dotRadius);
                                        leader.SetStartWidthAt(2, 0.0);
                                        leader.SetEndWidthAt(2, 0.0);
                                        leader.SetStartWidthAt(3, 0.0);
                                        leader.SetEndWidthAt(3, 0.0);

                                        if (curLanding.HasValue)
                                        {
                                            leader.SetPointAt(4, new Point2d(curLanding.Value.X, curLanding.Value.Y));
                                            leader.SetBulgeAt(4, 0.0);
                                            leader.SetStartWidthAt(4, 0.0);
                                            leader.SetEndWidthAt(4, 0.0);
                                        }
                                    }
                                    else if (leaderType == 0)
                                    {
                                        // 0 = 带箭头引线
                                        double arrowLen = Math.Min(arrowSize, dist * 0.8);
                                        double arrowWidth = arrowLen / 3.0;
                                        Point3d arrowBase = edgePt + u * arrowLen;
                                        int targetCount = curLanding.HasValue ? 4 : 3;

                                        while (leader.NumberOfVertices < targetCount)
                                        {
                                            leader.AddVertexAt(leader.NumberOfVertices, new Point2d(kneePt.X, kneePt.Y), 0, 0, 0);
                                        }
                                        while (leader.NumberOfVertices > targetCount)
                                        {
                                            leader.RemoveVertexAt(leader.NumberOfVertices - 1);
                                        }

                                        leader.SetPointAt(0, new Point2d(edgePt.X, edgePt.Y));
                                        leader.SetPointAt(1, new Point2d(arrowBase.X, arrowBase.Y));
                                        leader.SetPointAt(2, new Point2d(kneePt.X, kneePt.Y));

                                        leader.SetBulgeAt(0, 0.0);
                                        leader.SetBulgeAt(1, 0.0);
                                        leader.SetBulgeAt(2, 0.0);

                                        leader.SetStartWidthAt(0, 0.0);
                                        leader.SetEndWidthAt(0, arrowWidth);
                                        leader.SetStartWidthAt(1, 0.0);
                                        leader.SetEndWidthAt(1, 0.0);
                                        leader.SetStartWidthAt(2, 0.0);
                                        leader.SetEndWidthAt(2, 0.0);

                                        if (curLanding.HasValue)
                                        {
                                            leader.SetPointAt(3, new Point2d(curLanding.Value.X, curLanding.Value.Y));
                                            leader.SetBulgeAt(3, 0.0);
                                            leader.SetStartWidthAt(3, 0.0);
                                            leader.SetEndWidthAt(3, 0.0);
                                        }
                                    }
                                    else
                                    {
                                        // 默认直线
                                        int targetCount = curLanding.HasValue ? 3 : 2;
                                        while (leader.NumberOfVertices < targetCount)
                                        {
                                            leader.AddVertexAt(leader.NumberOfVertices, new Point2d(kneePt.X, kneePt.Y), 0, 0, 0);
                                        }
                                        while (leader.NumberOfVertices > targetCount)
                                        {
                                            leader.RemoveVertexAt(leader.NumberOfVertices - 1);
                                        }

                                        leader.SetPointAt(0, new Point2d(edgePt.X, edgePt.Y));
                                        leader.SetPointAt(1, new Point2d(kneePt.X, kneePt.Y));
                                        leader.SetBulgeAt(0, 0.0);
                                        leader.SetBulgeAt(1, 0.0);

                                        if (curLanding.HasValue)
                                        {
                                            leader.SetPointAt(2, new Point2d(curLanding.Value.X, curLanding.Value.Y));
                                            leader.SetBulgeAt(2, 0.0);
                                        }
                                    }
                                }

                                leader.RecordGraphicsModified(true);
                            }
                        }
                    }
                }

                // 4. 持久化同步写回 XRecord
                XRecordHelper.SaveNoteSlices(db, tr, note);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
