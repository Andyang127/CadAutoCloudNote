using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Polyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

namespace CadAutoCloudNote.Core.Services
{
    /// <summary>
    /// 曲线对象转换为云线服务：将任意 AutoCAD 几何对象（多段线/圆/椭圆/样条曲线/圆弧/直线）
    /// 高保真离散并重建为饱满外凸、书法笔锋变截面的轻量多段线云线
    /// </summary>
    public static class CurveToCloudService
    {
        /// <summary>
        /// 将指定 Curve 对象转换为云线 Polyline
        /// </summary>
        /// <param name="curve">源曲线对象</param>
        /// <param name="arcLength">目标弦弧长</param>
        /// <param name="bulgeMagnitude">凸度绝对值幅度（标准推荐 ~0.520567）</param>
        /// <param name="cloudStyle">云线样式: 0=普通等宽, 1=书法笔锋</param>
        /// <param name="constantWidth">全局线宽（仅普通样式有效）</param>
        /// <param name="layer">目标图层</param>
        /// <param name="colorIndex">颜色索引</param>
        /// <param name="reverseDirection">是否反转圆弧凹凸方向（默认 false 为饱满外凸）</param>
        /// <returns>构建生成的云线 Polyline，若失败则返回 null</returns>
        public static Polyline ConvertCurveToCloud(
            Curve curve,
            double arcLength,
            double bulgeMagnitude,
            int cloudStyle = 1,
            double constantWidth = 0.0,
            string layer = null,
            short colorIndex = 1,
            bool reverseDirection = false)
        {
            if (curve == null) return null;

            double targetArc = arcLength > 0.5 ? arcLength : 5.0;
            double absBulge = Math.Abs(bulgeMagnitude > 0.05 ? bulgeMagnitude : 0.520567);

            // 1. 针对 Circle 单独优化：解析几何中心与半径，全周长均分外凸弧
            if (curve is Circle circle)
            {
                return ConvertCircleToCloud(circle, targetArc, absBulge, cloudStyle, constantWidth, layer, colorIndex, reverseDirection);
            }

            // 2. 针对轻量 2D Polyline 单独优化：若含圆角/圆弧则连续采样，纯直线段多边形严格保留特征拐角
            if (curve is Polyline poly)
            {
                if (poly.HasBulges)
                {
                    return ConvertGenericCurveToCloud(curve, targetArc, absBulge, cloudStyle, constantWidth, layer, colorIndex, reverseDirection);
                }
                return ConvertPolylineToCloud(poly, targetArc, absBulge, cloudStyle, constantWidth, layer, colorIndex, reverseDirection);
            }

            // 3. 针对 Ellipse, Spline, Arc, Line 等通用 Curve：基于参数化弧长采样
            return ConvertGenericCurveToCloud(curve, targetArc, absBulge, cloudStyle, constantWidth, layer, colorIndex, reverseDirection);
        }

        /// <summary>
        /// 圆转换为高精度径向对称外凸云线
        /// </summary>
        private static Polyline ConvertCircleToCloud(
            Circle circle,
            double arcLength,
            double absBulge,
            int cloudStyle,
            double constantWidth,
            string layer,
            short colorIndex,
            bool reverseDirection)
        {
            double r = circle.Radius;
            if (r < 1e-4) return null;

            double perimeter = 2.0 * Math.PI * r;
            int count = Math.Max(6, (int)Math.Round(perimeter / arcLength));
            double stepAngle = 2.0 * Math.PI / (double)count;
            Point3d center = circle.Center;

            double startWidth = 0.0;
            double endWidth = 0.0;
            if (cloudStyle == 1)
            {
                double strokeWidth = Math.Max(arcLength * 0.18, constantWidth > 0 ? constantWidth * 2.0 : 0.0);
                startWidth = 0.0;
                endWidth = strokeWidth;
            }

            Polyline result = new Polyline();
            if (!string.IsNullOrEmpty(layer)) result.Layer = layer;
            result.ColorIndex = colorIndex;
            if (cloudStyle == 0 && constantWidth > 0) result.ConstantWidth = constantWidth;

            // 顺时针顺序布设顶点，配合负凸度 -absBulge 保证严格向外隆起
            double b = reverseDirection ? absBulge : -absBulge;

            for (int i = 0; i < count; i++)
            {
                double angle = -i * stepAngle; // 顺时针递减
                double x = center.X + r * Math.Cos(angle);
                double y = center.Y + r * Math.Sin(angle);
                result.AddVertexAt(i, new Point2d(x, y), b, startWidth, endWidth);
            }

            result.Closed = true;
            return result;
        }

