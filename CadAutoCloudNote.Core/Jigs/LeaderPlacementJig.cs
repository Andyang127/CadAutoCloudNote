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
    /// 批注引线与文字放置 Jig：在指定文字位置时实时拉出动态橡皮筋引线与文字预览外框
    /// </summary>
    public class LeaderPlacementJig : DrawJig
    {
        private readonly List<Extents3d> _cloudBoxes;
        private readonly List<Polyline> _cloudPolylines;
        private Point3d _currentPt;
        private readonly double _textHeight;
        private readonly short _colorIndex;
        private readonly string _previewText;
        private readonly string _previewContent;
        private readonly bool _isSimpleMode;
        private readonly int _leaderType;
        private readonly int _sequenceNumber;
        private readonly double _arrowSize;

        public Point3d TextPoint => _currentPt;

        public LeaderPlacementJig(
            List<Extents3d> cloudBoxes,
            Point3d initialPt,
            double textHeight,
            short colorIndex,
            string previewText = "",
            bool isSimpleMode = false,
            int leaderType = 0,
            int sequenceNumber = 1,
            double arrowSize = 2.5,
            List<Polyline> cloudPolylines = null,
            string previewContent = "")
        {
            _cloudBoxes = cloudBoxes ?? new List<Extents3d>();
            _cloudPolylines = cloudPolylines;
            _currentPt = initialPt;
            _textHeight = textHeight > 0 ? textHeight : 3.5;
            _colorIndex = colorIndex;
            _previewText = string.IsNullOrEmpty(previewText) ? $"待修改（{DateTime.Now:yy年MM月dd日}）" : previewText;
            _previewContent = previewContent ?? string.Empty;
            _isSimpleMode = isSimpleMode;
            _leaderType = leaderType;
            _sequenceNumber = sequenceNumber > 0 ? sequenceNumber : 1;
            _arrowSize = arrowSize > 0 ? arrowSize : 2.5;
        }

        public string KeywordResult { get; private set; }

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            string promptMsg = _isSimpleMode ? "\n请指定极简批注放置点 或 [快捷短语1-9(S)/输入文字(T)]: " : "\n请指定批注文字放置点: ";
            JigPromptPointOptions opt = new JigPromptPointOptions(promptMsg);
            opt.UserInputControls = UserInputControls.Accept3dCoordinates | UserInputControls.NoZeroResponseAccepted;
            if (_isSimpleMode)
            {
                opt.Keywords.Add("S", "S", "快捷短语1-9(S)");
                opt.Keywords.Add("T", "T", "输入文字(T)");
            }

            PromptPointResult res = prompts.AcquirePoint(opt);
            if (res.Status == PromptStatus.Keyword)
            {
                KeywordResult = res.StringResult;
                return SamplerStatus.Cancel;
            }
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
            // 0. 绘制所有关联的云线框（确保在引线与文字徽标放置的动态交互中，云线框始终清晰呈现）
            if (_cloudPolylines != null)
            {
                foreach (var poly in _cloudPolylines)
                {
                    if (poly != null)
                    {
                        draw.SubEntityTraits.Color = _colorIndex;
                        draw.Geometry.Draw(poly);
                    }
                }
            }

            if (_cloudBoxes.Count == 0) return true;

            // 计算水平托线几何参数
            double triH = Math.Max(2.0, _textHeight) * 1.8;
            double triW = triH / 0.866025;
            double textGap = _textHeight * 0.4;
            int seqForText = _isSimpleMode ? 0 : _sequenceNumber;
            double landingLength = NoteService.CalculateSimpleNoteLandingLength(_previewText, _textHeight, seqForText, _isSimpleMode, _previewContent);

            Point3d primaryEdgePt = _cloudBoxes.Count > 0 ? NoteService.GetClosestPointOnCloud(_cloudBoxes[0].MinPoint, _cloudBoxes[0].MaxPoint, _currentPt) : _currentPt;
            bool isLeft = _currentPt.X < primaryEdgePt.X;

            // 1. 为每个云线框绘制实时引线 (0=箭头, 1=圆点, 2=无引线)
            if (_leaderType != 2)
            {
                Point3d kneePt = _currentPt;

                for (int bIdx = 0; bIdx < _cloudBoxes.Count; bIdx++)
                {
                    var box = _cloudBoxes[bIdx];
                    Point3d edgePt = NoteService.GetClosestPointOnCloud(box.MinPoint, box.MaxPoint, kneePt);
                    Point3d? landingEnd = null;
                    if (bIdx == 0)
                    {
                        landingEnd = isLeft ? new Point3d(_currentPt.X - landingLength, _currentPt.Y, 0) : new Point3d(_currentPt.X + landingLength, _currentPt.Y, 0);
                    }

                    using (Polyline leaderPoly = NoteService.CreateLeaderEntity(edgePt, kneePt, "0", _colorIndex, _leaderType, _arrowSize, landingEnd))
                    {
                        if (leaderPoly != null)
                        {
                            draw.SubEntityTraits.Color = _colorIndex;
                            draw.Geometry.Draw(leaderPoly);
                        }
                    }
                }
            }

            // 2. 预览品红等边小三角徽标（节点重心与拐点对齐）
            using (Polyline tri = NoteService.CreateSimpleNoteTriangle(_currentPt, _textHeight, "0", 6))
            {
                draw.SubEntityTraits.Color = 6;
                draw.Geometry.Draw(tri);
            }

            // 3. 预览两行式图面文字（快速批注为上下，非快速批注第一行为标题+时间，第二行为详情）
            using (MText previewMText = new MText())
            {
                double contentWidth = NoteService.CalculateNoteContentWidth(_previewText, _textHeight, seqForText, _isSimpleMode, _previewContent);
                previewMText.TextHeight = _textHeight;
                previewMText.ColorIndex = _colorIndex;
                previewMText.LineSpacingStyle = LineSpacingStyle.AtLeast;
                previewMText.LineSpacingFactor = 1.35;
                bool hasLine2 = _isSimpleMode || !string.IsNullOrEmpty(_previewContent);
                if (hasLine2)
                {
                    previewMText.Attachment = AttachmentPoint.MiddleLeft;
                    previewMText.Location = isLeft
                        ? new Point3d(_currentPt.X - (triW / 2.0) - textGap - contentWidth, _currentPt.Y, 0)
                        : new Point3d(_currentPt.X + (triW / 2.0) + textGap, _currentPt.Y, 0);
                }
                else
                {
                    previewMText.Attachment = AttachmentPoint.BottomLeft;
                    previewMText.Location = isLeft
                        ? new Point3d(_currentPt.X - (triW / 2.0) - textGap - contentWidth, _currentPt.Y + _textHeight * 0.18, 0)
                        : new Point3d(_currentPt.X + (triW / 2.0) + textGap, _currentPt.Y + _textHeight * 0.18, 0);
                }
                previewMText.Contents = NoteService.FormatNoteContents(seqForText, _previewText, _previewContent, null, _isSimpleMode);
                draw.SubEntityTraits.Color = _colorIndex;
                draw.Geometry.Draw(previewMText);
            }

            return true;
        }
    }
}
