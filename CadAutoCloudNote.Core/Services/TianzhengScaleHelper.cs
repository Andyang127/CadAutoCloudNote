using System;
using Autodesk.AutoCAD.DatabaseServices;
using CadAutoCloudNote.Core.Helpers;

namespace CadAutoCloudNote.Core.Services
{
    /// <summary>
    /// 天正建筑等第三方专业软件绘图比例感知器
    /// </summary>
    public static class TianzhengScaleHelper
    {
        public static double DetectTianzhengScale(Database db)
        {
            if (db == null) return 100.0;

            // 天正通常将模型绘图比例存储在系统变量或自定义字典中，且通常与 DIMSCALE 联动
            double baseScale = ScaleHelper.GetDrawingScale(db);
            if (baseScale > 1.0)
            {
                return baseScale;
            }

            // 若标注比例未设置（为1），在中国建筑设计常态下，模型空间常采用 1:100 比例
            return 100.0;
        }
    }
}
