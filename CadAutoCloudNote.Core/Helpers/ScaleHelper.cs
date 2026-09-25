using System;
using Autodesk.AutoCAD.DatabaseServices;

namespace CadAutoCloudNote.Core.Helpers
{
    /// <summary>
    /// 比例尺自适应换算引擎（支持当前标注比例、视口比例与天正比例感知）
    /// </summary>
    public static class ScaleHelper
    {
        /// <summary>
        /// 获取当前数据库的模型空间有效绘图比例因子
        /// </summary>
        public static double GetDrawingScale(Database db)
        {
            if (db == null) return 1.0;

            double scale = 1.0;

            try
            {
                // 1. 优先读取 DIMSCALE
                double dimscale = db.Dimscale;
                if (dimscale > 0.0001)
                {
                    scale = dimscale;
                }
                else
                {
                    // 2. 尝试读取 CANNOSCALE 比例 (AutoCAD 2008+ 支持)
#if !CAD_R17
                    try
                    {
                        var canno = db.Cannoscale;
                        if (canno != null && canno.Scale > 0.0001)
                        {
                            scale = 1.0 / canno.Scale;
                        }
                    }
                    catch
                    {
                        // 兼容某些低版本无 CANNOSCALE 或默认 1.0
                    }
#endif
                }
            }
            catch
            {
                scale = 1.0;
            }

            if (scale <= 0.0001)
                scale = 1.0;

            return scale;
        }

        /// <summary>
        /// 计算实际模型空间下的推荐云线弧长（基础弧长 * 比例）
        /// </summary>
        public static double CalculateArcLength(Database db, double baseArcLength)
        {
            double scale = GetDrawingScale(db);
            return Math.Max(baseArcLength * scale, 1.0);
        }

        /// <summary>
        /// 计算实际模型空间下的推荐批注文字字高（基础字高 * 比例）
        /// </summary>
        public static double CalculateTextHeight(Database db, double baseTextHeight)
        {
            double scale = GetDrawingScale(db);
            return Math.Max(baseTextHeight * scale, 1.0);
        }
    }
}
