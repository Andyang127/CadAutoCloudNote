using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using CadAutoCloudNote.Core.Services;
using Polyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

namespace CadAutoCloudNote.Core.Jigs
{
    /// <summary>
    /// 多边形云线交互绘制 Jig (对齐 AutoCAD 原生 REVCLOUD 多边形模式)
    /// 连续指定角点，实时以外凸书法云弧呈现动态橡皮筋和已成型边，支持回车闭合、输入 C 闭合及 U 放弃
    /// </summary>
    public class PolygonCloudJig : DrawJig
    {
        private readonly List<Point2d> _vertices = new List<Point2d>();
        private Point3d _currentPt;
        private readonly double _arcLength;
        private readonly double _bulge;
        private readonly double _width;
        private readonly short _colorIndex;
        private readonly int _style;
        private bool _isClosed = false;

        public bool IsClosed => _isClosed;
        public int VertexCount => _vertices.Count;
        public Polyline ResultPolyline { get; private set; }

        public PromptStatus LastPromptStatus { get; private set; } = PromptStatus.OK;
        public string LastKeyword { get; private set; } = string.Empty;

        public PolygonCloudJig(Point3d startPt, double arcLength, double bulge, double width, short colorIndex, int style = 1)
        {
            _currentPt = startPt;
            _arcLength = arcLength > 0.5 ? arcLength : 5.0;
            double absBulge = Math.Abs(bulge);
            _bulge = absBulge > 0.05 ? absBulge : 0.520567;
            _width = width;
            _colorIndex = colorIndex;
            _style = style;

            _vertices.Add(new Point2d(startPt.X, startPt.Y));
        }

        public void AddCurrentPoint()
        {
            Point2d cur2d = new Point2d(_currentPt.X, _currentPt.Y);
            if (_vertices.Count == 0 || cur2d.GetDistanceTo(_vertices[_vertices.Count - 1]) > 1e-4)
            {
                _vertices.Add(cur2d);
            }
        }

        public void RemoveLastPoint()
        {
            if (_vertices.Count > 1)
            {
                _vertices.RemoveAt(_vertices.Count - 1);
                Point2d last = _vertices[_vertices.Count - 1];
                _currentPt = new Point3d(last.X, last.Y, 0);
            }
        }

        public bool Close()
        {
            if (_vertices.Count < 3) return false;

            _isClosed = true;
            ResultPolyline = NoteService.CreatePolygonCloud(
                _vertices,
                _arcLength,
                string.Empty,
                _colorIndex,
                _bulge,
                _width,
                _style);

            return ResultPolyline != null;
        }

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            JigPromptPointOptions opt = new JigPromptPointOptions("\n请指定下一个角点: ");
            if (_vertices.Count >= 2)
            {
                opt.Keywords.Add("C", "C", "闭合(C)");
                opt.Keywords.Add("U", "U", "放弃(U)");
                opt.Keywords.Default = "C";
            }

            opt.UserInputControls = UserInputControls.Accept3dCoordinates | UserInputControls.NullResponseAccepted;
            Point2d lastPt = _vertices[_vertices.Count - 1];
            opt.BasePoint = new Point3d(lastPt.X, lastPt.Y, 0);
            opt.UseBasePoint = true;

            PromptPointResult res = prompts.AcquirePoint(opt);
            LastPromptStatus = res.Status;
            LastKeyword = res.StringResult;

            if (res.Status == PromptStatus.OK)
            {
                if (res.Value.DistanceTo(_currentPt) < 1e-4)
                {
                    return SamplerStatus.NoChange;
                }
                _currentPt = res.Value;
                return SamplerStatus.OK;
            }

            return SamplerStatus.Cancel;
        }

        protected override bool WorldDraw(WorldDraw draw)
        {
            if (_vertices.Count == 0) return true;

            List<Point2d> tempPts = new List<Point2d>(_vertices);
            Point2d cur2d = new Point2d(_currentPt.X, _currentPt.Y);
            if (cur2d.GetDistanceTo(tempPts[tempPts.Count - 1]) > 1e-3)
            {
                tempPts.Add(cur2d);
            }

            if (tempPts.Count >= 3)
            {
                // 动态构建外凸闭合云线预览
                using (Polyline poly = NoteService.CreatePolygonCloud(
                    tempPts,
                    _arcLength,
                    string.Empty,
                    _colorIndex,
                    _bulge,
                    _width,
                    _style))
                {
                    if (poly != null)
                    {
                        draw.Geometry.Draw(poly);
                    }
                }
            }
            else if (tempPts.Count == 2)
            {
                // 2 个点时，绘制一条外凸云线段及直观提示
                double startWidth = 0.0;
                double endWidth = 0.0;
                if (_style == 1)
                {
                    double strokeWidth = Math.Max(_arcLength * 0.18, _width > 0 ? _width * 2.0 : 0.0);
                    startWidth = 0.0;
                    endWidth = strokeWidth;
                }

                using (Polyline segPoly = new Polyline())
                {
                    segPoly.ColorIndex = _colorIndex;
                    if (_style == 0 && _width > 0) segPoly.ConstantWidth = _width;

                    Point2d p0 = tempPts[0];
                    Point2d p1 = tempPts[1];
                    double dist = p0.GetDistanceTo(p1);
                    int segs = Math.Max(1, (int)Math.Round(dist / _arcLength));
                    Vector2d dir = (p1 - p0) / (double)segs;

                    for (int i = 0; i < segs; i++)
                    {
                        Point2d v = p0 + dir * (double)i;
                        segPoly.AddVertexAt(i, v, -_bulge, startWidth, endWidth);
                    }
                    segPoly.AddVertexAt(segs, p1, 0, 0, 0);

                    draw.Geometry.Draw(segPoly);
                }
            }

            return true;
        }
    }
}
