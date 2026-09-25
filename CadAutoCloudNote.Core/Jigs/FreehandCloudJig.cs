using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using Polyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

namespace CadAutoCloudNote.Core.Jigs
{
    /// <summary>
    /// 徒手画云线 DrawJig 引擎（对齐 AutoCAD 原生 REVCLOUD 徒手模式）
    /// 鼠标自由移动过程中，自动根据弧长步长追加外凸云弧，并在靠近起点时自动闭合
    /// </summary>
    public class FreehandCloudJig : DrawJig
    {
        private readonly Point3d _startPt;
        private Point3d _currentPt;
        private readonly double _arcLength;
        private readonly double _bulge;
        private readonly double _width;
        private readonly short _colorIndex;
        private readonly int _style;
        private readonly List<Point2d> _vertices = new List<Point2d>();
        private bool _isClosed = false;

        public bool IsClosed => _isClosed;
        public Polyline ResultPolyline { get; private set; }

        public FreehandCloudJig(Point3d startPt, double arcLength, double bulge, double width, short colorIndex, int style = 1)
        {
            _startPt = startPt;
            _currentPt = startPt;
            _arcLength = arcLength > 0.5 ? arcLength : 5.0;
            double absBulge = Math.Abs(bulge);
            _bulge = -(absBulge > 0.05 ? absBulge : 0.520567);
            _width = width;
            _colorIndex = colorIndex;
            _style = style;

            _vertices.Add(new Point2d(startPt.X, startPt.Y));
        }

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            JigPromptPointOptions opt = new JigPromptPointOptions("\n引导十字光标环绕批注区域绘制云线 (接近起点自动闭合) [完成(Enter)]: ");
            opt.UserInputControls = UserInputControls.Accept3dCoordinates | UserInputControls.NoZeroResponseAccepted;

            PromptPointResult res = prompts.AcquirePoint(opt);
            if (res.Status == PromptStatus.OK)
            {
                if (res.Value.DistanceTo(_currentPt) < 1e-4)
                {
                    return SamplerStatus.NoChange;
                }

                _currentPt = res.Value;
                Point2d lastPt = _vertices[_vertices.Count - 1];
                Point2d curPt2d = new Point2d(_currentPt.X, _currentPt.Y);
                double dist = lastPt.GetDistanceTo(curPt2d);

                // 1. 自动闭合判定：顶点数 >= 3 且光标接近起点（距离 < 1.5 倍弧长）
                Point2d p0 = _vertices[0];
                double distToStart = curPt2d.GetDistanceTo(p0);
                if (_vertices.Count >= 3 && distToStart < _arcLength * 1.5)
                {
                    _isClosed = true;
                    BuildFinalPolyline();
                    return SamplerStatus.Cancel; // 达到闭合条件，结束采样
                }

                // 2. 距离达到步长时追加新顶点
                if (dist >= _arcLength)
                {
                    _vertices.Add(curPt2d);
                }

                return SamplerStatus.OK;
            }

            if (res.Status == PromptStatus.None || res.Status == PromptStatus.Keyword)
            {
                if (_vertices.Count >= 3)
                {
                    _isClosed = true;
                    BuildFinalPolyline();
                    return SamplerStatus.Cancel;
                }
            }

            return SamplerStatus.Cancel;
        }

        protected override bool WorldDraw(WorldDraw draw)
        {
            if (_vertices.Count == 0) return true;

            double startWidth = 0.0;
            double endWidth = 0.0;
            if (_style == 1) // 书法笔锋样式
            {
                double strokeWidth = Math.Max(_arcLength * 0.18, _width > 0 ? _width * 2.0 : 0.0);
                startWidth = 0.0;
                endWidth = strokeWidth;
            }

            using (Polyline poly = new Polyline())
            {
                poly.ColorIndex = _colorIndex;
                if (_style == 0 && _width > 0) poly.ConstantWidth = _width;

                for (int i = 0; i < _vertices.Count; i++)
                {
                    poly.AddVertexAt(i, _vertices[i], _bulge, startWidth, endWidth);
                }

                // 连接到当前鼠标位置的动态橡皮筋段
                poly.AddVertexAt(_vertices.Count, new Point2d(_currentPt.X, _currentPt.Y), 0, 0, 0);

                draw.SubEntityTraits.Color = _colorIndex;
                draw.Geometry.Draw(poly);
            }

            return true;
        }

        public void BuildFinalPolyline()
        {
            if (_vertices.Count < 3) return;

            double startWidth = 0.0;
            double endWidth = 0.0;
            if (_style == 1)
            {
                double strokeWidth = Math.Max(_arcLength * 0.18, _width > 0 ? _width * 2.0 : 0.0);
                startWidth = 0.0;
                endWidth = strokeWidth;
            }

            Polyline poly = new Polyline();
            poly.ColorIndex = _colorIndex;
            if (_style == 0 && _width > 0) poly.ConstantWidth = _width;

            for (int i = 0; i < _vertices.Count; i++)
            {
                poly.AddVertexAt(i, _vertices[i], _bulge, startWidth, endWidth);
            }
            poly.Closed = true;
            ResultPolyline = poly;
        }
    }
}
