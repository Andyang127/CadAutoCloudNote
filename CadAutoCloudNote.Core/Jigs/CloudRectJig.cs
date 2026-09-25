using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using CadAutoCloudNote.Core.Services;
using Polyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

namespace CadAutoCloudNote.Core.Jigs
{
    /// <summary>
    /// 矩形云线动态绘制 Jig：在鼠标拖拽对角点时实时计算并呈现向外饱满外凸的云线多段线
    /// </summary>
    public class CloudRectJig : DrawJig
    {
        private readonly Point3d _basePt;
        private Point3d _currentPt;
        private readonly double _arcLength;
        private readonly double _bulge;
        private readonly double _width;
        private readonly short _colorIndex;
        private readonly int _style;

        public Point3d CornerPoint => _currentPt;

        public CloudRectJig(Point3d basePt, double arcLength, double bulge, double width, short colorIndex, int style = 1)
        {
            _basePt = basePt;
            _currentPt = basePt;
            _arcLength = arcLength > 0.5 ? arcLength : 5.0;
            double absBulge = Math.Abs(bulge);
            _bulge = -(absBulge > 0.05 ? absBulge : 0.520567);
            _width = width;
            _colorIndex = colorIndex;
            _style = style;
        }

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            JigPromptPointOptions opt = new JigPromptPointOptions("\n请指定云线框对角点: ");
            opt.UserInputControls = UserInputControls.Accept3dCoordinates | UserInputControls.NoZeroResponseAccepted;
            opt.BasePoint = _basePt;
            opt.UseBasePoint = true;

            PromptPointResult res = prompts.AcquirePoint(opt);
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
            double minX = Math.Min(_basePt.X, _currentPt.X);
            double minY = Math.Min(_basePt.Y, _currentPt.Y);
            double maxX = Math.Max(_basePt.X, _currentPt.X);
            double maxY = Math.Max(_basePt.Y, _currentPt.Y);

            double width = maxX - minX;
            double height = maxY - minY;

            if (width < 1e-3 && height < 1e-3)
            {
                return true;
            }

            // 动态限幅保护：防止极端过大范围生成过多圆弧导致顿挫
            double effectiveArc = _arcLength;
            double perimeter = 2 * (width + height);
            int estimatedArcs = (int)(perimeter / effectiveArc);
            if (estimatedArcs > 64)
            {
                effectiveArc = perimeter / 64.0;
            }
            else if (estimatedArcs < 4)
            {
                effectiveArc = perimeter / 4.0;
            }

            using (Polyline poly = NoteService.CreateRectangularCloud(
                new Point3d(minX, minY, 0),
                new Point3d(maxX, maxY, 0),
                effectiveArc,
                string.Empty,
                _colorIndex,
                _bulge,
                _width,
                _style))
            {
                draw.Geometry.Draw(poly);
            }

            return true;
        }
    }
}