        /// <summary>
        /// 2D 多段线转换为云线：保留原始几何拐角顶点，保证多边形转角平滑严密闭合
        /// </summary>
        private static Polyline ConvertPolylineToCloud(
            Polyline poly,
            double arcLength,
            double absBulge,
            int cloudStyle,
            double constantWidth,
            string layer,
            short colorIndex,
            bool reverseDirection)
        {
            int numVerts = poly.NumberOfVertices;
            if (numVerts < 2) return null;

            bool isClosed = poly.Closed;

            // 提取所有顶点
            List<Point2d> origVerts = new List<Point2d>();
            for (int i = 0; i < numVerts; i++)
            {
                origVerts.Add(poly.GetPoint2dAt(i));
            }

            // 计算多边形有向面积，确保闭合时统一为顺时针拓扑
            if (isClosed)
            {
                double area = ComputeSignedArea(origVerts);
                if (area > 0) // 逆时针 -> 翻转为顺时针
                {
                    origVerts.Reverse();
                }
            }

            double startWidth = 0.0;
            double endWidth = 0.0;
            if (cloudStyle == 1)
            {
                double strokeWidth = Math.Max(arcLength * 0.18, constantWidth > 0 ? constantWidth * 2.0 : 0.0);
                startWidth = 0.0;
                endWidth = strokeWidth;
            }

            Polyline result = new Polyline();
            if (!string.IsNullOrEmpty(layer)) result.Layer = layer;
            result.ColorIndex = colorIndex;
            if (cloudStyle == 0 && constantWidth > 0) result.ConstantWidth = constantWidth;

            double b = reverseDirection ? absBulge : -absBulge;
            int vIdx = 0;
            int segLimit = isClosed ? origVerts.Count : origVerts.Count - 1;

            for (int i = 0; i < segLimit; i++)
            {
                Point2d p1 = origVerts[i];
                Point2d p2 = origVerts[(i + 1) % origVerts.Count];
                double dist = p1.GetDistanceTo(p2);
                if (dist < 1e-4) continue;

                int segCount = Math.Max(1, (int)Math.Round(dist / arcLength));
                Vector2d dir = (p2 - p1) / (double)segCount;

                for (int j = 0; j < segCount; j++)
                {
                    Point2d pt = p1 + dir * (double)j;
                    result.AddVertexAt(vIdx++, pt, b, startWidth, endWidth);
                }
            }

            if (isClosed)
            {
                result.Closed = true;
            }
            else
            {
                // 非闭合多段线需补齐最后一个端点
                Point2d lastPt = origVerts[origVerts.Count - 1];
                result.AddVertexAt(vIdx++, lastPt, 0, 0, 0);
            }

            return result;
        }

        /// <summary>
        /// 通用曲线（椭圆、样条曲线、圆弧、直线）基于参数化距离等距离散为云线
        /// </summary>
        private static Polyline ConvertGenericCurveToCloud(
            Curve curve,
            double arcLength,
            double absBulge,
            int cloudStyle,
            double constantWidth,
            string layer,
            short colorIndex,
            bool reverseDirection)
        {
            double startDist = curve.GetDistanceAtParameter(curve.StartParam);
            double endDist = curve.GetDistanceAtParameter(curve.EndParam);
            double totalLength = Math.Abs(endDist - startDist);
            if (totalLength < 1e-3) return null;

            bool isClosed = curve.Closed;
            // 判定两端点是否物理重合（部分 Spline/Polyline 虽 Closed=false 但首尾坐标闭合）
            Point3d pStart = curve.StartPoint;
            Point3d pEnd = curve.EndPoint;
            if (pStart.DistanceTo(pEnd) < 1e-3)
            {
                isClosed = true;
            }

            int count = Math.Max(isClosed ? 6 : 2, (int)Math.Round(totalLength / arcLength));
            double stepDist = totalLength / (double)count;

            List<Point2d> points = new List<Point2d>();
            for (int i = 0; i < count; i++)
            {
                double d = Math.Min(totalLength - 1e-6, i * stepDist);
                Point3d pt = curve.GetPointAtDist(d);
                points.Add(new Point2d(pt.X, pt.Y));
            }

            if (isClosed)
            {
                double area = ComputeSignedArea(points);
                if (area > 0) // 逆时针 -> 翻转为顺时针
                {
                    points.Reverse();
                }
            }

            double startWidth = 0.0;
            double endWidth = 0.0;
            if (cloudStyle == 1)
            {
                double strokeWidth = Math.Max(arcLength * 0.18, constantWidth > 0 ? constantWidth * 2.0 : 0.0);
                startWidth = 0.0;
                endWidth = strokeWidth;
            }

            Polyline result = new Polyline();
            if (!string.IsNullOrEmpty(layer)) result.Layer = layer;
            result.ColorIndex = colorIndex;
            if (cloudStyle == 0 && constantWidth > 0) result.ConstantWidth = constantWidth;

            double b = reverseDirection ? absBulge : -absBulge;

            for (int i = 0; i < points.Count; i++)
            {
                result.AddVertexAt(i, points[i], b, startWidth, endWidth);
            }

            if (isClosed)
            {
                result.Closed = true;
            }
            else
            {
                // 非闭合曲线追加末尾顶点以形成完整路径
                result.AddVertexAt(points.Count, new Point2d(pEnd.X, pEnd.Y), 0, 0, 0);
            }

            return result;
        }

        /// <summary>
        /// 鞋带公式计算多边形有向面积：正值代表逆时针(CCW)，负值代表顺时针(CW)
        /// </summary>
        public static double ComputeSignedArea(List<Point2d> pts)
        {
            if (pts == null || pts.Count < 3) return 0.0;

            double area = 0.0;
            int n = pts.Count;
            for (int i = 0; i < n; i++)
            {
                Point2d p1 = pts[i];
                Point2d p2 = pts[(i + 1) % n];
                area += (p1.X * p2.Y - p2.X * p1.Y);
            }
            return area * 0.5;
        }

        /// <summary>
        /// 安全获取 Polyline 顶点的包围盒（全版本兼容，避免低版本 Bounds/GeometricExtents 异常）
        /// </summary>
        public static Extents3d GetPolylineExtents(Polyline poly)
        {
            if (poly == null || poly.NumberOfVertices == 0)
                return new Extents3d(Point3d.Origin, Point3d.Origin);

            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            for (int i = 0; i < poly.NumberOfVertices; i++)
            {
                Point2d pt = poly.GetPoint2dAt(i);
                if (pt.X < minX) minX = pt.X;
                if (pt.Y < minY) minY = pt.Y;
                if (pt.X > maxX) maxX = pt.X;
                if (pt.Y > maxY) maxY = pt.Y;
            }
            return new Extents3d(new Point3d(minX, minY, 0), new Point3d(maxX, maxY, 0));
        }
    }
}
