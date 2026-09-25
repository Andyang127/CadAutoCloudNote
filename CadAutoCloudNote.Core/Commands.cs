#pragma warning disable CA1416
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
#if CAD_R17 || CAD_R18
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
#else
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
#endif
using CadAutoCloudNote.Core.Config;
using CadAutoCloudNote.Core.Helpers;
using CadAutoCloudNote.Core.Models;
using CadAutoCloudNote.Core.Protocols;
using CadAutoCloudNote.Core.Services;
using CadAutoCloudNote.Core.UI;
using CadAutoCloudNote.Core.UI.WinForm;
using CadAutoCloudNote.Core.Jigs;
using CadAutoCloudNote.Core.Reactors;

[assembly: CommandClass(typeof(CadAutoCloudNote.Core.Commands))]

namespace CadAutoCloudNote.Core
{
    /// <summary>
    /// CAD Auto CloudNote 命令调度器（承载 20+ 核心 CAD 业务命令）
    /// </summary>
    public class Commands
    {
        [CommandMethod("CNOTE")]
        [CommandMethod("CN")]
        [CommandMethod("INKNOTE")]
        [CommandMethod("CLOUDNOTE")]
        public void CmdCloudNote()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc?.Database == null) return;
            ExecuteCreateNote(doc, false);
        }

        [CommandMethod("CNQ")]
        [CommandMethod("NOTEQUICK")]
        [CommandMethod("CNQUICK")]
        public void CmdQuickNote()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc?.Database == null) return;
            ExecuteCreateNote(doc, true);
        }

        [CommandMethod("NOTEHELP")]
        [CommandMethod("CNHELP")]
        [CommandMethod("CNH")]
        public void CmdHelp()
        {
            HelpService.OpenReadme();
        }

        [CommandMethod("CNADDCLOUD")]
        [CommandMethod("CNAC")]
        [CommandMethod("NOTEADDCLOUD")]
        public void CmdAddCloud()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc?.Database == null) return;
            Editor ed = doc.Editor;

            try
            {
                PromptEntityOptions peo = new PromptEntityOptions("\n请选择要追加云线的批注 (点击云线、引线或文字): ");
                PromptEntityResult per = ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK) return;

                string noteId = null;
                NoteRecord targetNote = null;
                using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    DBObject obj = tr.GetObject(per.ObjectId, OpenMode.ForRead);
                    noteId = XDataHelper.Instance.GetNoteId(obj, AppConstants.XDATA_APP_NAME);
                    if (!string.IsNullOrEmpty(noteId))
                    {
                        targetNote = XRecordHelper.ReadNote(doc.Database, tr, noteId);
                    }
                }

                if (string.IsNullOrEmpty(noteId) || targetNote == null)
                {
                    ed.WriteMessage("\n[提示] 所选图元不是有效的云线批注图元。\n");
                    return;
                }

                NoteSettings settings = ConfigManager.Instance.CurrentSettings;
                double drawingScale = ScaleHelper.GetDrawingScale(doc.Database);
                double minArc = Math.Max(1.0, settings.MinArcLength);
                double arcLength = Math.Max(minArc, settings.CloudArcLengthFactor * drawingScale);
                if (settings.MaxArcLengthRatio > 0)
                {
                    double maxArc = settings.MaxArcLengthRatio * drawingScale;
                    if (arcLength > maxArc) arcLength = maxArc;
                }
                double cloudBulge = Math.Abs(settings.BulgeCurvature > 0.05 ? settings.BulgeCurvature : 0.520567);
                double cloudWidth = settings.CloudWidth > 0 ? settings.CloudWidth * drawingScale : 0.0;
                short colorIndex = settings.EnablePriorityColorLink 
                    ? (short)settings.GetPriorityColorIndex(targetNote.Priority) 
                    : (short)settings.CloudColorIndex;
                string layerName = !string.IsNullOrEmpty(settings.CloudLayer) ? settings.CloudLayer : "CAD_NOTE_CLOUD";

                PromptPointOptions ppo1 = new PromptPointOptions("\n请指定追加云线框第一个角点 或 [选择对象(O)]: ");
                ppo1.Keywords.Add("O", "O", "选择对象(O)");
                PromptPointResult ppr1 = ed.GetPoint(ppo1);
                if (ppr1.Status == PromptStatus.Cancel) return;

                if (ppr1.Status == PromptStatus.Keyword && ppr1.StringResult == "O")
                {
                    PromptEntityOptions peoObj = new PromptEntityOptions("\n请选择要追加为云线的对象 (多段线/圆/椭圆/样条曲线/圆弧/直线): ");
                    peoObj.SetRejectMessage("\n[提示] 只能选择多段线、圆、椭圆、样条曲线、圆弧或直线对象。");
                    peoObj.AddAllowedClass(typeof(Curve), false);
                    PromptEntityResult perObj = ed.GetEntity(peoObj);
                    if (perObj.Status != PromptStatus.OK) return;

                    PromptKeywordOptions pkoRev = new PromptKeywordOptions("\n是否反转云弧方向 [是(Y)/否(N)] <否>: ");
                    pkoRev.Keywords.Add("Y", "Y", "是(Y)");
                    pkoRev.Keywords.Add("N", "N", "否(N)");
                    pkoRev.Keywords.Default = "N";
                    pkoRev.AllowNone = true;
                    PromptResult prRev = ed.GetKeywords(pkoRev);
                    if (prRev.Status == PromptStatus.Cancel) return;
                    bool rev = (prRev.Status == PromptStatus.Keyword && prRev.StringResult == "Y");

                    PromptKeywordOptions pkoDel = new PromptKeywordOptions("\n是否删除原对象 [是(Y)/否(N)] <是>: ");
                    pkoDel.Keywords.Add("Y", "Y", "是(Y)");
                    pkoDel.Keywords.Add("N", "N", "否(N)");
                    pkoDel.Keywords.Default = "Y";
                    pkoDel.AllowNone = true;
                    PromptResult prDel = ed.GetKeywords(pkoDel);
                    if (prDel.Status == PromptStatus.Cancel) return;
                    bool delOrig = (prDel.Status != PromptStatus.Keyword || prDel.StringResult == "Y");

                    Polyline cloudPoly = null;
                    using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        Curve sc = tr.GetObject(perObj.ObjectId, OpenMode.ForRead) as Curve;
                        if (sc != null)
                        {
                            cloudPoly = CurveToCloudService.ConvertCurveToCloud(sc, arcLength, cloudBulge, settings.CloudStyle, cloudWidth, settings.CloudLayer, colorIndex, rev);
                        }
                        tr.Commit();
                    }

                    if (cloudPoly == null)
                    {
                        ed.WriteMessage("\n[错误] 选定对象转换为云线失败。\n");
                        return;
                    }

                    bool appendOk = false;
                    try
                    {
                        appendOk = NoteService.Instance.AppendPolylineToNote(doc.Database, noteId, cloudPoly);
                        if (appendOk)
                        {
                            if (delOrig)
                            {
                                var docState = ReactorManager.Instance.GetState(doc.Database);
                                using (docState?.EnterInternalScope())
                                {
                                    using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                                    {
                                        DBObject orig = tr.GetObject(perObj.ObjectId, OpenMode.ForWrite);
                                        orig.Erase();
                                        tr.Commit();
                                    }
                                }
                            }
                            ed.WriteMessage("\n[提示] 成功为批注追加由对象转换的云线框与关联分枝引线。\n");
                            PaletteManager.Refresh();
                        }
                        else
                        {
                            ed.WriteMessage("\n[错误] 追加云线失败。\n");
                        }
                    }
                    finally
                    {
                        if (!appendOk && cloudPoly != null)
                        {
                            try { cloudPoly.Dispose(); } catch { }
                        }
                    }
                    return;
                }

                if (ppr1.Status != PromptStatus.OK) return;

                CloudRectJig rectJig = new CloudRectJig(ppr1.Value, arcLength, cloudBulge, cloudWidth, colorIndex, settings.CloudStyle);
                PromptResult jigRes = ed.Drag(rectJig);
                if (jigRes.Status != PromptStatus.OK) return;

                bool success = NoteService.Instance.AppendCloudToNote(doc.Database, noteId, ppr1.Value, rectJig.CornerPoint);
                if (success)
                {
                    ed.WriteMessage("\n[提示] 成功为批注追加云线框与关联分枝引线。\n");
                    PaletteManager.Refresh();
                }
                else
                {
                    ed.WriteMessage("\n[错误] 追加云线框失败，未能找到关联批注数据。\n");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[错误] 追加云线框操作失败: " + ex.Message + "\n");
                Logger.Error("CmdAddCloud 发生异常", ex);
            }
        }

        [CommandMethod("CNCONVERT")]
        [CommandMethod("CNCV")]
        [CommandMethod("NOTECONVERT")]
        public void CmdConvertObjectToCloud()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc?.Database == null) return;
            ExecuteConvertObject(doc, false);
        }

        /// <summary>
        /// 极速模式命令行单键切词交互（S=快捷短语1-9具体短语直选，T=即时手工输入内容）
        /// 将 1-9 条具体短语完整呈现在命令栏提示串与动态输入下拉菜单中，支持 1-9 键选、超链接点击与动态输入点选
        /// </summary>
        private static bool HandleRapidKeywordPrompt(Editor ed, string keyword, string discipline, ref string title)
        {
            if (string.Equals(keyword, "S", StringComparison.OrdinalIgnoreCase))
            {
                var phrases = RapidPhraseService.GetRapidPromptPhrases(discipline);
                int count = Math.Min(9, phrases.Count);
                if (count == 0) return false;

                // 确定当前默认项序号（若已有标题匹配某短语则默认选中该项，否则默认第 1 项）
                string defaultKey = "1";
                for (int i = 0; i < count; i++)
                {
                    if (!string.IsNullOrEmpty(title) && title.Contains(phrases[i].Text))
                    {
                        defaultKey = (i + 1).ToString();
                        break;
                    }
                }

                // 1. 在命令历史窗口打印清晰结构化列表（方便 F2 或多行历史查阅）
                ed.WriteMessage("\n==================== [CNQ 快捷短语 1-9] ====================");
                for (int i = 0; i < count; i++)
                {
                    ed.WriteMessage(string.Format("\n  [{0}] {1}", i + 1, phrases[i].Text));
                }
                ed.WriteMessage("\n=============================================================");

                // 2. 组装符合 AutoCAD 规范的关键字提示文本，将具体短语直接呈现在命令栏与动态输入上
                // 格式：\n请选择短语 [1-待修改(1)/2-请核实(2)/3-请补充(3)/.../自定义(T)]:
                StringBuilder sbPrompt = new StringBuilder();
                sbPrompt.Append("\n请选择短语 [");

                Dictionary<string, string> keyToPhrase = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < count; i++)
                {
                    string key = (i + 1).ToString();
                    string text = phrases[i].Text;
                    // 屏幕显示若过长则适度精炼，避免动态输入/命令栏溢出
                    string dispText = text.Length > 6 ? text.Substring(0, 5) + "…" : text;
                    string dispKeyword = string.Format("{0}-{1}({0})", key, dispText);

                    if (i > 0) sbPrompt.Append("/");
                    sbPrompt.Append(dispKeyword);

                    keyToPhrase[key] = text;
                    keyToPhrase[dispKeyword] = text;
                    keyToPhrase[string.Format("{0}-{1}", key, dispText)] = text;
                    keyToPhrase[text] = text;
                }

                sbPrompt.Append("/自定义(T)]: ");
                string promptMsg = sbPrompt.ToString();

                // 3. 采用 PromptKeywordOptions 激活命令栏超链接点击与动态输入(DYNMODE)下拉菜单
                PromptKeywordOptions pko = new PromptKeywordOptions(promptMsg);
                pko.AllowNone = true; // 允许回车/空格直接选取默认项

                for (int i = 0; i < count; i++)
                {
                    string key = (i + 1).ToString();
                    string text = phrases[i].Text;
                    string dispText = text.Length > 6 ? text.Substring(0, 5) + "…" : text;
                    string dispKeyword = string.Format("{0}-{1}({0})", key, dispText);
                    pko.Keywords.Add(key, key, dispKeyword);
                }

                pko.Keywords.Add("T", "T", "自定义(T)");
                pko.Keywords.Default = defaultKey;

                PromptResult pkoRes = ed.GetKeywords(pko);

                if (pkoRes.Status == PromptStatus.Cancel)
                {
                    return false;
                }

                if (pkoRes.Status == PromptStatus.None)
                {
                    // 用户直接按回车或空格，采用默认项
                    int defIdx = int.TryParse(defaultKey, out int dVal) ? dVal - 1 : 0;
                    if (defIdx >= 0 && defIdx < count)
                    {
                        string chosen = phrases[defIdx].Text;
                        title = NoteService.GetDefaultSimpleTitle(chosen);
                        RapidPhraseService.IncrementUsage(chosen);
                        ed.WriteMessage(string.Format("\n[已采用默认短语] {0}\n", chosen));
                        return true;
                    }
                    return false;
                }

                if (pkoRes.Status == PromptStatus.OK)
                {
                    string resStr = pkoRes.StringResult?.Trim() ?? string.Empty;

                    // 若用户选择 T 自定义
                    if (string.Equals(resStr, "T", StringComparison.OrdinalIgnoreCase))
                    {
                        PromptStringOptions pso = new PromptStringOptions("\n请输入批注自定义内容: ");
                        pso.AllowSpaces = true;
                        var psoRes = ed.GetString(pso);
                        if (psoRes.Status == PromptStatus.OK && !string.IsNullOrEmpty(psoRes.StringResult?.Trim()))
                        {
                            string custom = psoRes.StringResult.Trim();
                            title = NoteService.GetDefaultSimpleTitle(custom);
                            ed.WriteMessage(string.Format("\n[已自定义文字] {0}\n", custom));
                            return true;
                        }
                        return false;
                    }

                    // 匹配 1-9 关键字
                    string chosenPhrase = null;
                    if (keyToPhrase.TryGetValue(resStr, out string matched))
                    {
                        chosenPhrase = matched;
                    }
                    else if (int.TryParse(resStr, out int num) && num >= 1 && num <= count)
                    {
                        chosenPhrase = phrases[num - 1].Text;
                    }

                    if (!string.IsNullOrEmpty(chosenPhrase))
                    {
                        title = NoteService.GetDefaultSimpleTitle(chosenPhrase);
                        RapidPhraseService.IncrementUsage(chosenPhrase);
                        ed.WriteMessage(string.Format("\n[已选定短语] {0}\n", chosenPhrase));
                        return true;
                    }
                }
            }
            else if (string.Equals(keyword, "T", StringComparison.OrdinalIgnoreCase))
            {
                PromptStringOptions pso = new PromptStringOptions("\n请输入批注自定义内容: ");
                pso.AllowSpaces = true;
                var psoRes = ed.GetString(pso);
                if (psoRes.Status == PromptStatus.OK && !string.IsNullOrEmpty(psoRes.StringResult?.Trim()))
                {
                    string chosen = psoRes.StringResult.Trim();
                    title = NoteService.GetDefaultSimpleTitle(chosen);
                    ed.WriteMessage(string.Format("\n[已自定义文字] {0}\n", chosen));
                    return true;
                }
            }
            return false;
        }

        private void ExecuteCreateNote(Document doc, bool forceSimpleMode)
        {
            Editor ed = doc.Editor;
            try
            {
                NoteSettings settings = ConfigManager.Instance.CurrentSettings;
                double drawingScale = ScaleHelper.GetDrawingScale(doc.Database);
                double minArc = Math.Max(1.0, settings.MinArcLength);
                double arcLength = Math.Max(minArc, settings.CloudArcLengthFactor * drawingScale);
                if (settings.MaxArcLengthRatio > 0)
                {
                    double maxArc = settings.MaxArcLengthRatio * drawingScale;
                    if (arcLength > maxArc) arcLength = maxArc;
                }
                double cloudBulge = Math.Abs(settings.BulgeCurvature > 0.05 ? settings.BulgeCurvature : 0.520567);
                double cloudWidth = settings.CloudWidth > 0 ? settings.CloudWidth * drawingScale : 0.0;
                short colorIndex = NoteLifecycleStateExtensions.GetColorIndex(NoteLifecycleState.Pending);

                // 1. 根据配置判断云线绘制模式 (0=矩形, 1=多边形, 2=徒手)
                string cmdPrefix = forceSimpleMode ? "[CNQ急速批注]" : "[CN新建批注]";
                string layerName = !string.IsNullOrEmpty(settings.CloudLayer) ? settings.CloudLayer : "CAD_NOTE_CLOUD";

                if (settings.DefaultCloudType == 2)
                {
                    // 徒手画云线模式 (Freehand)
                    PromptPointOptions ppoFree = new PromptPointOptions(string.Format("\n{0} 请指定徒手画云线起点 或 [选择对象(O)]: ", cmdPrefix));
                    ppoFree.Keywords.Add("O", "O", "选择对象(O)");
                    PromptPointResult pprFree = ed.GetPoint(ppoFree);
                    if (pprFree.Status == PromptStatus.Cancel) return;
                    if (pprFree.Status == PromptStatus.Keyword && pprFree.StringResult == "O")
                    {
                        ExecuteConvertObject(doc, forceSimpleMode);
                        return;
                    }
                    if (pprFree.Status != PromptStatus.OK) return;

                    FreehandCloudJig freeJig = new FreehandCloudJig(pprFree.Value, arcLength, cloudBulge, cloudWidth, colorIndex, settings.CloudStyle);
                    PromptResult freeRes = ed.Drag(freeJig);
                    if (freeJig.ResultPolyline == null && freeJig.IsClosed)
                    {
                        freeJig.BuildFinalPolyline();
                    }
                    if (freeJig.ResultPolyline == null) return;

                    Polyline freePoly = freeJig.ResultPolyline;
                    bool isFreePolyAdded = false;
                    try
                    {
                        string freeTitle = string.Empty;
                        string freeContent = string.Empty;
                        string freeDiscipline = settings.DefaultDiscipline;
                        NotePriority freePriority = NoteSettings.ParsePriority(settings.DefaultPriority);
                        string freeAssignee = settings.DefaultAssignee;

                        bool isSimpleFree = forceSimpleMode;
                        int nSeq = 1;
                        if (isSimpleFree)
                        {
                            // 快速批注：免等待确认直接流转！无需等待用户敲回车确认“待修改”
                            freeTitle = NoteService.GetDefaultSimpleTitle();
                            freeContent = string.Empty;
                            nSeq = NoteService.GetNextSequenceNumber(doc.Database);
                        }
                        else
                        {
                            nSeq = NoteService.GetNextSequenceNumber(doc.Database);
                            var tempNote = new NoteRecord
                            {
                                Discipline = settings.DefaultDiscipline,
                                Assignee = settings.DefaultAssignee,
                                Priority = freePriority,
                                SequenceNumber = nSeq
                            };
                            if (!NoteUIProvider.Instance.ShowNoteEditDialog(tempNote, true)) return;
                            freeTitle = tempNote.Title;
                            freeContent = tempNote.Content;
                            freeDiscipline = tempNote.Discipline;
                            freePriority = tempNote.Priority;
                            freeAssignee = tempNote.Assignee;
                            nSeq = tempNote.SequenceNumber;
                        }

                        Extents3d freeExt = CurveToCloudService.GetPolylineExtents(freePoly);
                        double textH = Math.Max(settings.DefaultTextHeight * drawingScale, 2.5);
                        double arrowSz = Math.Max(2.0, settings.ArrowSizeRatio * drawingScale);

                        Point3d initPt = new Point3d(freeExt.MaxPoint.X + 15.0 * drawingScale, freeExt.MaxPoint.Y + 15.0 * drawingScale, 0);
                        LeaderPlacementJig leaderJig = new LeaderPlacementJig(
                            new List<Extents3d> { freeExt },
                            initPt,
                            textH,
                            colorIndex,
                            freeTitle,
                            isSimpleFree,
                            settings.LeaderType,
                            nSeq,
                            arrowSz,
                            cloudPolylines: new List<Polyline> { freePoly },
                            previewContent: freeContent);

                        PromptResult lRes = ed.Drag(leaderJig);
                        while (isSimpleFree && settings.EnableRapidCmdKeySelection && (leaderJig.KeywordResult == "S" || leaderJig.KeywordResult == "T"))
                        {
                            HandleRapidKeywordPrompt(ed, leaderJig.KeywordResult, freeDiscipline, ref freeTitle);
                            leaderJig = new LeaderPlacementJig(
                                new List<Extents3d> { freeExt },
                                initPt,
                                textH,
                                colorIndex,
                                freeTitle,
                                isSimpleFree,
                                settings.LeaderType,
                                nSeq,
                                arrowSz,
                                cloudPolylines: new List<Polyline> { freePoly },
                                previewContent: freeContent);
                            lRes = ed.Drag(leaderJig);
                        }
                        Point3d tPt = lRes.Status == PromptStatus.OK ? leaderJig.TextPoint : initPt;

                        NoteRecord note = NoteService.Instance.CreateCloudNoteFromPolyline(
                            doc.Database,
                            freePoly,
                            freeTitle,
                            freeContent,
                            freeDiscipline,
                            freePriority,
                            tPt,
                            freeAssignee,
                            isSimpleFree,
                            manualSeq: nSeq);

                        if (note != null)
                        {
                            isFreePolyAdded = true;
                            ed.WriteMessage(string.Format("\n[提示] 已创建徒手云线批注 {0} (状态: {1})\n",
                                note.Title,
                                NoteLifecycleStateExtensions.ToDisplayName(note.State)));
                            PaletteManager.Refresh();
                        }
                    }
                    finally
                    {
                        if (!isFreePolyAdded && freePoly != null)
                        {
                            try { freePoly.Dispose(); } catch { }
                        }
                    }
                    return;
                }
                else if (settings.DefaultCloudType == 1)
                {
                    // 多边形云线模式 (Polygonal)
                    PromptPointOptions ppoPoly = new PromptPointOptions(string.Format("\n{0} 请指定多边形云线起点 或 [选择对象(O)]: ", cmdPrefix));
                    ppoPoly.Keywords.Add("O", "O", "选择对象(O)");
                    PromptPointResult pprPoly = ed.GetPoint(ppoPoly);
                    if (pprPoly.Status == PromptStatus.Cancel) return;
                    if (pprPoly.Status == PromptStatus.Keyword && pprPoly.StringResult == "O")
                    {
                        ExecuteConvertObject(doc, forceSimpleMode);
                        return;
                    }
                    if (pprPoly.Status != PromptStatus.OK) return;

                    PolygonCloudJig polyJig = new PolygonCloudJig(pprPoly.Value, arcLength, cloudBulge, cloudWidth, colorIndex, settings.CloudStyle);
                    while (!polyJig.IsClosed)
                    {
                        PromptResult pRes = ed.Drag(polyJig);
                        PromptStatus status = pRes.Status;
                        string kw = pRes.StringResult;

                        // 当用户按下空格、回车或右键闭合时，Sampler 中已捕获 None 或 Keyword
                        if (status == PromptStatus.Cancel && polyJig.LastPromptStatus != PromptStatus.Cancel)
                        {
                            status = polyJig.LastPromptStatus;
                            kw = polyJig.LastKeyword;
                        }

                        if (status == PromptStatus.OK)
                        {
                            polyJig.AddCurrentPoint();
                        }
                        else if (status == PromptStatus.Keyword)
                        {
                            if (string.Equals(kw, "C", StringComparison.OrdinalIgnoreCase))
                            {
                                if (polyJig.VertexCount >= 3)
                                {
                                    polyJig.Close();
                                    break;
                                }
                                else
                                {
                                    ed.WriteMessage("\n[提示] 至少需要指定 3 个角点才能闭合多边形，请继续指定角点。");
                                }
                            }
                            else if (string.Equals(kw, "U", StringComparison.OrdinalIgnoreCase))
                            {
                                polyJig.RemoveLastPoint();
                            }
                        }
                        else if (status == PromptStatus.None)
                        {
                            // 空格、回车、右键均可直接闭合
                            if (polyJig.VertexCount >= 3)
                            {
                                polyJig.Close();
                                break;
                            }
                            else
                            {
                                ed.WriteMessage("\n[提示] 至少需要指定 3 个角点才能闭合多边形，请继续指定角点。");
                            }
                        }
                        else
                        {
                            return;
                        }
                    }

                    if (polyJig.ResultPolyline == null) return;
                    Polyline polygonPoly = polyJig.ResultPolyline;
                    bool isPolyAdded = false;
                    try
                    {
                        bool isSimplePoly = forceSimpleMode;
                        string polyTitle = string.Empty;
                        string polyContent = string.Empty;
                        string polyDiscipline = settings.DefaultDiscipline;
                        NotePriority polyPriority = NoteSettings.ParsePriority(settings.DefaultPriority);
                        string polyAssignee = settings.DefaultAssignee;
                        int nSeq = 1;

                        if (isSimplePoly)
                        {
                            polyTitle = NoteService.GetDefaultSimpleTitle();
                            polyContent = string.Empty;
                            nSeq = NoteService.GetNextSequenceNumber(doc.Database);
                        }
                        else
                        {
                            nSeq = NoteService.GetNextSequenceNumber(doc.Database);
                            var tempNote = new NoteRecord
                            {
                                Discipline = settings.DefaultDiscipline,
                                Assignee = settings.DefaultAssignee,
                                Priority = polyPriority,
                                SequenceNumber = nSeq
                            };
                            if (!NoteUIProvider.Instance.ShowNoteEditDialog(tempNote, true)) return;
                            polyTitle = tempNote.Title;
                            polyContent = tempNote.Content;
                            polyDiscipline = tempNote.Discipline;
                            polyPriority = tempNote.Priority;
                            polyAssignee = tempNote.Assignee;
                            nSeq = tempNote.SequenceNumber;
                        }

                        Extents3d polyExt = CurveToCloudService.GetPolylineExtents(polygonPoly);
                        double textH = Math.Max(settings.DefaultTextHeight * drawingScale, 2.5);
                        double arrowSz = Math.Max(2.0, settings.ArrowSizeRatio * drawingScale);

                        Point3d initPt = new Point3d(polyExt.MaxPoint.X + 15.0 * drawingScale, polyExt.MaxPoint.Y + 15.0 * drawingScale, 0);
                        LeaderPlacementJig leaderJig = new LeaderPlacementJig(
                            new List<Extents3d> { polyExt },
                            initPt,
                            textH,
                            colorIndex,
                            polyTitle,
                            isSimplePoly,
                            settings.LeaderType,
                            nSeq,
                            arrowSz,
                            cloudPolylines: new List<Polyline> { polygonPoly },
                            previewContent: polyContent);

                        PromptResult lRes = ed.Drag(leaderJig);
                        while (isSimplePoly && settings.EnableRapidCmdKeySelection && (leaderJig.KeywordResult == "S" || leaderJig.KeywordResult == "T"))
                        {
                            HandleRapidKeywordPrompt(ed, leaderJig.KeywordResult, polyDiscipline, ref polyTitle);
                            leaderJig = new LeaderPlacementJig(
                                new List<Extents3d> { polyExt },
                                initPt,
                                textH,
                                colorIndex,
                                polyTitle,
                                isSimplePoly,
                                settings.LeaderType,
                                nSeq,
                                arrowSz,
                                cloudPolylines: new List<Polyline> { polygonPoly },
                                previewContent: polyContent);
                            lRes = ed.Drag(leaderJig);
                        }
                        Point3d tPt = lRes.Status == PromptStatus.OK ? leaderJig.TextPoint : initPt;

                        NoteRecord note = NoteService.Instance.CreateCloudNoteFromPolyline(
                            doc.Database,
                            polygonPoly,
                            polyTitle,
                            polyContent,
                            polyDiscipline,
                            polyPriority,
                            tPt,
                            polyAssignee,
                            isSimplePoly,
                            manualSeq: nSeq);

                        if (note != null)
                        {
                            isPolyAdded = true;
                            ed.WriteMessage(string.Format("\n[提示] 已创建多边形云线批注 {0} (状态: {1})\n",
                                note.Title,
                                NoteLifecycleStateExtensions.ToDisplayName(note.State)));
                            PaletteManager.Refresh();
                        }
                    }
                    finally
                    {
                        if (!isPolyAdded && polygonPoly != null)
                        {
                            try { polygonPoly.Dispose(); } catch { }
                        }
                    }
                    return;
                }

                // 矩形云线模式
                PromptPointOptions ppo1 = new PromptPointOptions(string.Format("\n{0} 请指定云线框第一个角点 或 [选择对象(O)]: ", cmdPrefix));
                ppo1.Keywords.Add("O", "O", "选择对象(O)");
                PromptPointResult ppr1 = ed.GetPoint(ppo1);
                if (ppr1.Status == PromptStatus.Cancel) return;

                if (ppr1.Status == PromptStatus.Keyword && ppr1.StringResult == "O")
                {
                    ExecuteConvertObject(doc, forceSimpleMode);
                    return;
                }
                if (ppr1.Status != PromptStatus.OK) return;

                CloudRectJig jig1 = new CloudRectJig(ppr1.Value, arcLength, cloudBulge, cloudWidth, colorIndex, settings.CloudStyle);
                PromptResult jigRes1 = ed.Drag(jig1);
                if (jigRes1.Status != PromptStatus.OK) return;
                Point3d pt2 = jig1.CornerPoint;

                List<CloudRect> cloudRects = new List<CloudRect>();
                cloudRects.Add(new CloudRect(ppr1.Value, pt2));

                // 2. 连续支持多个云线框
                while (true)
                {
                    PromptPointOptions pNextOpt = new PromptPointOptions("\n请指定下一个云线框第一角点 [继续框选(C)/完成框选(D)] <完成框选>: ");
                    pNextOpt.Keywords.Add("C", "C", "继续框选(C)");
                    pNextOpt.Keywords.Add("D", "D", "完成框选(D)");
                    pNextOpt.Keywords.Default = "D";
                    pNextOpt.AllowNone = true;

                    PromptPointResult pNextRes = ed.GetPoint(pNextOpt);
                    if (pNextRes.Status == PromptStatus.None || (pNextRes.Status == PromptStatus.Keyword && pNextRes.StringResult == "D"))
                    {
                        break;
                    }
                    if (pNextRes.Status == PromptStatus.Cancel)
                    {
                        return;
                    }

                    Point3d nextP1;
                    if (pNextRes.Status == PromptStatus.Keyword && pNextRes.StringResult == "C")
                    {
                        PromptPointResult pcRes = ed.GetPoint("\n请指定下一个云线框第一角点: ");
                        if (pcRes.Status != PromptStatus.OK) break;
                        nextP1 = pcRes.Value;
                    }
                    else if (pNextRes.Status == PromptStatus.OK)
                    {
                        nextP1 = pNextRes.Value;
                    }
                    else
                    {
                        break;
                    }

                    CloudRectJig nextJig = new CloudRectJig(nextP1, arcLength, cloudBulge, cloudWidth, colorIndex, settings.CloudStyle);
                    PromptResult nextJigRes = ed.Drag(nextJig);
                    if (nextJigRes.Status != PromptStatus.OK) break;
                    cloudRects.Add(new CloudRect(nextP1, nextJig.CornerPoint));
                }

                // 3. 录入批注内容 (极速模式免等待确认 vs 详细弹窗模式)
                string title = string.Empty;
                string content = string.Empty;
                string discipline = settings.DefaultDiscipline;
                NotePriority priority = NoteSettings.ParsePriority(settings.DefaultPriority);
                string assignee = settings.DefaultAssignee;

                bool isSimple = forceSimpleMode;
                int nextSeq = 1;
                if (isSimple)
                {
                    // 极速批注模式：免等待确认直接流转！无需等待用户敲回车确认“待修改”，若有需要后期由用户自行修改
                    title = NoteService.GetDefaultSimpleTitle();
                    content = string.Empty;
                    nextSeq = NoteService.GetNextSequenceNumber(doc.Database);
                }
                else
                {
                    nextSeq = NoteService.GetNextSequenceNumber(doc.Database);
                    var tempNote = new NoteRecord
                    {
                        Discipline = settings.DefaultDiscipline,
                        Assignee = settings.DefaultAssignee,
                        Priority = priority,
                        SequenceNumber = nextSeq
                    };
                    if (!NoteUIProvider.Instance.ShowNoteEditDialog(tempNote, true)) return;
                    title = tempNote.Title;
                    content = tempNote.Content;
                    discipline = tempNote.Discipline;
                    priority = tempNote.Priority;
                    assignee = tempNote.Assignee;
                    nextSeq = tempNote.SequenceNumber;
                }

                // 4. 动态引线与文字放置 Jig 交互（生成预览云线确保拉引线期间云线全程清晰可见）
                List<Extents3d> boxes = new List<Extents3d>();
                List<Polyline> previewClouds = new List<Polyline>();
                double totalMaxX = double.MinValue, totalMaxY = double.MinValue;

                try
                {
                    foreach (var r in cloudRects)
                    {
                        Point3d min = new Point3d(Math.Min(r.Pt1.X, r.Pt2.X), Math.Min(r.Pt1.Y, r.Pt2.Y), 0);
                        Point3d max = new Point3d(Math.Max(r.Pt1.X, r.Pt2.X), Math.Max(r.Pt1.Y, r.Pt2.Y), 0);
                        boxes.Add(new Extents3d(min, max));
                        totalMaxX = Math.Max(totalMaxX, max.X);
                        totalMaxY = Math.Max(totalMaxY, max.Y);

                        Polyline previewPoly = NoteService.CreateRectangularCloud(min, max, arcLength, layerName, colorIndex, cloudBulge, cloudWidth, settings.CloudStyle);
                        if (previewPoly != null) previewClouds.Add(previewPoly);
                    }

                    double textHeight = Math.Max(settings.DefaultTextHeight * drawingScale, 2.5);
                    double arrowSize = Math.Max(2.0, settings.ArrowSizeRatio * drawingScale);

                    Point3d initialTextPt = new Point3d(totalMaxX + 15.0 * drawingScale, totalMaxY + 15.0 * drawingScale, 0);

                    LeaderPlacementJig leaderJig = new LeaderPlacementJig(
                        boxes,
                        initialTextPt,
                        textHeight,
                        colorIndex,
                        title,
                        isSimple,
                        settings.LeaderType,
                        nextSeq,
                        arrowSize,
                        cloudPolylines: previewClouds,
                        previewContent: content);
                    PromptResult leaderRes = ed.Drag(leaderJig);
                    while (isSimple && settings.EnableRapidCmdKeySelection && (leaderJig.KeywordResult == "S" || leaderJig.KeywordResult == "T"))
                    {
                        HandleRapidKeywordPrompt(ed, leaderJig.KeywordResult, discipline, ref title);
                        leaderJig = new LeaderPlacementJig(
                            boxes,
                            initialTextPt,
                            textHeight,
                            colorIndex,
                            title,
                            isSimple,
                            settings.LeaderType,
                            nextSeq,
                            arrowSize,
                            cloudPolylines: previewClouds,
                            previewContent: content);
                        leaderRes = ed.Drag(leaderJig);
                    }
                    Point3d textPt = leaderRes.Status == PromptStatus.OK ? leaderJig.TextPoint : initialTextPt;

                    // 5. 调用核心服务生成批注与图元
                    NoteRecord note = NoteService.Instance.CreateCloudNote(
                        doc.Database,
                        title,
                        content,
                        discipline,
                        priority,
                        cloudRects,
                        textPt,
                        assignee,
                        isSimple,
                        manualSeq: nextSeq);

                    if (note != null)
                    {
                        ed.WriteMessage(string.Format("\n[提示] 已创建云线批注 {0} (共关联 {1} 个云线框, 状态: {2})\n",
                            note.Title,
                            cloudRects.Count,
                            NoteLifecycleStateExtensions.ToDisplayName(note.State)));
                        PaletteManager.Refresh();
                    }
                }
                finally
                {
                    foreach (var p in previewClouds)
                    {
                        try { p?.Dispose(); } catch { }
                    }
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[错误] 创建云线批注失败: " + ex.Message + "\n");
                Logger.Error("ExecuteCreateNote 发生异常", ex);
            }
        }

        private void ExecuteConvertObject(Document doc, bool forceSimpleMode)
        {
            Editor ed = doc.Editor;
            try
            {
                NoteSettings settings = ConfigManager.Instance.CurrentSettings;
                double drawingScale = ScaleHelper.GetDrawingScale(doc.Database);
                double minArc = Math.Max(1.0, settings.MinArcLength);
                double arcLength = Math.Max(minArc, settings.CloudArcLengthFactor * drawingScale);
                if (settings.MaxArcLengthRatio > 0)
                {
                    double maxArc = settings.MaxArcLengthRatio * drawingScale;
                    if (arcLength > maxArc) arcLength = maxArc;
                }
                double cloudBulge = Math.Abs(settings.BulgeCurvature > 0.05 ? settings.BulgeCurvature : 0.520567);
                double cloudWidth = settings.CloudWidth > 0 ? settings.CloudWidth * drawingScale : 0.0;
                short colorIndex = NoteLifecycleStateExtensions.GetColorIndex(NoteLifecycleState.Pending);
                string layerName = !string.IsNullOrEmpty(settings.CloudLayer) ? settings.CloudLayer : "CAD_NOTE_CLOUD";

                // 1. 提示选取曲线对象
                PromptEntityOptions peo = new PromptEntityOptions("\n请选择要转换为云线的对象 (多段线/圆/椭圆/样条曲线/圆弧/直线): ");
                peo.SetRejectMessage("\n[提示] 只能选择多段线、圆、椭圆、样条曲线、圆弧或直线对象。");
                peo.AddAllowedClass(typeof(Curve), false);
                PromptEntityResult per = ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK) return;

                // 2. 提示是否反转云弧方向 (对齐 AutoCAD 原生 REVCLOUD)
                PromptKeywordOptions pkoRev = new PromptKeywordOptions("\n是否反转云弧方向 [是(Y)/否(N)] <否>: ");
                pkoRev.Keywords.Add("Y", "Y", "是(Y)");
                pkoRev.Keywords.Add("N", "N", "否(N)");
                pkoRev.Keywords.Default = "N";
                pkoRev.AllowNone = true;
                PromptResult prRev = ed.GetKeywords(pkoRev);
                if (prRev.Status == PromptStatus.Cancel) return;
                bool reverseDirection = (prRev.Status == PromptStatus.Keyword && prRev.StringResult == "Y");

                // 3. 提示是否删除原对象
                PromptKeywordOptions pkoDel = new PromptKeywordOptions("\n是否删除原对象 [是(Y)/否(N)] <是>: ");
                pkoDel.Keywords.Add("Y", "Y", "是(Y)");
                pkoDel.Keywords.Add("N", "N", "否(N)");
                pkoDel.Keywords.Default = "Y";
                pkoDel.AllowNone = true;
                PromptResult prDel = ed.GetKeywords(pkoDel);
                if (prDel.Status == PromptStatus.Cancel) return;
                bool deleteOriginal = (prDel.Status != PromptStatus.Keyword || prDel.StringResult == "Y");

                // 4. 执行几何离散与转换
                Polyline cloudPoly = null;
                using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    Curve sourceCurve = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Curve;
                    if (sourceCurve == null)
                    {
                        ed.WriteMessage("\n[错误] 所选对象不是有效的曲线图元。\n");
                        return;
                    }

                    cloudPoly = CurveToCloudService.ConvertCurveToCloud(
                        sourceCurve,
                        arcLength,
                        cloudBulge,
                        settings.CloudStyle,
                        cloudWidth,
                        layerName,
                        colorIndex,
                        reverseDirection);

                    tr.Commit();
                }

                if (cloudPoly == null)
                {
                    ed.WriteMessage("\n[错误] 选定对象转换为云线失败，几何长度过小或不受支持。\n");
                    return;
                }

                bool isPolyAdded = false;
                try
                {
                    // 5. 提示是否添加批注内容（若由 CNOTE/CNQ 触发则默认为直接添加批注）
                    bool addNote = true;
                    if (!forceSimpleMode)
                    {
                        PromptKeywordOptions pkoNote = new PromptKeywordOptions("\n是否为此云线添加批注内容 [是(Y)/否(N)] <是>: ");
                        pkoNote.Keywords.Add("Y", "Y", "是(Y)");
                        pkoNote.Keywords.Add("N", "N", "否(N)");
                        pkoNote.Keywords.Default = "Y";
                        pkoNote.AllowNone = true;
                        PromptResult prNote = ed.GetKeywords(pkoNote);
                        if (prNote.Status == PromptStatus.Cancel) return;
                        addNote = (prNote.Status != PromptStatus.Keyword || prNote.StringResult == "Y");
                    }

                    // 若不添加批注，仅作为独立云线放置在图纸上
                    if (!addNote)
                    {
                        var docState = ReactorManager.Instance.GetState(doc.Database);
                        using (docState?.EnterInternalScope())
                        {
                            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                            {
                                ValidationService.EnsureNoteLayers(doc.Database, tr, settings, NotePriority.Important);
                                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(doc.Database.CurrentSpaceId, OpenMode.ForWrite);
                                cloudPoly.Layer = layerName;
                                cloudPoly.ColorIndex = colorIndex;
                                btr.AppendEntity(cloudPoly);
                                tr.AddNewlyCreatedDBObject(cloudPoly, true);
                                isPolyAdded = true;

                                if (deleteOriginal)
                                {
                                    DBObject origObj = tr.GetObject(per.ObjectId, OpenMode.ForWrite);
                                    origObj.Erase();
                                }

                                tr.Commit();
                            }
                        }
                        ed.WriteMessage("\n[提示] 成功将对象转换为云线。\n");
                        return;
                    }

                // 6. 录入批注内容 (极速模式免等待确认 vs 详细弹窗模式)
                string title = string.Empty;
                string content = string.Empty;
                string discipline = settings.DefaultDiscipline;
                NotePriority priority = NoteSettings.ParsePriority(settings.DefaultPriority);
                string assignee = settings.DefaultAssignee;

                bool isSimple = forceSimpleMode;
                int nextSeq = 1;
                if (isSimple)
                {
                    // 极速批注模式：免等待确认直接流转！无需等待用户敲回车确认“待修改”，若有需要后期由用户自行修改
                    title = NoteService.GetDefaultSimpleTitle();
                    content = string.Empty;
                    nextSeq = NoteService.GetNextSequenceNumber(doc.Database);
                }
                else
                {
                    nextSeq = NoteService.GetNextSequenceNumber(doc.Database);
                    var tempNote = new NoteRecord
                    {
                        Discipline = settings.DefaultDiscipline,
                        Assignee = settings.DefaultAssignee,
                        Priority = priority,
                        SequenceNumber = nextSeq
                    };
                    if (!NoteUIProvider.Instance.ShowNoteEditDialog(tempNote, true)) return;
                    title = tempNote.Title;
                    content = tempNote.Content;
                    discipline = tempNote.Discipline;
                    priority = tempNote.Priority;
                    assignee = tempNote.Assignee;
                    nextSeq = tempNote.SequenceNumber;
                }

                // 7. 动态引线与文字放置 (临时隐藏原曲线，传入新云线实时高亮呈现)
                try
                {
                    using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        Curve sc = tr.GetObject(per.ObjectId, OpenMode.ForWrite) as Curve;
                        if (sc != null) sc.Visible = false;
                        tr.Commit();
                    }
                }
                catch { }

                Extents3d actualBox = CurveToCloudService.GetPolylineExtents(cloudPoly);
                List<Extents3d> boxes = new List<Extents3d>();
                boxes.Add(actualBox);

                double textHeight = Math.Max(settings.DefaultTextHeight * drawingScale, 2.5);
                double arrowSize = Math.Max(2.0, settings.ArrowSizeRatio * drawingScale);

                Point3d initialTextPt = new Point3d(actualBox.MaxPoint.X + 15.0 * drawingScale, actualBox.MaxPoint.Y + 15.0 * drawingScale, 0);

                LeaderPlacementJig leaderJig = new LeaderPlacementJig(
                    boxes,
                    initialTextPt,
                    textHeight,
                    colorIndex,
                    title,
                    isSimple,
                    settings.LeaderType,
                    nextSeq,
                    arrowSize,
                    cloudPolylines: new List<Polyline> { cloudPoly },
                    previewContent: content);
                PromptResult leaderRes = ed.Drag(leaderJig);

                while (isSimple && settings.EnableRapidCmdKeySelection && (leaderJig.KeywordResult == "S" || leaderJig.KeywordResult == "T"))
                {
                    HandleRapidKeywordPrompt(ed, leaderJig.KeywordResult, discipline, ref title);
                    leaderJig = new LeaderPlacementJig(
                        boxes,
                        initialTextPt,
                        textHeight,
                        colorIndex,
                        title,
                        isSimple,
                        settings.LeaderType,
                        nextSeq,
                        arrowSize,
                        cloudPolylines: new List<Polyline> { cloudPoly },
                        previewContent: content);
                    leaderRes = ed.Drag(leaderJig);
                }

                if (leaderRes.Status != PromptStatus.OK)
                {
                    // 用户取消引线放置，恢复原对象可见性
                    try
                    {
                        using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                        {
                            Curve sc = tr.GetObject(per.ObjectId, OpenMode.ForWrite) as Curve;
                            if (sc != null) sc.Visible = true;
                            tr.Commit();
                        }
                    }
                    catch { }
                    return;
                }

                Point3d textPt = leaderJig.TextPoint;

                // 8. 创建批注实体与引线落库
                NoteRecord note = NoteService.Instance.CreateCloudNoteFromPolyline(
                    doc.Database,
                    cloudPoly,
                    title,
                    content,
                    discipline,
                    priority,
                    textPt,
                    assignee,
                    isSimple,
                    manualSeq: nextSeq);

                if (note != null)
                {
                    isPolyAdded = true;
                    // 若需要删除原对象
                    if (deleteOriginal)
                    {
                        var docState = ReactorManager.Instance.GetState(doc.Database);
                        using (docState?.EnterInternalScope())
                        {
                            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                            {
                                DBObject origObj = tr.GetObject(per.ObjectId, OpenMode.ForWrite);
                                origObj.Erase();
                                tr.Commit();
                            }
                        }
                    }
                    else
                    {
                        // 保留原对象，恢复可见性
                        try
                        {
                            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                            {
                                Curve sc = tr.GetObject(per.ObjectId, OpenMode.ForWrite) as Curve;
                                if (sc != null) sc.Visible = true;
                                tr.Commit();
                            }
                        }
                        catch { }
                    }

                    ed.WriteMessage(string.Format("\n[提示] 成功将对象转换为云线并创建批注 {0} (状态: {1})\n",
                        note.Title,
                        NoteLifecycleStateExtensions.ToDisplayName(note.State)));
                    PaletteManager.Refresh();
                }
            }
            finally
            {
                if (!isPolyAdded && cloudPoly != null)
                {
                    try { cloudPoly.Dispose(); } catch { }
                }
            }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[错误] 对象转换为云线操作失败: " + ex.Message + "\n");
                Logger.Error("ExecuteConvertObject 发生异常", ex);
            }
        }

        [CommandMethod("NOTEADD")]
        public void CmdNoteAdd() => CmdCloudNote();

        [CommandMethod("NOTEQUERY")]
        public void CmdNoteQuery()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc?.Database == null) return;
            Editor ed = doc.Editor;

            var notes = NoteService.Instance.GetAllNotes(doc.Database);
            ed.WriteMessage(string.Format("\n================= 图纸审查批注清单 (共 {0} 条) =================\n", notes.Count));
            if (notes.Count == 0)
            {
                ed.WriteMessage("当前图纸中暂无云线批注，输入 CLOUDNOTE 可新建批注。\n");
                return;
            }

            foreach (var n in notes)
            {
                ed.WriteMessage(string.Format("[{0}] [{1}] {2} (批注者: {3})\n    意见: {4}\n",
                    NoteLifecycleStateExtensions.ToDisplayName(n.State),
                    n.Discipline,
                    n.Title,
                    string.IsNullOrEmpty(n.Assignee) ? "未指派" : n.Assignee,
                    n.Content));
            }
            ed.WriteMessage("=================================================================\n");
        }

        [CommandMethod("NOTELIST")]
        public void CmdNoteList() => CmdNoteQuery();

        [CommandMethod("CNR")]
        [CommandMethod("CNREPLY")]
        [CommandMethod("NOTEREPLY")]
        public void CmdNoteReply()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc?.Database == null) return;
            Editor ed = doc.Editor;

            PromptEntityOptions peo = new PromptEntityOptions("\n请选择要追加回复或流转状态的云线批注图元 (云线/文字/引线): ");
            PromptEntityResult per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return;

            string noteId = null;
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                DBObject obj = tr.GetObject(per.ObjectId, OpenMode.ForRead);
                noteId = XDataHelper.Instance.GetNoteId(obj, AppConstants.XDATA_APP_NAME);
                tr.Commit();
            }

            if (string.IsNullOrEmpty(noteId))
            {
                ed.WriteMessage("\n[提示] 所选图元不是本插件创建的云线批注或未绑定批注数据。\n");
                return;
            }

            NoteRecord note = NoteService.Instance.GetNote(doc.Database, noteId);
            if (note == null)
            {
                ed.WriteMessage("\n[提示] 未检索到对应的批注元数据记录。\n");
                return;
            }

            // 弹出回复输入框（全版本统一深色 Fluent 现代 UI）
            string replyContent;
            NoteLifecycleState targetState;
            string replyAuthor;
            if (NoteUIProvider.Instance.ShowNoteReplyDialog(note, out replyContent, out targetState, out replyAuthor))
            {
                string err;
                if (ReplyService.AddReply(doc.Database, note.NoteId, replyContent, replyAuthor, targetState, out err))
                {
                    ed.WriteMessage(string.Format("\n[提示] 批注 {0} 状态已更新为 {1} (办理人: {2})。\n",
                        note.Title,
                        NoteLifecycleStateExtensions.ToDisplayName(targetState),
                        replyAuthor));
                    PaletteManager.Refresh();
                }
                else
                {
                    ed.WriteMessage("\n[错误] 回复流转失败: " + err + "\n");
                }
            }
        }

        [CommandMethod("NOTEOUT")]
        public void CmdNoteOut()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc?.Database == null) return;
            Editor ed = doc.Editor;

            var notes = NoteService.Instance.GetAllNotes(doc.Database);
            if (notes.Count == 0)
            {
                ed.WriteMessage("\n[提示] 当前图纸暂无任何批注数据，无法导出。\n");
                return;
            }

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Title = "导出批注清单";
                sfd.Filter = "Excel 电子表格 (*.xls)|*.xls|CSV 逗号分隔文本 (*.csv)|*.csv";
                sfd.FileName = string.Format("图纸审查批注_{0:yyyyMMdd_HHmm}.xls", DateTime.Now);

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        if (sfd.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                        {
                            ExportService.ExportToCsv(notes, sfd.FileName);
                        }
                        else
                        {
                            ExportService.ExportToExcelXml(notes, sfd.FileName);
                        }

                        ed.WriteMessage(string.Format("\n[提示] 已导出 {0} 条批注至文件:\n{1}\n", notes.Count, sfd.FileName));
                    }
                    catch (System.Exception ex)
                    {
                        ed.WriteMessage("\n[错误] 导出批注失败: " + ex.Message + "\n");
                    }
                }
            }
        }

        [CommandMethod("NOTEEXPORT")]
        public void CmdNoteExport() => CmdNoteOut();

        [CommandMethod("NOTESUMMARY")]
        [CommandMethod("NOTETABLE")]
        public void CmdNoteSummary()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc?.Database == null) return;
            Editor ed = doc.Editor;

            var notes = NoteService.Instance.GetAllNotes(doc.Database);
            if (notes.Count == 0)
            {
                ed.WriteMessage("\n[提示] 当前图纸暂无批注，无法生成汇总表。\n");
                return;
            }

            PromptPointOptions ppo = new PromptPointOptions("\n请指定批注汇总表左上角放置位置: ");
            PromptPointResult ppr = ed.GetPoint(ppo);
            if (ppr.Status != PromptStatus.OK) return;

            try
            {
                ObjectId tblId = SummaryTableService.CreateSummaryTable(doc.Database, ppr.Value, notes);
                if (!tblId.IsNull)
                {
                    ed.WriteMessage(string.Format("\n[提示] 批注汇总表绘制完成，共收录 {0} 条意见。\n", notes.Count));
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[错误] 生成汇总表异常: " + ex.Message + "\n");
            }
        }

        [CommandMethod("NOTEKB")]
        public void CmdNoteKb()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;

            PromptStringOptions pso = new PromptStringOptions("\n请输入要检索的规范知识库关键词 (直接回车列出全部): ");
            pso.AllowSpaces = true;
            PromptResult pr = ed.GetString(pso);

            string kw = pr.Status == PromptStatus.OK ? pr.StringResult : null;
            var list = KnowledgeBaseService.Search(kw);

            ed.WriteMessage(string.Format("\n================= 审查规范知识库条目 (共 {0} 条) =================\n", list.Count));
            foreach (var item in list)
            {
                ed.WriteMessage(string.Format("[{0}] [{1}] {2}\n    依据: {3}\n    意见: {4}\n",
                    item.Id,
                    item.Discipline,
                    item.Title,
                    item.StandardCode,
                    item.Suggestion));
            }
            ed.WriteMessage("===================================================================\n");
        }

        [CommandMethod("NOTESHOW")]
        public void CmdNoteShow()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc?.Database == null) return;
            Editor ed = doc.Editor;

            var settings = ConfigManager.Instance.CurrentSettings;
            string cloudLayer = !string.IsNullOrEmpty(settings.CloudLayer) ? settings.CloudLayer : "CAD_NOTE_CLOUD";
            string textLayer = !string.IsNullOrEmpty(settings.TextLayer) ? settings.TextLayer : "CAD_NOTE_TEXT";

            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                LayerTable lt = (LayerTable)tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead);
                bool hasCloud = lt.Has(cloudLayer);
                bool hasText = lt.Has(textLayer);

                if (!hasCloud && !hasText)
                {
                    ed.WriteMessage(string.Format("\n[提示] 批注图层 [{0}] 与 [{1}] 均尚未创建。\n", cloudLayer, textLayer));
                }
                else
                {
                    // 若任一图层当前为开启状态，则全部关闭；若均已关闭，则全部开启
                    bool isAnyOn = false;
                    if (hasCloud)
                    {
                        LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(lt[cloudLayer], OpenMode.ForRead);
                        if (!ltr.IsOff) isAnyOn = true;
                    }
                    if (hasText)
                    {
                        LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(lt[textLayer], OpenMode.ForRead);
                        if (!ltr.IsOff) isAnyOn = true;
                    }

                    bool targetIsOff = isAnyOn; // 若有开启的，则全部关闭；若都关闭了，则开启

                    if (hasCloud)
                    {
                        LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(lt[cloudLayer], OpenMode.ForWrite);
                        ltr.IsOff = targetIsOff;
                    }
                    if (hasText)
                    {
                        LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(lt[textLayer], OpenMode.ForWrite);
                        ltr.IsOff = targetIsOff;
                    }

                    string statusStr = targetIsOff ? "关闭 (隐藏)" : "开启 (显示)";
                    ed.WriteMessage(string.Format("\n[提示] 批注双图层状态同步切换为: [云线: {0}, 文字: {1}] -> {2}\n", cloudLayer, textLayer, statusStr));
                }
                tr.Commit();
            }
            doc.SendStringToExecute("._REGEN\n", true, false, false);
        }

        [CommandMethod("CNS")]
        [CommandMethod("CNOTESET")]
        [CommandMethod("NOTESET")]
        public void CmdNoteSet()
        {
            NoteUIProvider.Instance.ShowConfigDialog();
        }

        [CommandMethod("NOTECFG")]
        public void CmdNoteCfg() => CmdNoteSet();

        [CommandMethod("CLOUDNOTECFG")]
        public void CmdCloudNoteCfg() => CmdNoteSet();

        [CommandMethod("CLOUDNOTESET")]
        public void CmdCloudNoteSet() => CmdNoteSet();

        [CommandMethod("CND")]
        [CommandMethod("CNDEL")]
        [CommandMethod("NOTEDEL")]
        public void CmdNoteDel()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc?.Database == null) return;
            Editor ed = doc.Editor;

            PromptEntityOptions peo = new PromptEntityOptions("\n请选择要删除的云线批注图元: ");
            PromptEntityResult per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return;

            string noteId = null;
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                DBObject obj = tr.GetObject(per.ObjectId, OpenMode.ForRead);
                noteId = XDataHelper.Instance.GetNoteId(obj, AppConstants.XDATA_APP_NAME);
                tr.Commit();
            }

            if (!string.IsNullOrEmpty(noteId))
            {
                if (NoteService.Instance.DeleteNote(doc.Database, noteId))
                {
                    ed.WriteMessage("\n[提示] 批注及其关联图元已删除。\n");
                    PaletteManager.Refresh();
                }
            }
            else
            {
                ed.WriteMessage("\n[提示] 所选图元非批注关联对象。\n");
            }
        }

        [CommandMethod("NOTEADAPT")]
        public void CmdNoteAdapt()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc?.Database == null) return;
            Editor ed = doc.Editor;

            double scale = ScaleHelper.GetDrawingScale(doc.Database);
            var s = ConfigManager.Instance.CurrentSettings;
            double arc = ScaleHelper.CalculateArcLength(doc.Database, s.CloudArcLengthFactor);
            double th = ScaleHelper.CalculateTextHeight(doc.Database, s.DefaultTextHeight);

            ed.WriteMessage(string.Format("\n[比例适配诊断]\n  当前绘图比例因子: 1:{0:0.##}\n  推荐云线弧长: {1:0.##}\n  推荐文字字高: {2:0.##}\n",
                scale, arc, th));
        }

        [CommandMethod("NOTECOMPARE")]
        public void CmdNoteCompare()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            CompareLinkService.StartCompare(doc, null);
        }

        [CommandMethod("CNP")]
        [CommandMethod("CNOTEPANEL")]
        [CommandMethod("NOTEPANEL")]
        [CommandMethod("NOTEPALETTE")]
        [CommandMethod("云线批注看板")]
        public void CmdNotePanel()
        {
            PaletteManager.ShowPalette();
        }

        [CommandMethod("NOTEPALETTE")]
        public void CmdNotePalette() => CmdNotePanel();

        [CommandMethod("NOTECOLOR")]
        public void CmdNoteColor()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc?.Database == null) return;
            Editor ed = doc.Editor;

            var notes = NoteService.Instance.GetAllNotes(doc.Database);
            int count = 0;
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                foreach (var n in notes)
                {
                    short color = NoteLifecycleStateExtensions.GetColorIndex(n.State);
                    if (n.Cloud?.EntityHandles != null)
                    {
                        foreach (string h in n.Cloud.EntityHandles)
                        {
                            try
                            {
                                if (XDataHelper.SafeGetObjectId(doc.Database, h, out ObjectId id) && !id.IsNull && !id.IsErased)
                                {
                                    Entity ent = (Entity)tr.GetObject(id, OpenMode.ForWrite);
                                    ent.ColorIndex = color;
                                    count++;
                                }
                            }
                            catch { }
                        }
                    }
                }
                tr.Commit();
            }

            ed.WriteMessage(string.Format("\n[提示] 已按批注生命周期状态刷新 {0} 个图元的显示颜色。\n", count));
            doc.SendStringToExecute("._REGEN\n", true, false, false);
        }

        [CommandMethod("NOTEAISEARCH")]
        public void CmdNoteAiSearch()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc?.Database == null) return;
            Editor ed = doc.Editor;

            PromptStringOptions pso = new PromptStringOptions("\n请输入审查意见草稿或问题描述: ");
            pso.AllowSpaces = true;
            PromptResult pr = ed.GetString(pso);
            if (pr.Status != PromptStatus.OK || string.IsNullOrEmpty(pr.StringResult)) return;

            var matches = SemanticSearchService.MatchKnowledgeBase(pr.StringResult, null, 0.2);
            ed.WriteMessage(string.Format("\n================= 语义审查匹配推荐 (共 {0} 条建议) =================\n", matches.Count));
            for (int i = 0; i < Math.Min(5, matches.Count); i++)
            {
                var m = matches[i];
                ed.WriteMessage(string.Format("[匹配度 {0:P0}] 【{1}】 {2}\n    依据: {3}\n    建议: {4}\n",
                    m.Score,
                    m.Item.Discipline,
                    m.Item.Title,
                    m.Item.StandardCode,
                    m.Item.Suggestion));
            }
            ed.WriteMessage("====================================================================\n");
        }

        [CommandMethod("NOTEAUDIT")]
        public void CmdNoteAudit()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc?.Database == null) return;
            Editor ed = doc.Editor;

            PromptEntityOptions peo = new PromptEntityOptions("\n请选择要追溯防篡改审计日志的批注图元: ");
            PromptEntityResult per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return;

            string noteId = null;
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                DBObject obj = tr.GetObject(per.ObjectId, OpenMode.ForRead);
                noteId = XDataHelper.Instance.GetNoteId(obj, AppConstants.XDATA_APP_NAME);
                tr.Commit();
            }

            if (string.IsNullOrEmpty(noteId))
            {
                ed.WriteMessage("\n[提示] 所选图元未绑定批注数据。\n");
                return;
            }

            NoteRecord note = NoteService.Instance.GetNote(doc.Database, noteId);
            if (note == null || note.AuditLogs == null || note.AuditLogs.Count == 0)
            {
                ed.WriteMessage("\n[提示] 未检索到审计链日志。\n");
                return;
            }

            bool isValid = AuditService.VerifyChain(note.AuditLogs);
            ed.WriteMessage(string.Format("\n================= 批注 【{0}】 SHA256 审计追溯 (链完整性: {1}) =================\n",
                note.SequenceNumber,
                isValid ? "通过 (VALID)" : "异常篡改 (TAMPERED)"));

            foreach (var log in note.AuditLogs)
            {
                ed.WriteMessage(string.Format("[{0:yyyy-MM-dd HH:mm:ss}] 操作人: {1} | 动作: {2}\n    摘要: {3}\n    SHA256: {4}\n    PrevHash: {5}\n",
                    log.Timestamp,
                    log.Operator,
                    log.Action,
                    log.DiffSummary,
                    log.CurrentHash,
                    string.IsNullOrEmpty(log.PreviousHash) ? "GENESIS" : log.PreviousHash));
            }
            ed.WriteMessage("====================================================================================\n");
        }

        [CommandMethod("NOTEPKG")]
        public void CmdNotePkg()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc?.Database == null) return;
            Editor ed = doc.Editor;

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Title = "导出离线审查包";
                sfd.Filter = "审查包文件 (*.cloudnotepkg)|*.cloudnotepkg";
                sfd.FileName = string.Format("ReviewPackage_{0:yyyyMMdd_HHmm}.cloudnotepkg", DateTime.Now);

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    string err;
                    if (ReviewPackageService.ExportPackage(doc.Database, sfd.FileName, out err))
                    {
                        ed.WriteMessage(string.Format("\n[提示] 审查包导出完成: {0}\n", sfd.FileName));
                    }
                    else
                    {
                        ed.WriteMessage("\n[错误] 审查包导出失败: " + err + "\n");
                    }
                }
            }
        }

        [CommandMethod("NOTEIMPORT")]
        public void CmdNoteImport()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc?.Database == null) return;
            Editor ed = doc.Editor;

            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "导入离线审查包";
                ofd.Filter = "审查包文件 (*.cloudnotepkg)|*.cloudnotepkg";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    int count;
                    string err;
                    if (ReviewPackageService.ImportPackage(doc.Database, ofd.FileName, out count, out err))
                    {
                        ed.WriteMessage(string.Format("\n[提示] 导入完成，共载入 {0} 条批注记录。\n", count));
                        PaletteManager.Refresh();
                    }
                    else
                    {
                        ed.WriteMessage("\n[错误] 审查包导入失败: " + err + "\n");
                    }
                }
            }
        }

        [CommandMethod("NOTEBATCH")]
        public void CmdNoteBatch()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;

            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "请选择需要批量扫描审查批注的工程图纸目录";
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    ed.WriteMessage(string.Format("\n[批处理] 开始扫描目录: {0} ...\n", fbd.SelectedPath));
                    var summary = BatchReviewService.ScanDirectory(fbd.SelectedPath);

                    ed.WriteMessage(string.Format("\n================= 批量图纸审查统计报告 =================\n" +
                                                  "  扫描图纸总数: {0} 张\n" +
                                                  "  审查批注总计: {1} 条\n" +
                                                  "  - 待办批注 (Pending): {2} 条\n" +
                                                  "  - 已改批注 (Resolved): {3} 条\n" +
                                                  "  - 已闭环 (Closed): {4} 条\n" +
                                                  "========================================================\n",
                        summary.TotalDrawings,
                        summary.TotalNotes,
                        summary.PendingNotes,
                        summary.ResolvedNotes,
                        summary.ClosedNotes));
                }
            }
        }

        [CommandMethod("NOTECHECK")]
        public void CmdNoteCheck()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc?.Database == null) return;
            Editor ed = doc.Editor;

            int migrated = MigrationService.MigrateLegacyData(doc.Database);
            ed.WriteMessage(string.Format("\n[自检完成] 数据字典结构健康，平滑升级/迁移旧版批注 {0} 条。\n", migrated));
        }

        [CommandMethod("NOTELOG")]
        public void CmdNoteLog()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string logDir = Path.Combine(appData, "InkVerse\\CadAutoCloudNote\\Logs");
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }
                Process.Start("explorer.exe", logDir);
            }
            catch (System.Exception ex)
            {
                AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\n[错误] 打开日志目录失败: " + ex.Message + "\n");
            }
        }
    }
}
