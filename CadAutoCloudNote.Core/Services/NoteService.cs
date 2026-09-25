using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using CadAutoCloudNote.Core.Config;
using CadAutoCloudNote.Core.Helpers;
using CadAutoCloudNote.Core.Interfaces;
using CadAutoCloudNote.Core.Models;
using CadAutoCloudNote.Core.Protocols;
using CadAutoCloudNote.Core.Reactors;

namespace CadAutoCloudNote.Core.Services
{
    /// <summary>
    /// 批注核心业务服务：管理批注生成、云线绘制、引线拉伸、文字生成与事务落库
    /// </summary>
    public class NoteService : INoteRepository
    {
        private static readonly NoteService _instance = new NoteService();
        public static NoteService Instance => _instance;

        public List<NoteRecord> GetAllNotes(Database db)
        {
            if (db == null) return new List<NoteRecord>();

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var notes = XRecordHelper.ReadAllNotes(db, tr);
                return notes;
            }
        }

        public NoteRecord GetNote(Database db, string noteId)
        {
            if (db == null || string.IsNullOrEmpty(noteId)) return null;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var note = XRecordHelper.ReadNote(db, tr, noteId);
                return note;
            }
        }

        public int GetNoteCount(Database db)
        {
            return GetAllNotes(db).Count;
        }

        public bool SaveNote(Database db, NoteRecord note)
        {
            if (db == null || note == null) return false;

            var docState = ReactorManager.Instance.GetState(db);
            using (docState?.EnterInternalScope())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    XRecordHelper.SaveNoteSlices(db, tr, note);
                    tr.Commit();
                    return true;
                }
            }
        }

        public bool DeleteNote(Database db, string noteId)
        {
            if (db == null || string.IsNullOrEmpty(noteId)) return false;

            var docState = ReactorManager.Instance.GetState(db);
            using (docState?.EnterInternalScope())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    NoteRecord note = XRecordHelper.ReadNote(db, tr, noteId);
                    if (note != null)
                    {
                        // 1. 删除图元
                        EraseNoteEntities(db, tr, note);
                        // 2. 移除 XRecord 分片
                        XRecordHelper.DeleteNoteSlices(db, tr, noteId);
                    }
                    tr.Commit();
                    return true;
                }
            }
        }

        /// <summary>
        /// 创建并绘制单云线框批注组合（向后兼容重载）
        /// </summary>
        public NoteRecord CreateCloudNote(
            Database db,
            string title,
            string content,
            string discipline,
            NotePriority priority,
            Point3d pt1,
            Point3d pt2,
            Point3d textPt,
            string assignee = null)
        {
            return CreateCloudNote(db, title, content, discipline, priority, new List<CloudRect> { new CloudRect(pt1, pt2) }, textPt, assignee);
        }

        /// <summary>
        /// 创建并绘制多云线框批注组合（多云线 + 多分枝引线 + MText），提交事务并持久化
        /// </summary>
        public NoteRecord CreateCloudNote(
            Database db,
            string title,
            string content,
            string discipline,
            NotePriority priority,
            List<CloudRect> cloudRects,
            Point3d textPt,
            string assignee = null,
            bool isSimpleMode = false,
            int? manualSeq = null)
        {
            if (db == null || cloudRects == null || cloudRects.Count == 0) return null;

            var docState = ReactorManager.Instance.GetState(db);
            using (docState?.EnterInternalScope())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    // 1. 获取配置与图层
                    NoteSettings settings = ConfigManager.Instance.CurrentSettings;
                    string cloudLayer = !string.IsNullOrEmpty(settings.CloudLayer) ? settings.CloudLayer : "CAD_NOTE_CLOUD";
                    string textLayer = !string.IsNullOrEmpty(settings.TextLayer) ? settings.TextLayer : "CAD_NOTE_TEXT";
                    short colorIndex = settings.EnablePriorityColorLink 
                        ? (short)settings.GetPriorityColorIndex(priority) 
                        : NoteLifecycleStateExtensions.GetColorIndex(NoteLifecycleState.Pending);
                    short textColorIndex = settings.EnablePriorityColorLink 
                        ? colorIndex 
                        : (short)settings.TextColorIndex;
                    short triColor = settings.EnablePriorityColorLink 
                        ? colorIndex 
                        : (short)settings.CloudColorIndex;
                    ValidationService.EnsureNoteLayers(db, tr, settings, priority);
                    XDataHelper.Instance.EnsureRegApp(db, tr, AppConstants.XDATA_APP_NAME);

                    // 2. 计算比例
                    double drawingScale = ScaleHelper.GetDrawingScale(db);
                    double minArc = Math.Max(1.0, settings.MinArcLength);
                    double arcLength = Math.Max(minArc, settings.CloudArcLengthFactor * drawingScale);
                    if (settings.MaxArcLengthRatio > 0)
                    {
                        double maxArc = settings.MaxArcLengthRatio * drawingScale;
                        if (arcLength > maxArc) arcLength = maxArc;
                    }
                    double textHeight = Math.Max(settings.DefaultTextHeight * drawingScale, 2.5);

                    // 3. 计算所有云线矩形的聚合包络
                    double totalMinX = double.MaxValue, totalMinY = double.MaxValue;
                    double totalMaxX = double.MinValue, totalMaxY = double.MinValue;
                    foreach (var rect in cloudRects)
                    {
                        totalMinX = Math.Min(totalMinX, Math.Min(rect.Pt1.X, rect.Pt2.X));
                        totalMinY = Math.Min(totalMinY, Math.Min(rect.Pt1.Y, rect.Pt2.Y));
                        totalMaxX = Math.Max(totalMaxX, Math.Max(rect.Pt1.X, rect.Pt2.X));
                        totalMaxY = Math.Max(totalMaxY, Math.Max(rect.Pt1.Y, rect.Pt2.Y));
                    }

                    int nextSeq = (manualSeq.HasValue && manualSeq.Value > 0) ? manualSeq.Value : GetNextSequenceNumber(db, tr);
                    string noteId = Guid.NewGuid().ToString("N");
                    DateTime now = DateTime.Now;

                    NoteRecord note = new NoteRecord
                    {
                        NoteId = noteId,
                        SequenceNumber = nextSeq,
                        Title = title,
                        Content = content,
                        State = NoteLifecycleState.Pending,
                        Discipline = discipline ?? settings.DefaultDiscipline,
                        Priority = priority,
                        CreatedBy = Environment.UserName,
                        Assignee = assignee ?? settings.DefaultAssignee,
                        CreatedTime = now,
                        ModifiedTime = now,
                        TemplateId = settings.CurrentTemplateId ?? "tpl_general",
                        Cloud = new CloudEntity
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            ShapeType = CloudShapeType.Rectangle,
                            ArcLength = arcLength,
                            MinPoint = new Point3d(totalMinX, totalMinY, 0),
                            MaxPoint = new Point3d(totalMaxX, totalMaxY, 0)
                        },
                        Text = new TextEntity
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            TextHeight = textHeight,
                            InsertionPoint = textPt,
                            FormattedContent = content,
                            LeaderType = settings.LeaderType
                        }
                    };

                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                    double cloudBulge = Math.Abs(settings.BulgeCurvature > 0.05 ? settings.BulgeCurvature : 0.520567);
                    double cloudWidth = settings.CloudWidth > 0 ? settings.CloudWidth * drawingScale : 0.0;
                    int cloudStyle = settings.CloudStyle;

                    // 4. 依次绘制每个矩形云线 (归属云线图层)
                    List<Extents3d> rectBoxes = new List<Extents3d>();
                    foreach (var rect in cloudRects)
                    {
                        Point3d rMin = new Point3d(Math.Min(rect.Pt1.X, rect.Pt2.X), Math.Min(rect.Pt1.Y, rect.Pt2.Y), 0);
                        Point3d rMax = new Point3d(Math.Max(rect.Pt1.X, rect.Pt2.X), Math.Max(rect.Pt1.Y, rect.Pt2.Y), 0);
                        rectBoxes.Add(new Extents3d(rMin, rMax));

                        Polyline cloudPoly = CreateRectangularCloud(rMin, rMax, arcLength, cloudLayer, colorIndex, cloudBulge, cloudWidth, cloudStyle);
                        ObjectId cloudId = btr.AppendEntity(cloudPoly);
                        tr.AddNewlyCreatedDBObject(cloudPoly, true);
                        XDataHelper.Instance.SetNoteId(cloudPoly, AppConstants.XDATA_APP_NAME, noteId);
                        note.Cloud.EntityHandles.Add(cloudId.Handle.ToString());
                    }

                    // 5. 绘制文字与徽标 (归属文字图层，文字颜色遵循 TextColorIndex)
                    MText mtext = new MText
                    {
                        TextHeight = textHeight,
                        ColorIndex = textColorIndex
                    };
                    if (!string.IsNullOrEmpty(textLayer))
                    {
                        try { mtext.Layer = textLayer; } catch { try { mtext.Layer = "0"; } catch { } }
                    }

                    double triH = Math.Max(2.0, textHeight) * 1.8;
                    double triW = triH / 0.866025;
                    Point3d primaryEdgePt = rectBoxes.Count > 0 ? GetClosestPointOnCloud(rectBoxes[0].MinPoint, rectBoxes[0].MaxPoint, textPt) : textPt;
                    bool isLeft = textPt.X < primaryEdgePt.X;

                    // 统一工程批注图面样式：引线转折点放置品红等边小三角徽标，引线带水平托线，上方标题，下方规范日期戳与详情
                    string rawTitle = !string.IsNullOrEmpty(note.Title) ? note.Title : (!string.IsNullOrEmpty(note.Content) ? note.Content : GetDefaultSimpleTitle());
                    int seqForText = isSimpleMode ? 0 : note.SequenceNumber;
                    mtext.Contents = FormatNoteContents(seqForText, rawTitle, note.Content, note.CreatedTime, isSimpleMode);
                    mtext.LineSpacingStyle = LineSpacingStyle.AtLeast;
                    mtext.LineSpacingFactor = 1.35;

                    double textGap = textHeight * 0.4;
                    double contentWidth = CalculateNoteContentWidth(rawTitle, textHeight, seqForText, isSimpleMode, note.Content);

                    bool hasLine2 = isSimpleMode || !string.IsNullOrEmpty(note.Content);
                    if (hasLine2)
                    {
                        mtext.Attachment = AttachmentPoint.MiddleLeft;
                        mtext.Location = isLeft 
                            ? new Point3d(textPt.X - (triW / 2.0) - textGap - contentWidth, textPt.Y, 0) 
                            : new Point3d(textPt.X + (triW / 2.0) + textGap, textPt.Y, 0);
                    }
                    else
                    {
                        // 审查意见为空的单行批注：以 BottomLeft 附着且基准线位于托线上方 0.18h，托线稳固托住文字底边
                        mtext.Attachment = AttachmentPoint.BottomLeft;
                        mtext.Location = isLeft 
                            ? new Point3d(textPt.X - (triW / 2.0) - textGap - contentWidth, textPt.Y + textHeight * 0.18, 0) 
                            : new Point3d(textPt.X + (triW / 2.0) + textGap, textPt.Y + textHeight * 0.18, 0);
                    }

                    Polyline tri = CreateSimpleNoteTriangle(textPt, textHeight, textLayer, triColor);
                    ObjectId triId = btr.AppendEntity(tri);
                    tr.AddNewlyCreatedDBObject(tri, true);
                    XDataHelper.Instance.SetNoteId(tri, AppConstants.XDATA_APP_NAME, noteId);
                    note.Text.FrameHandle = triId.Handle.ToString();

                    if (!string.IsNullOrEmpty(settings.TextStyleName) && !settings.TextStyleName.Equals("Standard", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            TextStyleTable tst = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                            if (tst.Has(settings.TextStyleName))
                            {
#if CAD_R17
                                mtext.TextStyle = tst[settings.TextStyleName];
#else
                                mtext.TextStyleId = tst[settings.TextStyleName];
#endif
                            }
                        }
                        catch { }
                    }

                    ObjectId mtextId = btr.AppendEntity(mtext);
                    tr.AddNewlyCreatedDBObject(mtext, true);
                    XDataHelper.Instance.SetNoteId(mtext, AppConstants.XDATA_APP_NAME, noteId);
                    note.Text.MTextHandle = mtextId.Handle.ToString();

                    // 6. 为每个云线框绘制指向文字/徽标中心的分枝引线（归属文字图层，托线唯一且方向由文字方位决定，折点与小三角重心精准对齐）
                    Point3d kneePt = textPt;

                    for (int bIdx = 0; bIdx < rectBoxes.Count; bIdx++)
                    {
                        var box = rectBoxes[bIdx];
                        Point3d cloudEdgePt = GetClosestPointOnCloud(box.MinPoint, box.MaxPoint, kneePt);
                        Point3d? landingEnd = null;
                        if (bIdx == 0)
                        {
                            double landingLength = CalculateSimpleNoteLandingLength(rawTitle, textHeight, seqForText, isSimpleMode, note.Content);
                            landingEnd = isLeft ? new Point3d(textPt.X - landingLength, textPt.Y, 0) : new Point3d(textPt.X + landingLength, textPt.Y, 0);
                        }
                        Polyline leaderLine = CreateLeaderEntity(cloudEdgePt, kneePt, textLayer, colorIndex, settings.LeaderType, settings.ArrowSizeRatio * drawingScale, landingEnd);
                        if (leaderLine != null)
                        {
                            ObjectId leaderId = btr.AppendEntity(leaderLine);
                            tr.AddNewlyCreatedDBObject(leaderLine, true);
                            XDataHelper.Instance.SetNoteId(leaderLine, AppConstants.XDATA_APP_NAME, noteId);
                            note.Text.LeaderHandles.Add(leaderId.Handle.ToString());
                        }
                    }

                    // 7. 写入初始审计链
                    AuditEntry initAudit = AuditService.CreateEntry(string.Empty, "CREATE", "创建批注", Environment.UserName);
                    note.AuditLogs.Add(initAudit);

                    // 8. 持久化至 XRecord
                    XRecordHelper.SaveNoteSlices(db, tr, note);

                    tr.Commit();
                    return note;
                }
            }
        }

        /// <summary>
        /// 向指定已有批注追加新的云线框与引线
        /// </summary>
        public bool AppendCloudToNote(Database db, string noteId, Point3d pt1, Point3d pt2)
        {
            if (db == null || string.IsNullOrEmpty(noteId)) return false;

            var settings = ConfigManager.Instance.CurrentSettings;
            var docState = ReactorManager.Instance.GetState(db);

            using (docState?.EnterInternalScope())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    NoteRecord note = XRecordHelper.ReadNote(db, tr, noteId);
                    if (note == null) return false;

                    string cloudLayer = !string.IsNullOrEmpty(settings.CloudLayer) ? settings.CloudLayer : "CAD_NOTE_CLOUD";
                    string textLayer = !string.IsNullOrEmpty(settings.TextLayer) ? settings.TextLayer : "CAD_NOTE_TEXT";
                    short colorIndex = settings.EnablePriorityColorLink 
                        ? (short)settings.GetPriorityColorIndex(note.Priority) 
                        : (short)settings.CloudColorIndex;
                    ValidationService.EnsureNoteLayers(db, tr, settings, note.Priority);
                    XDataHelper.Instance.EnsureRegApp(db, tr, AppConstants.XDATA_APP_NAME);

                    double drawingScale = ScaleHelper.GetDrawingScale(db);
                    double minArc = Math.Max(1.0, settings.MinArcLength);
                    double arcLength = Math.Max(minArc, settings.CloudArcLengthFactor * drawingScale);
                    if (settings.MaxArcLengthRatio > 0)
                    {
                        double maxArc = settings.MaxArcLengthRatio * drawingScale;
                        if (arcLength > maxArc) arcLength = maxArc;
                    }

                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                    Point3d rMin = new Point3d(Math.Min(pt1.X, pt2.X), Math.Min(pt1.Y, pt2.Y), 0);
                    Point3d rMax = new Point3d(Math.Max(pt1.X, pt2.X), Math.Max(pt1.Y, pt2.Y), 0);

                    // 1. 创建新云线多段线 (归属云线图层)
                    double cloudBulge = Math.Abs(settings.BulgeCurvature > 0.05 ? settings.BulgeCurvature : 0.520567);
                    double cloudWidth = settings.CloudWidth > 0 ? settings.CloudWidth * drawingScale : 0.0;
                    int cloudStyle = settings.CloudStyle;
                    Polyline cloudPoly = CreateRectangularCloud(rMin, rMax, arcLength, cloudLayer, colorIndex, cloudBulge, cloudWidth, cloudStyle);
                    ObjectId cloudId = btr.AppendEntity(cloudPoly);
                    tr.AddNewlyCreatedDBObject(cloudPoly, true);
                    XDataHelper.Instance.SetNoteId(cloudPoly, AppConstants.XDATA_APP_NAME, noteId);
                    if (note.Cloud.EntityHandles == null) note.Cloud.EntityHandles = new List<string>();
                    note.Cloud.EntityHandles.Add(cloudId.Handle.ToString());

                    // 2. 扩展全局云线范围
                    note.Cloud.MinPoint = new Point3d(Math.Min(note.Cloud.MinPoint.X, rMin.X), Math.Min(note.Cloud.MinPoint.Y, rMin.Y), 0);
                    note.Cloud.MaxPoint = new Point3d(Math.Max(note.Cloud.MaxPoint.X, rMax.X), Math.Max(note.Cloud.MaxPoint.Y, rMax.Y), 0);

                    // 3. 生成连接至文字的分枝引线（归属文字图层，汇交至三角形外边缘，无额外托线）
                    Point3d textPt = note.Text != null ? note.Text.InsertionPoint : Point3d.Origin;
                    bool isSimple = !string.IsNullOrEmpty(note.Text?.FrameHandle);
                    double textHeight = note.Text != null && note.Text.TextHeight > 0 ? note.Text.TextHeight : Math.Max(2.5 * drawingScale, 2.5);
                    double triH = Math.Max(2.0, textHeight) * 1.8;
                    double triW = triH / 0.866025;
                    Point3d approxCenter = new Point3d((note.Cloud.MinPoint.X + note.Cloud.MaxPoint.X) / 2.0, (note.Cloud.MinPoint.Y + note.Cloud.MaxPoint.Y) / 2.0, 0);
                    Point3d kneePt = textPt;

                    Point3d cloudEdgePt = GetClosestPointOnCloud(rMin, rMax, kneePt);

                    Polyline leaderLine = CreateLeaderEntity(cloudEdgePt, kneePt, textLayer, colorIndex, settings.LeaderType, settings.ArrowSizeRatio * drawingScale, null);
                    if (leaderLine != null)
                    {
                        ObjectId leaderId = btr.AppendEntity(leaderLine);
                        tr.AddNewlyCreatedDBObject(leaderLine, true);
                        XDataHelper.Instance.SetNoteId(leaderLine, AppConstants.XDATA_APP_NAME, noteId);
                        if (note.Text.LeaderHandles == null) note.Text.LeaderHandles = new List<string>();
                        note.Text.LeaderHandles.Add(leaderId.Handle.ToString());
                    }

                    // 4. 追加审计日志
                    string prevHash = note.AuditLogs.Count > 0 ? note.AuditLogs[note.AuditLogs.Count - 1].CurrentHash : string.Empty;
                    AuditEntry audit = AuditService.CreateEntry(prevHash, "APPEND_CLOUD", "追加云线框图元", Environment.UserName);
                    note.AuditLogs.Add(audit);

                    note.ModifiedTime = DateTime.Now;
                    XRecordHelper.SaveNoteSlices(db, tr, note);

                    tr.Commit();
                    return true;
                }
            }
        }

        /// <summary>
        /// 基于已转换或自定义的多段线云线创建完整批注组合（云线 + 引线 + MText），提交事务并持久化
        /// </summary>
        public NoteRecord CreateCloudNoteFromPolyline(
            Database db,
            Polyline cloudPoly,
            string title,
            string content,
            string discipline,
            NotePriority priority,
            Point3d textPt,
            string assignee = null,
            bool isSimpleMode = false,
            int? manualSeq = null)
        {
            if (db == null || cloudPoly == null) return null;

            var docState = ReactorManager.Instance.GetState(db);
            using (docState?.EnterInternalScope())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    NoteSettings settings = ConfigManager.Instance.CurrentSettings;
                    string cloudLayer = !string.IsNullOrEmpty(settings.CloudLayer) ? settings.CloudLayer : "CAD_NOTE_CLOUD";
                    string textLayer = !string.IsNullOrEmpty(settings.TextLayer) ? settings.TextLayer : "CAD_NOTE_TEXT";
                    short colorIndex = settings.EnablePriorityColorLink 
                        ? (short)settings.GetPriorityColorIndex(priority) 
                        : NoteLifecycleStateExtensions.GetColorIndex(NoteLifecycleState.Pending);
                    short textColorIndex = settings.EnablePriorityColorLink 
                        ? colorIndex 
                        : (short)settings.TextColorIndex;
                    short triColor = settings.EnablePriorityColorLink 
                        ? colorIndex 
                        : (short)settings.CloudColorIndex;
                    ValidationService.EnsureNoteLayers(db, tr, settings, priority);
                    XDataHelper.Instance.EnsureRegApp(db, tr, AppConstants.XDATA_APP_NAME);

                    double drawingScale = ScaleHelper.GetDrawingScale(db);
                    double textHeight = Math.Max(settings.DefaultTextHeight * drawingScale, 2.5);

                    // 计算云线包围盒
                    Extents3d ext = CurveToCloudService.GetPolylineExtents(cloudPoly);
                    Point3d minPt = ext.MinPoint;
                    Point3d maxPt = ext.MaxPoint;

                    int nextSeq = (manualSeq.HasValue && manualSeq.Value > 0) ? manualSeq.Value : GetNextSequenceNumber(db, tr);
                    string noteId = Guid.NewGuid().ToString("N");
                    DateTime now = DateTime.Now;

                    NoteRecord note = new NoteRecord
                    {
                        NoteId = noteId,
                        SequenceNumber = nextSeq,
                        Title = title,
                        Content = content,
                        State = NoteLifecycleState.Pending,
                        Discipline = discipline ?? settings.DefaultDiscipline,
                        Priority = priority,
                        CreatedBy = Environment.UserName,
                        Assignee = assignee ?? settings.DefaultAssignee,
                        CreatedTime = now,
                        ModifiedTime = now,
                        TemplateId = settings.CurrentTemplateId ?? "tpl_general",
                        Cloud = new CloudEntity
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            ShapeType = CloudShapeType.Converted,
                            ArcLength = Math.Max(1.0, settings.CloudArcLengthFactor * drawingScale),
                            MinPoint = minPt,
                            MaxPoint = maxPt
                        },
                        Text = new TextEntity
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            TextHeight = textHeight,
                            InsertionPoint = textPt,
                            FormattedContent = content,
                            LeaderType = settings.LeaderType
                        }
                    };

                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                    // 1. 将 Polyline 附加到图纸并关联 XData (归属云线图层)
                    if (!string.IsNullOrEmpty(cloudLayer))
                    {
                        try { cloudPoly.Layer = cloudLayer; } catch { try { cloudPoly.Layer = "0"; } catch { } }
                    }
                    cloudPoly.ColorIndex = colorIndex;
                    ObjectId cloudId = btr.AppendEntity(cloudPoly);
                    tr.AddNewlyCreatedDBObject(cloudPoly, true);
                    XDataHelper.Instance.SetNoteId(cloudPoly, AppConstants.XDATA_APP_NAME, noteId);
                    note.Cloud.EntityHandles.Add(cloudId.Handle.ToString());

                    // 2. 绘制文字与徽标 (归属文字图层，文字颜色遵循 TextColorIndex)
                    MText mtext = new MText
                    {
                        TextHeight = textHeight,
                        ColorIndex = textColorIndex
                    };
                    if (!string.IsNullOrEmpty(textLayer))
                    {
                        try { mtext.Layer = textLayer; } catch { try { mtext.Layer = "0"; } catch { } }
                    }

                    double triH = Math.Max(2.0, textHeight) * 1.8;
                    double triW = triH / 0.866025;
                    Point3d cloudEdgePtPrimary = cloudPoly.GetClosestPointTo(textPt, false);
                    bool isLeft = textPt.X < cloudEdgePtPrimary.X;

                    // 统一工程批注图面样式：引线转折点放置品红等边小三角徽标，引线带水平托线，上方标题，下方规范日期戳与详情
                    string rawTitle = !string.IsNullOrEmpty(note.Title) ? note.Title : (!string.IsNullOrEmpty(note.Content) ? note.Content : GetDefaultSimpleTitle());
                    int seqForText = isSimpleMode ? 0 : note.SequenceNumber;
                    mtext.Contents = FormatNoteContents(seqForText, rawTitle, note.Content, note.CreatedTime, isSimpleMode);
                    mtext.LineSpacingStyle = LineSpacingStyle.AtLeast;
                    mtext.LineSpacingFactor = 1.35;

                    double textGap = textHeight * 0.4;
                    double contentWidth = CalculateNoteContentWidth(rawTitle, textHeight, seqForText, isSimpleMode, note.Content);

                    bool hasLine2 = isSimpleMode || !string.IsNullOrEmpty(note.Content);
                    if (hasLine2)
                    {
                        mtext.Attachment = AttachmentPoint.MiddleLeft;
                        mtext.Location = isLeft 
                            ? new Point3d(textPt.X - (triW / 2.0) - textGap - contentWidth, textPt.Y, 0) 
                            : new Point3d(textPt.X + (triW / 2.0) + textGap, textPt.Y, 0);
                    }
                    else
                    {
                        // 审查意见为空的单行批注：以 BottomLeft 附着且基准线位于托线上方 0.18h，托线稳固托住文字底边
                        mtext.Attachment = AttachmentPoint.BottomLeft;
                        mtext.Location = isLeft 
                            ? new Point3d(textPt.X - (triW / 2.0) - textGap - contentWidth, textPt.Y + textHeight * 0.18, 0) 
                            : new Point3d(textPt.X + (triW / 2.0) + textGap, textPt.Y + textHeight * 0.18, 0);
                    }

                    Polyline tri = CreateSimpleNoteTriangle(textPt, textHeight, textLayer, triColor);
                    ObjectId triId = btr.AppendEntity(tri);
                    tr.AddNewlyCreatedDBObject(tri, true);
                    XDataHelper.Instance.SetNoteId(tri, AppConstants.XDATA_APP_NAME, noteId);
                    note.Text.FrameHandle = triId.Handle.ToString();

                    if (!string.IsNullOrEmpty(settings.TextStyleName) && !settings.TextStyleName.Equals("Standard", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            TextStyleTable tst = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                            if (tst.Has(settings.TextStyleName))
                            {
#if CAD_R17
                                mtext.TextStyle = tst[settings.TextStyleName];
#else
                                mtext.TextStyleId = tst[settings.TextStyleName];
#endif
                            }
                        }
                        catch { }
                    }

                    ObjectId mtextId = btr.AppendEntity(mtext);
                    tr.AddNewlyCreatedDBObject(mtext, true);
                    XDataHelper.Instance.SetNoteId(mtext, AppConstants.XDATA_APP_NAME, noteId);
                    note.Text.MTextHandle = mtextId.Handle.ToString();

                    Point3d kneePt = textPt;

                    Point3d cloudEdgePt = cloudPoly.GetClosestPointTo(kneePt, false);
                    double landingLength = CalculateSimpleNoteLandingLength(rawTitle, textHeight, seqForText, isSimpleMode, note.Content);
                    Point3d? landingEnd = isLeft ? new Point3d(textPt.X - landingLength, textPt.Y, 0) : new Point3d(textPt.X + landingLength, textPt.Y, 0);
                    Polyline leaderLine = CreateLeaderEntity(cloudEdgePt, kneePt, textLayer, colorIndex, settings.LeaderType, settings.ArrowSizeRatio * drawingScale, landingEnd);
                    if (leaderLine != null)
                    {
                        ObjectId leaderId = btr.AppendEntity(leaderLine);
                        tr.AddNewlyCreatedDBObject(leaderLine, true);
                        XDataHelper.Instance.SetNoteId(leaderLine, AppConstants.XDATA_APP_NAME, noteId);
                        note.Text.LeaderHandles.Add(leaderId.Handle.ToString());
                    }

                    // 4. 写入审计链
                    AuditEntry initAudit = AuditService.CreateEntry(string.Empty, "CONVERT", "对象转云线创建批注", Environment.UserName);
                    note.AuditLogs.Add(initAudit);

                    // 5. 持久化至 XRecord
                    XRecordHelper.SaveNoteSlices(db, tr, note);

                    tr.Commit();
                    return note;
                }
            }
        }

        /// <summary>
        /// 向指定已有批注追加已转换的云线多段线与引线
        /// </summary>
        public bool AppendPolylineToNote(Database db, string noteId, Polyline cloudPoly)
        {
            if (db == null || string.IsNullOrEmpty(noteId) || cloudPoly == null) return false;

            var settings = ConfigManager.Instance.CurrentSettings;
            var docState = ReactorManager.Instance.GetState(db);

            using (docState?.EnterInternalScope())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    NoteRecord note = XRecordHelper.ReadNote(db, tr, noteId);
                    if (note == null) return false;

                    string cloudLayer = !string.IsNullOrEmpty(settings.CloudLayer) ? settings.CloudLayer : "CAD_NOTE_CLOUD";
                    string textLayer = !string.IsNullOrEmpty(settings.TextLayer) ? settings.TextLayer : "CAD_NOTE_TEXT";
                    short colorIndex = settings.EnablePriorityColorLink 
                        ? (short)settings.GetPriorityColorIndex(note.Priority) 
                        : (short)settings.CloudColorIndex;
                    ValidationService.EnsureNoteLayers(db, tr, settings, note.Priority);
                    XDataHelper.Instance.EnsureRegApp(db, tr, AppConstants.XDATA_APP_NAME);

                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                    // 1. 添加云线多段线 (归属云线图层)
                    cloudPoly.Layer = cloudLayer;
                    cloudPoly.ColorIndex = colorIndex;
                    ObjectId cloudId = btr.AppendEntity(cloudPoly);
                    tr.AddNewlyCreatedDBObject(cloudPoly, true);
                    XDataHelper.Instance.SetNoteId(cloudPoly, AppConstants.XDATA_APP_NAME, noteId);
                    if (note.Cloud.EntityHandles == null) note.Cloud.EntityHandles = new List<string>();
                    note.Cloud.EntityHandles.Add(cloudId.Handle.ToString());

                    // 2. 扩展全局云线范围
                    Extents3d ext = CurveToCloudService.GetPolylineExtents(cloudPoly);
                    note.Cloud.MinPoint = new Point3d(Math.Min(note.Cloud.MinPoint.X, ext.MinPoint.X), Math.Min(note.Cloud.MinPoint.Y, ext.MinPoint.Y), 0);
                    note.Cloud.MaxPoint = new Point3d(Math.Max(note.Cloud.MaxPoint.X, ext.MaxPoint.X), Math.Max(note.Cloud.MaxPoint.Y, ext.MaxPoint.Y), 0);

                    double drawingScale = ScaleHelper.GetDrawingScale(db);
                    Point3d textPt = note.Text != null ? note.Text.InsertionPoint : Point3d.Origin;
                    bool isSimple = !string.IsNullOrEmpty(note.Text?.FrameHandle);
                    double textHeight = note.Text != null && note.Text.TextHeight > 0 ? note.Text.TextHeight : Math.Max(2.5 * drawingScale, 2.5);
                    double triH = Math.Max(2.0, textHeight) * 1.8;
                    double triW = triH / 0.866025;
                    Point3d approxCenter = new Point3d((note.Cloud.MinPoint.X + note.Cloud.MaxPoint.X) / 2.0, (note.Cloud.MinPoint.Y + note.Cloud.MaxPoint.Y) / 2.0, 0);
                    Point3d kneePt = textPt;

                    Point3d cloudEdgePt = cloudPoly.GetClosestPointTo(kneePt, false);

                    // 3. 生成连接至文字的分枝引线 (归属文字图层)
                    Polyline leaderLine = CreateLeaderEntity(cloudEdgePt, kneePt, textLayer, colorIndex, settings.LeaderType, settings.ArrowSizeRatio * drawingScale, null);
                    if (leaderLine != null)
                    {
                        ObjectId leaderId = btr.AppendEntity(leaderLine);
                        tr.AddNewlyCreatedDBObject(leaderLine, true);
                        XDataHelper.Instance.SetNoteId(leaderLine, AppConstants.XDATA_APP_NAME, noteId);
                        if (note.Text.LeaderHandles == null) note.Text.LeaderHandles = new List<string>();
                        note.Text.LeaderHandles.Add(leaderId.Handle.ToString());
                    }

                    // 4. 追加审计日志
                    string prevHash = note.AuditLogs.Count > 0 ? note.AuditLogs[note.AuditLogs.Count - 1].CurrentHash : string.Empty;
                    AuditEntry audit = AuditService.CreateEntry(prevHash, "APPEND_CLOUD", "追加对象转换云线图元", Environment.UserName);
                    note.AuditLogs.Add(audit);

                    note.ModifiedTime = DateTime.Now;
                    XRecordHelper.SaveNoteSlices(db, tr, note);

                    tr.Commit();
                    return true;
                }
            }
        }

        private static int GetNextSequenceNumber(Database db, Transaction tr)
        {
            var notes = XRecordHelper.ReadAllNotes(db, tr);
            int max = 0;
            foreach (var n in notes)
            {
                if (n.SequenceNumber > max) max = n.SequenceNumber;
            }
            return max + 1;
        }

        public static int GetNextSequenceNumber(Database db)
        {
            if (db == null) return 1;
            try
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    return GetNextSequenceNumber(db, tr);
                }
            }
            catch
            {
                return 1;
            }
        }

        public static string FormatSequencePrefix(string prefix, int seq)
        {
            if (string.IsNullOrEmpty(prefix)) return string.Format("【{0}】", seq);
            prefix = prefix.Trim();
            if (prefix == "【" || prefix == "【】" || prefix == "[" || prefix == "[]")
            {
                return string.Format("【{0}】", seq);
            }
            if (prefix.StartsWith("【") && prefix.EndsWith("】"))
            {
                return prefix.Insert(prefix.Length - 1, seq.ToString());
            }
            if (prefix.EndsWith("-") || prefix.EndsWith("_") || prefix.EndsWith(".") || prefix.EndsWith("#"))
            {
                return string.Format("{0}{1}", prefix, seq);
            }
            return string.Format("{0}{1}", prefix, seq);
        }

        public static Polyline CreateRectangularCloud(Point3d minPt, Point3d maxPt, double arcLength, string layer, short colorIndex, double bulge, double constantWidth, int cloudStyle = 1)
        {
            Polyline poly = new Polyline();
            if (!string.IsNullOrEmpty(layer))
            {
                try { poly.Layer = layer; } catch { try { poly.Layer = "0"; } catch { } }
            }
            poly.ColorIndex = colorIndex;

            double startWidth = 0.0;
            double endWidth = 0.0;

            if (cloudStyle == 1) // 书法样式 (Calligraphy): 变截面笔锋多段线 (对应图 2 效果)
            {
                double strokeWidth = Math.Max(arcLength * 0.18, constantWidth > 0 ? constantWidth * 2.0 : 0.0);
                startWidth = 0.0;
                endWidth = strokeWidth;
            }
            else // 普通样式 (Normal): 全局等宽
            {
                if (constantWidth > 0)
                {
                    poly.ConstantWidth = constantWidth;
                }
            }

            // 顺时针顺序遍历矩形四边 (Top: L->R, Right: T->B, Bottom: R->L, Left: B->T)
            // 在顺时针遍历中，严格使用负凸度 -Math.Abs(bulge) 沿前进方向向外饱满凸出，且起笔细、落笔粗变截面形成书法毛笔折角笔锋 (对齐图 2 效果)
            // 极窄与正交单线退化防护：当宽度或高度过小（如开启正交模式/误选单线）时，自几何中心对称扩张至安全跨度，彻底防止上下/左右圆弧交错重叠形成“麻花辫”
            double minSpan = Math.Max(arcLength * 1.2, 5.0);
            double minX = Math.Min(minPt.X, maxPt.X);
            double maxX = Math.Max(minPt.X, maxPt.X);
            double minY = Math.Min(minPt.Y, maxPt.Y);
            double maxY = Math.Max(minPt.Y, maxPt.Y);

            if (maxX - minX < minSpan)
            {
                double cx = (minX + maxX) * 0.5;
                minX = cx - minSpan * 0.5;
                maxX = cx + minSpan * 0.5;
            }
            if (maxY - minY < minSpan)
            {
                double cy = (minY + maxY) * 0.5;
                minY = cy - minSpan * 0.5;
                maxY = cy + minSpan * 0.5;
            }

            Point2d pTopLeft = new Point2d(minX, maxY);
            Point2d pTopRight = new Point2d(maxX, maxY);
            Point2d pBottomRight = new Point2d(maxX, minY);
            Point2d pBottomLeft = new Point2d(minX, minY);

            double absBulge = Math.Abs(bulge);
            double b = -(absBulge > 0.05 ? absBulge : 0.520567);
            int vIdx = 0;
            AddEdgeArcs(poly, ref vIdx, pTopLeft, pTopRight, arcLength, b, startWidth, endWidth);
            AddEdgeArcs(poly, ref vIdx, pTopRight, pBottomRight, arcLength, b, startWidth, endWidth);
            AddEdgeArcs(poly, ref vIdx, pBottomRight, pBottomLeft, arcLength, b, startWidth, endWidth);
            AddEdgeArcs(poly, ref vIdx, pBottomLeft, pTopLeft, arcLength, b, startWidth, endWidth);

            poly.Closed = true;
            return poly;
        }

        public static Polyline CreatePolygonCloud(IList<Point2d> vertices, double arcLength, string layer, short colorIndex, double bulge, double constantWidth, int cloudStyle = 1)
        {
            if (vertices == null || vertices.Count < 3) return null;

            Polyline poly = new Polyline();
            if (!string.IsNullOrEmpty(layer)) poly.Layer = layer;
            poly.ColorIndex = colorIndex;

            double startWidth = 0.0;
            double endWidth = 0.0;

            if (cloudStyle == 1) // 书法样式 (Calligraphy)
            {
                double strokeWidth = Math.Max(arcLength * 0.18, constantWidth > 0 ? constantWidth * 2.0 : 0.0);
                startWidth = 0.0;
                endWidth = strokeWidth;
            }
            else // 普通样式 (Normal)
            {
                if (constantWidth > 0) poly.ConstantWidth = constantWidth;
            }

            // 计算多边形有向面积判定顶点环绕方向 (Shoelace 公式)
            double signedArea2 = 0.0;
            int n = vertices.Count;
            for (int i = 0; i < n; i++)
            {
                Point2d p1 = vertices[i];
                Point2d p2 = vertices[(i + 1) % n];
                signedArea2 += (p1.X * p2.Y - p2.X * p1.Y);
            }

            // 顺时针(CW, signedArea2 < 0): 外侧在前进方向左侧，需要 Bulge < 0
            // 逆时针(CCW, signedArea2 > 0): 外侧在前进方向右侧，需要 Bulge > 0
            double absBulge = Math.Abs(bulge > 0.05 ? bulge : 0.520567);
            double effectiveBulge = (signedArea2 < 0) ? -absBulge : absBulge;

            int vIdx = 0;
            for (int i = 0; i < n; i++)
            {
                Point2d start = vertices[i];
                Point2d end = vertices[(i + 1) % n];
                AddEdgeArcs(poly, ref vIdx, start, end, arcLength, effectiveBulge, startWidth, endWidth);
            }

            poly.Closed = true;
            return poly;
        }

        private static void AddEdgeArcs(Polyline poly, ref int vIdx, Point2d start, Point2d end, double arcLength, double bulge, double startWidth, double endWidth)
        {
            double dist = start.GetDistanceTo(end);
            int segCount = Math.Max(1, (int)Math.Round(dist / arcLength));
            Vector2d dir = (end - start) / (double)segCount;

            for (int i = 0; i < segCount; i++)
            {
                Point2d pt = start + dir * (double)i;
                poly.AddVertexAt(vIdx++, pt, bulge, startWidth, endWidth);
            }
        }

        public static Point3d GetClosestPointOnCloud(Point3d minPt, Point3d maxPt, Point3d target)
        {
            // 1. 若目标在外部，精确 Clamp 到包围盒四周边界/边角点
            if (target.X < minPt.X || target.X > maxPt.X || target.Y < minPt.Y || target.Y > maxPt.Y)
            {
                double cx = Math.Max(minPt.X, Math.Min(target.X, maxPt.X));
                double cy = Math.Max(minPt.Y, Math.Min(target.Y, maxPt.Y));
                return new Point3d(cx, cy, 0);
            }

            // 2. 若目标在内部，投影到最近的一条边界边上
            double dLeft = target.X - minPt.X;
            double dRight = maxPt.X - target.X;
            double dBottom = target.Y - minPt.Y;
            double dTop = maxPt.Y - target.Y;
            double minD = Math.Min(Math.Min(dLeft, dRight), Math.Min(dBottom, dTop));

            if (minD == dLeft) return new Point3d(minPt.X, target.Y, 0);
            if (minD == dRight) return new Point3d(maxPt.X, target.Y, 0);
            if (minD == dBottom) return new Point3d(target.X, minPt.Y, 0);
            return new Point3d(target.X, maxPt.Y, 0);
        }

        /// <summary>
        /// 获取极简批注默认标题（自动附加当前日期，如 待修改（26年09月17日））
        /// </summary>
        public static string GetDefaultSimpleTitle(string phrase = null)
        {
            if (string.IsNullOrEmpty(phrase))
            {
                phrase = ConfigManager.Instance.CurrentSettings?.RapidDefaultPhrase;
                if (string.IsNullOrEmpty(phrase)) phrase = "待修改";
            }
            string dateStr = DateTime.Now.ToString("yy年MM月dd日");
            return string.Format("{0}（{1}）", phrase, dateStr);
        }

        /// <summary>
        /// 格式化极简批注 MText 文本：
        /// <summary>
        /// <summary>
        /// 提取极简模式两行式批注的上半部分核心标题（去除前缀序号与日期后缀）
        /// </summary>
        public static string ExtractSimpleTitlePart(string rawTitle)
        {
            if (string.IsNullOrEmpty(rawTitle) || string.IsNullOrEmpty(rawTitle.Trim()))
            {
                return "待修改";
            }

            string s = rawTitle.Trim();

            // 若已经带有【序号】前缀（例如【6】或【6】 待修改），剥离前缀序号，只保留核心标题
            if (s.StartsWith("【"))
            {
                int endB = s.IndexOf('】');
                if (endB >= 0)
                {
                    s = s.Substring(endB + 1).Trim();
                }
            }

            // 剥离尾部日期（如（26年09月24日）或 (26年09月24日)）
            // 采用 LastIndexOf，且校验内部确实是日期标记，避免误伤“防火门(乙级)”等工程标题
            int idx1 = s.LastIndexOf('（');
            if (idx1 < 0) idx1 = s.LastIndexOf('(');

            if (idx1 >= 0)
            {
                string afterParen = s.Substring(idx1);
                if (afterParen.Contains("年") || afterParen.Contains("月") || afterParen.Contains("-") || afterParen.Contains("/"))
                {
                    s = s.Substring(0, idx1).Trim();
                }
            }

            if (string.IsNullOrEmpty(s))
            {
                s = "待修改";
            }
            return s;
        }

        /// <summary>
        /// 精确计算批注文字包围盒的实际排版内容宽度 (用于在各个方向自适应托线与文字对齐，杜绝与三角形重叠)
        /// </summary>
        public static double CalculateNoteContentWidth(string rawTitle, double textHeight, int seqNumber = 0, bool isSimpleMode = true, string content = null)
        {
            double safeHeight = Math.Max(2.0, textHeight);
            string titlePart;
            if (!string.IsNullOrEmpty(rawTitle) && !string.IsNullOrEmpty(rawTitle.Trim()))
            {
                titlePart = ExtractSimpleTitlePart(rawTitle);
            }
            else if (!string.IsNullOrEmpty(content) && !string.IsNullOrEmpty(content.Trim()))
            {
                string c = content.Trim();
                int nl = c.IndexOfAny(new[] { '\r', '\n', '。', '；', ';' });
                titlePart = nl > 0 ? c.Substring(0, nl).Trim() : (c.Length > 15 ? c.Substring(0, 15).Trim() : c);
            }
            else
            {
                titlePart = "待修改";
            }

            if (string.IsNullOrEmpty(titlePart))
            {
                titlePart = "待修改";
            }

            if (!isSimpleMode && seqNumber > 0)
            {
                string pfx = ConfigManager.Instance.CurrentSettings.AutoNumberPrefix;
                string seqTag = FormatSequencePrefix(pfx, seqNumber);
                if (titlePart.StartsWith("【") && titlePart.Contains("】"))
                {
                    int cIdx = titlePart.IndexOf('】');
                    titlePart = titlePart.Substring(cIdx + 1).Trim();
                }
                titlePart = string.Format("{0} {1}", seqTag, titlePart);
            }

            // AutoCAD 中宋体字宽测算：汉字约 1.05h，半角字符约 0.65h
            double titleWidth = 0.0;
            foreach (char c in titlePart)
            {
                titleWidth += (c > 127) ? (safeHeight * 1.05) : (safeHeight * 0.65);
            }

            // 计算第二行日期宽度 (MText 中字高因子为 0.75x)
            int idx1 = rawTitle != null ? rawTitle.LastIndexOf('（') : -1;
            if (idx1 < 0 && rawTitle != null) idx1 = rawTitle.LastIndexOf('(');
            string datePart = string.Empty;
            if (idx1 >= 0)
            {
                string afterParen = rawTitle.Substring(idx1).Trim();
                if (afterParen.Contains("年") || afterParen.Contains("月") || afterParen.Contains("-") || afterParen.Contains("/"))
                {
                    datePart = afterParen;
                }
            }
            if (string.IsNullOrEmpty(datePart))
            {
                datePart = string.Format("（{0:yy年MM月dd日}）", DateTime.Now);
            }

            double dateWidth = 0.0;
            double dateHeight = safeHeight * 0.75;
            foreach (char c in datePart)
            {
                dateWidth += (c > 127) ? (dateHeight * 1.05) : (dateHeight * 0.65);
            }

            if (isSimpleMode)
            {
                // 快速批注：上下排布，托线覆盖两者中较宽者
                return Math.Max(titleWidth, dateWidth);
            }
            else
            {
                // 非快速批注：水平排布，托线承托上方第一排（标题 + 间距 + 日期）；
                // 下方第二排为规范条文/详情内容，横向自然展开，上方水平托线与小三角精准贴合第一排标题，彻底消除冗余延伸缺陷
                double spaceWidth = safeHeight * 0.8;
                double line1Width = titleWidth + spaceWidth + dateWidth;
                return line1Width;
            }
        }

        /// <summary>
        /// 测算批注水平托线完整长度 (从三角形转折点开始，完整包裹文字排版并留白)
        /// 1. 快速批注 (isSimpleMode=true)：为上下排布，托线覆盖两者中较宽者并留白 1.2h
        /// 2. 非快速批注 (isSimpleMode=false)：为水平排布（标题与时间同一排），托线承托第一排标题并留白 0.6h 优雅收笔
        /// </summary>
        public static double CalculateSimpleNoteLandingLength(string rawTitle, double textHeight, int seqNumber = 0, bool isSimpleMode = true, string content = null)
        {
            double safeHeight = Math.Max(2.0, textHeight);
            double triH = safeHeight * 1.8;
            double triW = triH / 0.866025;
            double textGap = safeHeight * 0.4;
            double contentWidth = CalculateNoteContentWidth(rawTitle, textHeight, seqNumber, isSimpleMode, content);

            // 制图标准：水平托线必须充分覆盖第一排文字并向外优雅收笔 (快速批注留白约1.2h，非快速批注留白约0.6h)
            double margin = isSimpleMode ? (1.2 * safeHeight) : (0.6 * safeHeight);
            return (triW / 2.0) + textGap + contentWidth + margin;
        }

        /// <summary>
        /// 格式化批注图面 MText 内容：
        /// 1. 快速批注 (isSimpleMode=true)：为上下排布（横线上方为粗体标题，横线下方为较小字号日期戳）
        /// 2. 非快速批注 (isSimpleMode=false)：为水平排布（横线上方为【序号】粗体标题 + 较小字号日期戳在同一排），横线下方为审查详情/依据规范
        /// </summary>
        public static string FormatNoteContents(int seqNumber, string rawTitle, string content, DateTime? createTime = null, bool isSimpleMode = false)
        {
            string titlePart;
            if (!string.IsNullOrEmpty(rawTitle) && !string.IsNullOrEmpty(rawTitle.Trim()))
            {
                titlePart = ExtractSimpleTitlePart(rawTitle);
            }
            else if (!string.IsNullOrEmpty(content) && !string.IsNullOrEmpty(content.Trim()))
            {
                string c = content.Trim();
                int nl = c.IndexOfAny(new[] { '\r', '\n', '。', '；', ';' });
                titlePart = nl > 0 ? c.Substring(0, nl).Trim() : (c.Length > 15 ? c.Substring(0, 15).Trim() : c);
            }
            else
            {
                titlePart = "待修改";
            }

            if (string.IsNullOrEmpty(titlePart))
            {
                titlePart = "待修改";
            }

            string datePart = string.Empty;
            int idx1 = rawTitle != null ? rawTitle.LastIndexOf('（') : -1;
            if (idx1 < 0 && rawTitle != null) idx1 = rawTitle.LastIndexOf('(');

            if (idx1 >= 0)
            {
                string afterParen = rawTitle.Substring(idx1).Trim();
                if (afterParen.Contains("年") || afterParen.Contains("月") || afterParen.Contains("-") || afterParen.Contains("/"))
                {
                    datePart = afterParen;
                }
            }

            if (string.IsNullOrEmpty(datePart))
            {
                DateTime dt = createTime.HasValue && createTime.Value > DateTime.MinValue ? createTime.Value : DateTime.Now;
                datePart = string.Format("（{0:yy年MM月dd日}）", dt);
            }

            if (isSimpleMode)
            {
                // 快速批注：为上下排布
                string line1 = string.Format("{{\\fSimSun|b1|i0;{0}}}", titlePart);
                string line2 = string.Format("{{\\H0.75x;{0}}}", datePart);
                return string.Format("\\A1;{0}\\P{1}", line1, line2);
            }
            else
            {
                // 非快速批注：为水平排布（标题与时间同一排，字体样式和字号大小区别）
                if (seqNumber > 0)
                {
                    string pfx = ConfigManager.Instance.CurrentSettings.AutoNumberPrefix;
                    string seqTag = FormatSequencePrefix(pfx, seqNumber);
                    if (titlePart.StartsWith("【") && titlePart.Contains("】"))
                    {
                        int cIdx = titlePart.IndexOf('】');
                        titlePart = titlePart.Substring(cIdx + 1).Trim();
                    }
                    titlePart = string.Format("{0} {1}", seqTag, titlePart);
                }

                string line1 = string.Format("{{\\fSimSun|b1|i0;{0}}}  {{\\H0.75x;{1}}}", titlePart, datePart);
                string cleanContent = !string.IsNullOrEmpty(content) ? content.Trim() : string.Empty;

                if (!string.IsNullOrEmpty(cleanContent))
                {
                    // 横线下方：规范、引用、审查详情等
                    return string.Format("\\A1;{0}\\P{1}", line1, cleanContent);
                }
                else
                {
                    return string.Format("\\A1;{0}", line1);
                }
            }
        }

        /// <summary>
        /// 格式化极简模式两行式文本（MText Contents）
        /// </summary>
        public static string FormatSimpleNoteContents(string rawTitle)
        {
            return FormatNoteContents(0, rawTitle, string.Empty, null, true);
        }

        public static string FormatSimpleNoteContents(int seq, string rawTitle)
        {
            return FormatNoteContents(seq, rawTitle, string.Empty, null, true);
        }

        /// <summary>
        /// 绘制极简批注模式2标准等边小三角徽标（节点小三角，尺寸由字高确定，重心位于引线转折点）
        /// </summary>
        public static Polyline CreateSimpleNoteTriangle(Point3d kneePt, double textHeight, string layerName, short frameColor = 6)
        {
            double safeTextHeight = Math.Max(2.0, textHeight);
            double height = safeTextHeight * 1.8;
            double baseWidth = height / 0.866025; // 正等边比例: H = sqrt(3)/2 * W => W = H / 0.866025

            // 等边小三角几何重心（Centroid）精确重合于引线转折点 (kneePt.X, kneePt.Y)
            // 顶点朝上: Y + height * (2/3)
            // 底边水平: Y - height * (1/3)
            // 水平托线正中穿过小三角几何重心，图面左右文字通过 textGap 完全避让
            Point2d pApex = new Point2d(kneePt.X, kneePt.Y + height * (2.0 / 3.0));
            Point2d pLeft = new Point2d(kneePt.X - baseWidth / 2.0, kneePt.Y - height * (1.0 / 3.0));
            Point2d pRight = new Point2d(kneePt.X + baseWidth / 2.0, kneePt.Y - height * (1.0 / 3.0));

            Polyline tri = new Polyline();
            tri.AddVertexAt(0, pApex, 0, 0, 0);
            tri.AddVertexAt(1, pLeft, 0, 0, 0);
            tri.AddVertexAt(2, pRight, 0, 0, 0);
            tri.Closed = true;
            if (!string.IsNullOrEmpty(layerName))
            {
                try { tri.Layer = layerName; } catch { try { tri.Layer = "0"; } catch { } }
            }
            tri.ColorIndex = frameColor;
            return tri;
        }

        /// <summary>
        /// 根据引线模式绘制引线 (0=带箭头引线, 1=点引线, 2=无引线独立放置)
        /// 支持在转折点 targetPt 后追加水平托线端点 landingEndPt
        /// </summary>
        public static Polyline CreateLeaderEntity(
            Point3d cloudEdgePt,
            Point3d targetPt,
            string layerName,
            short colorIndex,
            int leaderType,
            double arrowSize,
            Point3d? landingEndPt = null)
        {
            if (leaderType == 2) return null; // 2 = 无引线独立放置

            double dist = cloudEdgePt.DistanceTo(targetPt);
            if (dist < 1e-4) return null;

            Polyline leaderLine = new Polyline();
            if (!string.IsNullOrEmpty(layerName))
            {
                try { leaderLine.Layer = layerName; } catch { try { leaderLine.Layer = "0"; } catch { } }
            }
            leaderLine.ColorIndex = colorIndex;

            Vector3d v = targetPt - cloudEdgePt;
            Vector3d u = v / dist;

            if (leaderType == 0)
            {
                // 0 = 带箭头引线
                double arrowLen = Math.Max(2.0, arrowSize);
                double arrowWidth = arrowLen / 3.0;

                if (dist <= arrowLen)
                {
                    leaderLine.AddVertexAt(0, new Point2d(cloudEdgePt.X, cloudEdgePt.Y), 0, 0, dist / 3.0);
                    leaderLine.AddVertexAt(1, new Point2d(targetPt.X, targetPt.Y), 0, 0, 0);
                }
                else
                {
                    Point3d arrowBase = cloudEdgePt + u * arrowLen;
                    leaderLine.AddVertexAt(0, new Point2d(cloudEdgePt.X, cloudEdgePt.Y), 0, 0, arrowWidth);
                    leaderLine.AddVertexAt(1, new Point2d(arrowBase.X, arrowBase.Y), 0, 0, 0);
                    leaderLine.AddVertexAt(2, new Point2d(targetPt.X, targetPt.Y), 0, 0, 0);
                }
            }
            else if (leaderType == 1)
            {
                // 1 = 点引线 (Donut 实心圆点 + 引线)
                double dotRadius = Math.Max(0.75, arrowSize * 0.25);
                if (dist <= dotRadius * 2.0)
                {
                    leaderLine.AddVertexAt(0, new Point2d(cloudEdgePt.X, cloudEdgePt.Y), 0, 0, 0);
                    leaderLine.AddVertexAt(1, new Point2d(targetPt.X, targetPt.Y), 0, 0, 0);
                }
                else
                {
                    Point3d pA = cloudEdgePt - u * (dotRadius / 2.0);
                    Point3d pB = cloudEdgePt + u * (dotRadius / 2.0);
                    leaderLine.AddVertexAt(0, new Point2d(pA.X, pA.Y), 1.0, dotRadius, dotRadius);
                    leaderLine.AddVertexAt(1, new Point2d(pB.X, pB.Y), 1.0, dotRadius, dotRadius);
                    leaderLine.AddVertexAt(2, new Point2d(pA.X, pA.Y), 0.0, 0.0, 0.0);
                    leaderLine.AddVertexAt(3, new Point2d(targetPt.X, targetPt.Y), 0.0, 0.0, 0.0);
                }
            }
            else
            {
                // 默认 2 顶点直线
                leaderLine.AddVertexAt(0, new Point2d(cloudEdgePt.X, cloudEdgePt.Y), 0, 0, 0);
                leaderLine.AddVertexAt(1, new Point2d(targetPt.X, targetPt.Y), 0, 0, 0);
            }

            // 若指定了水平托线末端，则追加托线顶点
            if (landingEndPt.HasValue)
            {
                int nextIdx = leaderLine.NumberOfVertices;
                leaderLine.AddVertexAt(nextIdx, new Point2d(landingEndPt.Value.X, landingEndPt.Value.Y), 0, 0, 0);
            }

            return leaderLine;
        }

        private static void EraseNoteEntities(Database db, Transaction tr, NoteRecord note)
        {
            if (note.Cloud != null && note.Cloud.EntityHandles != null)
            {
                foreach (string hStr in note.Cloud.EntityHandles)
                {
                    TryEraseByHandle(db, tr, hStr);
                }
            }

            if (note.Text != null)
            {
                TryEraseByHandle(db, tr, note.Text.MTextHandle);
                TryEraseByHandle(db, tr, note.Text.FrameHandle);
                if (note.Text.LeaderHandles != null && note.Text.LeaderHandles.Count > 0)
                {
                    foreach (string lh in note.Text.LeaderHandles)
                    {
                        TryEraseByHandle(db, tr, lh);
                    }
                }
                else
                {
                    TryEraseByHandle(db, tr, note.Text.LeaderHandle);
                }
            }
        }

        private static void TryEraseByHandle(Database db, Transaction tr, string handleStr)
        {
            if (string.IsNullOrEmpty(handleStr)) return;
            try
            {
                if (XDataHelper.SafeGetObjectId(db, handleStr, out ObjectId id) && !id.IsNull && !id.IsErased)
                {
                    DBObject obj = tr.GetObject(id, OpenMode.ForWrite);
                    obj.Erase(true);
                }
            }
            catch { }
        }

        /// <summary>
        /// 图纸批注健康体检与轻量化清理：清理无实体关联的孤儿 XRecord，释放图纸存储空间
        /// </summary>
        public static int CompactDrawingDatabase(Database db)
        {
            if (db == null) return 0;

            int deletedOrphans = 0;
            int totalAlive = 0;

            var docState = ReactorManager.Instance.GetState(db);
            using (docState?.EnterInternalScope())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    List<NoteRecord> allNotes = XRecordHelper.ReadAllNotes(db, tr);
                    List<string> notesToDelete = new List<string>();

                    foreach (var note in allNotes)
                    {
                        bool hasAnyEntity = false;

                        if (note.Cloud?.EntityHandles != null)
                        {
                            foreach (string ch in note.Cloud.EntityHandles)
                            {
                                if (XDataHelper.SafeGetObjectId(db, ch, out ObjectId cid) && !cid.IsNull && !cid.IsErased)
                                {
                                    hasAnyEntity = true;
                                    break;
                                }
                            }
                        }

                        if (!hasAnyEntity && note.Text != null)
                        {
                            if (!string.IsNullOrEmpty(note.Text.MTextHandle) && XDataHelper.SafeGetObjectId(db, note.Text.MTextHandle, out ObjectId mid) && !mid.IsNull && !mid.IsErased)
                            {
                                hasAnyEntity = true;
                            }
                            else if (!string.IsNullOrEmpty(note.Text.FrameHandle) && XDataHelper.SafeGetObjectId(db, note.Text.FrameHandle, out ObjectId fid) && !fid.IsNull && !fid.IsErased)
                            {
                                hasAnyEntity = true;
                            }
                        }

                        if (!hasAnyEntity)
                        {
                            notesToDelete.Add(note.NoteId);
                        }
                    }

                    foreach (string noteId in notesToDelete)
                    {
                        XRecordHelper.DeleteNote(db, tr, noteId);
                        deletedOrphans++;
                    }

                    tr.Commit();
                    totalAlive = allNotes.Count - deletedOrphans;
                }
            }

            return deletedOrphans;
        }
    }
}
