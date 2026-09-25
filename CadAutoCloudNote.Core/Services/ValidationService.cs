using System;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using CadAutoCloudNote.Core.Config;
using CadAutoCloudNote.Core.Models;
using CadAutoCloudNote.Core.Protocols;

namespace CadAutoCloudNote.Core.Services
{
    /// <summary>
    /// 批注数据与业务规则校验服务
    /// </summary>
    public static class ValidationService
    {
        public static bool ValidateNote(NoteRecord note, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (note == null)
            {
                errorMessage = "批注对象不可为空";
                return false;
            }

            if (string.IsNullOrEmpty(note.NoteId))
            {
                errorMessage = "批注唯一标识 NoteId 不可为空";
                return false;
            }

            if (string.IsNullOrEmpty(note.Title))
            {
                errorMessage = "批注标题不可为空";
                return false;
            }

            if (note.Title.Length > 100)
            {
                errorMessage = "批注标题长度不能超过 100 字符";
                return false;
            }

            if (!string.IsNullOrEmpty(note.Content) && note.Content.Length > 5000)
            {
                errorMessage = "批注内容长度不能超过 5000 字符";
                return false;
            }

            return true;
        }

        public static bool ValidateStateTransition(NoteLifecycleState from, NoteLifecycleState to, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (from == to) return true;

            // 状态机规则
            switch (from)
            {
                case NoteLifecycleState.Draft:
                    if (to == NoteLifecycleState.Pending || to == NoteLifecycleState.Closed)
                        return true;
                    break;
                case NoteLifecycleState.Pending:
                    if (to == NoteLifecycleState.Resolved || to == NoteLifecycleState.Rejected || to == NoteLifecycleState.Closed)
                        return true;
                    break;
                case NoteLifecycleState.Resolved:
                    if (to == NoteLifecycleState.Closed || to == NoteLifecycleState.Pending || to == NoteLifecycleState.Rejected)
                        return true;
                    break;
                case NoteLifecycleState.Rejected:
                    if (to == NoteLifecycleState.Pending || to == NoteLifecycleState.Resolved || to == NoteLifecycleState.Closed)
                        return true;
                    break;
                case NoteLifecycleState.Closed:
                    if (to == NoteLifecycleState.Pending || to == NoteLifecycleState.Resolved)
                        return true; // 重新激活或补充修改已改
                    break;
            }

            string fromText = from.ToDisplayName();
            string toText = to.ToDisplayName();
            if (from == NoteLifecycleState.Closed)
            {
                errorMessage = string.Format("当前批注已处于【{0}】状态。如需重新处理，建议流转为【{1}】重新激活。", 
                    fromText, NoteLifecycleState.Pending.ToDisplayName());
            }
            else
            {
                errorMessage = string.Format("当前批注状态为【{0}】，暂不支持直接切换至【{1}】。建议先流转为【{2}】后进行协同处理。", 
                    fromText, toText, NoteLifecycleState.Pending.ToDisplayName());
            }
            return false;
        }

        public static void EnsureLayer(Database db, Transaction tr, string layerName, short colorIndex, bool isPlottable)
        {
            if (db == null || tr == null || string.IsNullOrEmpty(layerName))
                return;

            try
            {
                LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (!lt.Has(layerName))
                {
                    lt.UpgradeOpen();
                    LayerTableRecord ltr = new LayerTableRecord
                    {
                        Name = layerName,
                        Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex),
                        IsPlottable = isPlottable
                    };
                    lt.Add(ltr);
                    tr.AddNewlyCreatedDBObject(ltr, true);
                }
                else
                {
                    // 若图层已存在，检查并同步不打印状态（与全局设置联动）
                    ObjectId layerId = lt[layerName];
                    if (!layerId.IsNull)
                    {
                        LayerTableRecord ltr = tr.GetObject(layerId, OpenMode.ForRead) as LayerTableRecord;
                        if (ltr != null && ltr.IsPlottable != isPlottable)
                        {
                            ltr.UpgradeOpen();
                            ltr.IsPlottable = isPlottable;
                        }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// 确保云线图层与文字图层双双就绪，保证图层物理存在、颜色正确且遵循不打印设置
        /// </summary>
        public static void EnsureNoteLayers(Database db, Transaction tr, NoteSettings settings, NotePriority priority)
        {
            if (db == null || tr == null || settings == null) return;

            string cloudLayer = !string.IsNullOrEmpty(settings.CloudLayer) ? settings.CloudLayer : "CAD_NOTE_CLOUD";
            string textLayer = !string.IsNullOrEmpty(settings.TextLayer) ? settings.TextLayer : "CAD_NOTE_TEXT";
            bool isPlottable = !settings.ForceNonPlotting;

            short cloudColor = settings.EnablePriorityColorLink
                ? (short)settings.GetPriorityColorIndex(priority)
                : (short)settings.CloudColorIndex;
            short textColor = (short)settings.TextColorIndex;

            EnsureLayer(db, tr, cloudLayer, cloudColor, isPlottable);
            EnsureLayer(db, tr, textLayer, textColor, isPlottable);
        }
    }
}
