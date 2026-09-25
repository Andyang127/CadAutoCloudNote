using System;
using Autodesk.AutoCAD.ApplicationServices;
#if CAD_R17 || CAD_R18
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
#else
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
#endif

namespace CadAutoCloudNote.Core.Services
{
    /// <summary>
    /// 图纸跨版本比对与差异云线关联服务
    /// </summary>
    public static class CompareLinkService
    {
        public static void StartCompare(Document doc, string targetDwgPath)
        {
            if (doc == null) return;

#if CAD_R23 || CAD_R24 || CAD_R25 || CAD_R26 || CAD_R27
            if (!string.IsNullOrEmpty(targetDwgPath))
            {
                // AutoCAD 2019+ 原生支持 COMPARE 命令
                doc.SendStringToExecute(string.Format("._COMPARE \"{0}\"\n", targetDwgPath.Replace("\\", "/")), true, false, false);
            }
            else
            {
                doc.SendStringToExecute("._COMPARE\n", true, false, false);
            }
#else
            doc.Editor.WriteMessage("\n[提示] 原生 DWG 图纸内核比对命令要求 AutoCAD 2019 及以上版本。当前版本可通过导出审查包 (NOTEPKG) 进行人工校核对比。\n");
#endif
        }
    }
}
