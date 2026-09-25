#if USE_WPF
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using CadAutoCloudNote.Core.Config;
using CadAutoCloudNote.Core.Helpers;
using CadAutoCloudNote.Core.Models;
using CadAutoCloudNote.Core.Protocols;
using CadAutoCloudNote.Core.Services;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using AcDb = Autodesk.AutoCAD.DatabaseServices;

namespace CadAutoCloudNote.Core.UI.Wpf
{
    /// <summary>
    /// 颜色下拉项实体
    /// </summary>
    public class ColorItem
    {
        public string Name { get; set; }
        public int ColorIndex { get; set; }
        public Brush Brush { get; set; }
        public override string ToString() => Name;
    }

    /// <summary>
    /// 现代化 Fluent 暗黑/自适应风设置窗口
    /// </summary>
    public partial class ConfigWindow : Window
    {
        private bool _isInitializing = true;
        private bool _isUpdatingFromCode = false;

        public ConfigWindow()
        {
            InitializeComponent();
            try
            {
                var iconSource = IconHelper.GetAppImageSource();
                if (iconSource != null) this.Icon = iconSource;
            }
            catch { }
            ApplyAutoCadSmartTheme();
            InitComboBoxes();
            LoadSettingsToUI();
            RefreshKnowledgeList();
            _isInitializing = false;
            Loaded += (s, e) => UpdatePreview();
            if (CanvasCloudPreview != null)
            {
                CanvasCloudPreview.SizeChanged += (s, e) => UpdatePreview();
            }
            UpdatePreview();

            this.PreviewKeyDown += ConfigWindow_PreviewKeyDown;
        }

        private void ConfigWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F1)
            {
                e.Handled = true;
                HelpService.OpenReadme();
                return;
            }

            if (e.Key == Key.Enter || e.Key == Key.Space)
            {
                var focused = Keyboard.FocusedElement;
                if (focused is System.Windows.Controls.Primitives.TextBoxBase || focused is PasswordBox)
                {
                    return;
                }
                if (focused is ComboBox cb && cb.IsDropDownOpen)
                {
                    return;
                }

                e.Handled = true;
                BtnSaveAndCreate_Click(this, new RoutedEventArgs());
            }
        }

        private bool _isDarkTheme = true;

        private void ApplyAutoCadSmartTheme()
        {
            bool isDarkTheme = true;
            try
            {
#if CAD_R17 || CAD_R18 || CAD_R19
                isDarkTheme = false;
#elif !CAD_TEST
                if (AcApp.Version.Major < 20) isDarkTheme = false;
                else
                {
                    object themeVar = AcApp.GetSystemVariable("COLORTHEME");
                    if (themeVar != null && Convert.ToInt16(themeVar) == 1) isDarkTheme = false;
                }
#else
                isDarkTheme = false;
#endif
            }
            catch { isDarkTheme = false; }

            _isDarkTheme = isDarkTheme;

            if (!isDarkTheme)
            {
                this.Resources["WindowBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F4F6F9"));
                this.Resources["CardBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
                this.Resources["TitleBarBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E9F0"));
                this.Resources["NavBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EDF1F7"));
                this.Resources["TextPrimary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B"));
                this.Resources["TextSecondary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));
                this.Resources["BorderLine"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1"));
                this.Resources["InputBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
                this.Resources["ButtonBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0"));
                this.Resources["ButtonHover"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1"));
                this.Resources["AccentColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0070D2"));
                this.Resources["AccentHover"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#005FB2"));
                this.Resources["AccentGlow"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D0E8FF"));
                this.Resources["ThumbNormalBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));
                this.Resources["ThumbHoverBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));
                this.Resources["SubCardBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EDF2F7"));
                this.Resources["DeepCardBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC"));
                this.Resources["ListBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
                this.Resources["PreviewCardBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9"));
                this.Resources["BadgeBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0F2FE"));
                this.Resources["BadgeBorder"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BAE6FD"));
                this.Resources["ChipBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0F2FE"));
                this.Resources["ChipBorder"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0284C7"));
                this.Resources["ChipDisabledBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9"));
            }
        }

        private void InitComboBoxes()
        {
            // 带色块的 AutoCAD 标准索引颜色下拉
            ColorItem[] colorItems = new ColorItem[]
            {
                new ColorItem { Name = "红色", ColorIndex = 1, Brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFF4444")) },
                new ColorItem { Name = "黄色", ColorIndex = 2, Brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFD700")) },
                new ColorItem { Name = "绿色", ColorIndex = 3, Brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF00E676")) },
                new ColorItem { Name = "青色", ColorIndex = 4, Brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF00E5FF")) },
                new ColorItem { Name = "蓝色", ColorIndex = 5, Brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF2979FF")) },
                new ColorItem { Name = "洋红", ColorIndex = 6, Brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFF4081")) },
                new ColorItem { Name = "黑白", ColorIndex = 7, Brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF0F0F0")) },
                new ColorItem { Name = "随层", ColorIndex = 256, Brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFAAAAAA")) }
            };

            CmbCloudColor.Items.Clear();
            CmbTextColor.Items.Clear();
            if (CmbColorImportant != null) CmbColorImportant.Items.Clear();
            if (CmbColorUrgent != null) CmbColorUrgent.Items.Clear();

            foreach (var ci in colorItems)
            {
                CmbCloudColor.Items.Add(ci);
                CmbTextColor.Items.Add(new ColorItem { Name = ci.Name, ColorIndex = ci.ColorIndex, Brush = ci.Brush });
                if (CmbColorImportant != null) CmbColorImportant.Items.Add(new ColorItem { Name = ci.Name, ColorIndex = ci.ColorIndex, Brush = ci.Brush });
                if (CmbColorUrgent != null) CmbColorUrgent.Items.Add(new ColorItem { Name = ci.Name, ColorIndex = ci.ColorIndex, Brush = ci.Brush });
            }

            // 文字样式下拉列表 (从当前活动图纸读取 TextStyleTable)
            CmbTextStyle.Items.Clear();
            CmbTextStyle.Items.Add("Standard");
            try
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                if (doc?.Database != null)
                {
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        var tst = (AcDb.TextStyleTable)tr.GetObject(doc.Database.TextStyleTableId, AcDb.OpenMode.ForRead);
                        foreach (AcDb.ObjectId id in tst)
                        {
                            var rec = (AcDb.TextStyleTableRecord)tr.GetObject(id, AcDb.OpenMode.ForRead);
                            if (!string.IsNullOrEmpty(rec.Name) && !CmbTextStyle.Items.Contains(rec.Name))
                            {
                                CmbTextStyle.Items.Add(rec.Name);
                            }
                        }
                        tr.Commit();
                    }
                }
            }
            catch { }

            // 云线样式
            if (CmbCloudStyle != null)
            {
                CmbCloudStyle.Items.Clear();
                CmbCloudStyle.Items.Add("普通等宽");
                CmbCloudStyle.Items.Add("书法笔锋");
            }

            // 云线线宽
            CmbCloudWidth.Items.Clear();
            CmbCloudWidth.Items.Add("0.00 mm");
            CmbCloudWidth.Items.Add("0.30 mm");
            CmbCloudWidth.Items.Add("0.50 mm");
            CmbCloudWidth.Items.Add("0.80 mm");
            CmbCloudWidth.Items.Add("1.00 mm");
            CmbCloudWidth.Items.Add("1.50 mm");

            // 云线类型
            CmbCloudType.Items.Clear();
            CmbCloudType.Items.Add("矩形云线");
            CmbCloudType.Items.Add("多边形云线");
            CmbCloudType.Items.Add("徒手画云线");

            // 引线类型
            CmbLeaderType.Items.Clear();
            CmbLeaderType.Items.Add("带箭头引线");
            CmbLeaderType.Items.Add("点引线");
            CmbLeaderType.Items.Add("无引线独立放置");

            // 专业
            string[] disciplines = new string[] { "建筑", "结构", "给排水", "暖通", "电气", "总图", "通用" };
            CmbDiscipline.Items.Clear();
            CmbKbItemDiscipline.Items.Clear();
            foreach (var d in disciplines)
            {
                CmbDiscipline.Items.Add(d);
                CmbKbItemDiscipline.Items.Add(d);
            }

            // 规范筛选来源与专业
            if (CmbKbFilterSource != null)
            {
                CmbKbFilterSource.Items.Clear();
                CmbKbFilterSource.Items.Add("全部来源");
                CmbKbFilterSource.Items.Add("用户常用");
                CmbKbFilterSource.Items.Add("现行规范");
                CmbKbFilterSource.SelectedIndex = 0;
            }

            CmbKbFilterDiscipline.Items.Clear();
            CmbKbFilterDiscipline.Items.Add("全部专业");
            foreach (var d in disciplines) CmbKbFilterDiscipline.Items.Add(d);
            CmbKbFilterDiscipline.SelectedIndex = 0;

            // 优先级
            CmbPriority.Items.Clear();
            string[] priorities = new string[] { "重要", "紧急" };
            foreach (var p in priorities) CmbPriority.Items.Add(p);

            // 极速快捷短语专业分类
            if (CmbPhraseDisciplineFilter != null)
            {
                CmbPhraseDisciplineFilter.ItemsSource = new string[] { "全部", "通用", "建筑", "结构", "给排水", "电气", "暖通", "销项回复" };
                CmbPhraseDisciplineFilter.SelectedIndex = 0;
            }
            if (CmbNewPhraseDiscipline != null)
            {
                CmbNewPhraseDiscipline.ItemsSource = new string[] { "通用", "建筑", "结构", "给排水", "电气", "暖通", "销项回复" };
                CmbNewPhraseDiscipline.SelectedIndex = 0;
            }

            // 批注使用对象（使用本工具的人）下拉
            var allTemplates = ProjectTemplateService.GetAllTemplates();
            if (CmbTargetPersonaHero != null)
            {
                CmbTargetPersonaHero.ItemsSource = allTemplates;
                CmbTargetPersonaHero.SelectedIndex = 0;
            }
            if (CmbProjectTemplates != null)
            {
                CmbProjectTemplates.ItemsSource = allTemplates;
                CmbProjectTemplates.SelectedIndex = 0;
            }

            RefreshRapidPhrasesList();
        }

        private void LoadSettingsToUI()
        {
            var s = ConfigManager.Instance.CurrentSettings;

            // 1. 图层与样式
            TxtCloudLayer.Text = s.CloudLayer;
            TxtTextLayer.Text = s.TextLayer;
            ChkNonPlotting.IsChecked = s.ForceNonPlotting;
            string targetStyle = string.IsNullOrEmpty(s.TextStyleName) ? "Standard" : s.TextStyleName;
            if (CmbTextStyle.Items.Contains(targetStyle))
            {
                CmbTextStyle.SelectedItem = targetStyle;
            }
            else if (CmbTextStyle.Items.Count > 0)
            {
                CmbTextStyle.SelectedIndex = 0;
            }
            else
            {
                CmbTextStyle.Text = targetStyle;
            }
            if (TxtTextHeightInLayer != null) TxtTextHeightInLayer.Text = s.TextHeightRatio.ToString("0.0");
            SetColorCombo(CmbCloudColor, s.CloudColorIndex);
            SetColorCombo(CmbTextColor, s.TextColorIndex);

            // 严重等级颜色联动
            if (ChkPriorityColorLink != null) ChkPriorityColorLink.IsChecked = s.EnablePriorityColorLink;
            if (CmbColorImportant != null) SetColorCombo(CmbColorImportant, s.PriorityColorImportant);
            if (CmbColorUrgent != null) SetColorCombo(CmbColorUrgent, s.PriorityColorUrgent);

            // 极速短语库刷新
            RefreshRapidPhrasesList();

            // 2. 几何
            _isUpdatingFromCode = true;
            SliderArcLength.Value = s.ArcLengthRatio;
            TxtArcLengthVal.Text = s.ArcLengthRatio.ToString("0.0");
            SliderBulge.Value = s.BulgeCurvature;
            TxtBulgeVal.Text = s.BulgeCurvature.ToString("0.00");
            _isUpdatingFromCode = false;

            if (CmbCloudStyle != null)
            {
                CmbCloudStyle.SelectedIndex = Math.Max(0, Math.Min(1, s.CloudStyle));
            }
            SelectCloudWidthCombo(s.CloudWidth);
            CmbCloudType.SelectedIndex = Math.Max(0, Math.Min(2, s.DefaultCloudType));
            TxtMinArc.Text = s.MinArcLength.ToString("0.0");
            TxtMaxArc.Text = s.MaxArcLengthRatio.ToString("0.0");

            // 3. 文字与引线
            _isUpdatingFromCode = true;
            SliderTextHeight.Value = s.TextHeightRatio;
            TxtTextHeightVal.Text = s.TextHeightRatio.ToString("0.0");
            SliderArrowSize.Value = s.ArrowSizeRatio;
            TxtArrowSizeVal.Text = s.ArrowSizeRatio.ToString("0.0");
            _isUpdatingFromCode = false;

            CmbLeaderType.SelectedIndex = Math.Max(0, Math.Min(2, s.LeaderType));
            TxtPrefix.Text = s.AutoNumberPrefix;

            // 4. 业务
            int dIdx = CmbDiscipline.Items.IndexOf(s.DefaultDiscipline);
            CmbDiscipline.SelectedIndex = dIdx >= 0 ? dIdx : 0;
            TxtAssignee.Text = s.DefaultAssignee;
            int pIdx = CmbPriority.Items.IndexOf(s.DefaultPriority);
            CmbPriority.SelectedIndex = pIdx >= 0 ? pIdx : 0;
            ChkAutoZoom.IsChecked = s.AutoZoomOnSelect;
            ChkSound.IsChecked = s.EnableSoundNotification;

            // 极速批注与短语配置
            _isUpdatingFromCode = true;
            ChkEnableSimpleNoteMode.IsChecked = s.EnableSimpleNoteMode;
            if (ChkPanelEnableSimpleMode != null) ChkPanelEnableSimpleMode.IsChecked = s.EnableSimpleNoteMode;
            if (ChkEnableRapidCmdKey != null) ChkEnableRapidCmdKey.IsChecked = s.EnableRapidCmdKeySelection;
            if (CmbRapidDefaultPhrase != null) CmbRapidDefaultPhrase.Text = s.RapidDefaultPhrase;
            if (CmbBottomRapidPhrase != null) CmbBottomRapidPhrase.SelectedItem = s.RapidDefaultPhrase;

            // 批注使用对象选中
            AnnotationTemplate curTpl = null;
            if (CmbTargetPersonaHero != null && CmbTargetPersonaHero.ItemsSource is List<AnnotationTemplate> tplsHero)
            {
                curTpl = tplsHero.Find(t => t.Id == s.CurrentTemplateId) ?? tplsHero[0];
                CmbTargetPersonaHero.SelectedItem = curTpl;
            }
            if (CmbProjectTemplates != null && CmbProjectTemplates.ItemsSource is List<AnnotationTemplate> tpls)
            {
                if (curTpl == null) curTpl = tpls.Find(t => t.Id == s.CurrentTemplateId) ?? tpls[0];
                CmbProjectTemplates.SelectedItem = curTpl;
            }
            if (curTpl != null)
            {
                UpdateAudiencePanelDisplay(curTpl);
                if (curTpl.IsNonPlottingLocked)
                {
                    ChkNonPlotting.IsEnabled = false;
                }
            }
            _isUpdatingFromCode = false;

            // 5. 规范与 AI
            ChkEnableAi.IsChecked = s.EnableAiSemantic;
            _isUpdatingFromCode = true;
            SliderAiThreshold.Value = s.AiSimilarityThreshold;
            TxtAiThresholdVal.Text = s.AiSimilarityThreshold.ToString("0.00");
            _isUpdatingFromCode = false;

            ChkAutoFillCode.IsChecked = s.AutoFillStandardCode;

            // 6. 存储与审计
            TxtMaxNotesVolume.Text = s.MaxNotesPerVolume.ToString();
            ChkAuditTrail.IsChecked = s.EnableAuditTrail;
            ChkAutoBackup.IsChecked = s.AutoBackupOnSave;
        }

        private void SelectCloudWidthCombo(double width)
        {
            if (width <= 0.05) CmbCloudWidth.SelectedIndex = 0;
            else if (Math.Abs(width - 0.30) < 0.05) CmbCloudWidth.SelectedIndex = 1;
            else if (Math.Abs(width - 0.50) < 0.05) CmbCloudWidth.SelectedIndex = 2;
            else if (Math.Abs(width - 0.80) < 0.05) CmbCloudWidth.SelectedIndex = 3;
            else if (Math.Abs(width - 1.00) < 0.05) CmbCloudWidth.SelectedIndex = 4;
            else if (Math.Abs(width - 1.50) < 0.05) CmbCloudWidth.SelectedIndex = 5;
            else
            {
                CmbCloudWidth.Text = width.ToString("0.00") + " mm";
            }
        }

        private double GetSelectedCloudWidth()
        {
            int idx = CmbCloudWidth.SelectedIndex;
            if (idx == 0) return 0.0;
            if (idx == 1) return 0.30;
            if (idx == 2) return 0.50;
            if (idx == 3) return 0.80;
            if (idx == 4) return 1.00;
            if (idx == 5) return 1.50;

            string txt = CmbCloudWidth.Text.Replace("mm", "").Trim();
            if (double.TryParse(txt, out double val)) return Math.Max(0.0, val);
            return 0.0;
        }

        private void SetColorCombo(ComboBox cmb, int colorIndex)
        {
            foreach (var item in cmb.Items)
            {
                if (item is ColorItem ci && ci.ColorIndex == colorIndex)
                {
                    cmb.SelectedItem = ci;
                    return;
                }
            }
            if (cmb.Items.Count > 0) cmb.SelectedIndex = 0;
        }

        private int GetColorFromCombo(ComboBox cmb)
        {
            if (cmb.SelectedItem is ColorItem ci)
            {
                return ci.ColorIndex;
            }
            return 1;
        }

        private void TabNav_Click(object sender, RoutedEventArgs e)
        {
            if (PanelAudience != null) PanelAudience.Visibility = (TabBtnAudience?.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
            if (PanelDrawing != null) PanelDrawing.Visibility = (TabBtnDrawing?.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
            if (PanelRapid != null) PanelRapid.Visibility = (TabBtnRapid?.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
            if (PanelKnowledge != null) PanelKnowledge.Visibility = (TabBtnKnowledge?.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
            if (PanelSystem != null) PanelSystem.Visibility = (TabBtnSystem?.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;

            if (TabBtnDrawing?.IsChecked == true)
            {
                UpdatePreview();
            }
        }

        private void CmbColors_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || _isUpdatingFromCode) return;
            // 默认云线色与严重等级双向联动同步
            if (sender == CmbCloudColor && CmbCloudColor.SelectedItem is ColorItem ci)
            {
                _isUpdatingFromCode = true;
                string prio = CmbPriority?.SelectedItem as string ?? "重要";
                if (prio == "重要" && CmbColorImportant != null)
                {
                    SetColorCombo(CmbColorImportant, ci.ColorIndex);
                }
                else if (prio == "紧急" && CmbColorUrgent != null)
                {
                    SetColorCombo(CmbColorUrgent, ci.ColorIndex);
                }
                _isUpdatingFromCode = false;
            }
            UpdatePreview();
        }

        private void CmbPriority_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || _isUpdatingFromCode) return;
            _isUpdatingFromCode = true;
            string prio = CmbPriority?.SelectedItem as string ?? "重要";
            if (prio == "重要" && CmbColorImportant?.SelectedItem is ColorItem ciImp)
            {
                SetColorCombo(CmbCloudColor, ciImp.ColorIndex);
            }
            else if (prio == "紧急" && CmbColorUrgent?.SelectedItem is ColorItem ciUrg)
            {
                SetColorCombo(CmbCloudColor, ciUrg.ColorIndex);
            }
            _isUpdatingFromCode = false;
            UpdatePreview();
        }

        private void CmbColorPriority_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || _isUpdatingFromCode) return;
            _isUpdatingFromCode = true;
            string prio = CmbPriority?.SelectedItem as string ?? "重要";
            if (sender == CmbColorImportant && prio == "重要" && CmbColorImportant.SelectedItem is ColorItem ciImp)
            {
                SetColorCombo(CmbCloudColor, ciImp.ColorIndex);
            }
            else if (sender == CmbColorUrgent && prio == "紧急" && CmbColorUrgent.SelectedItem is ColorItem ciUrg)
            {
                SetColorCombo(CmbCloudColor, ciUrg.ColorIndex);
            }
            _isUpdatingFromCode = false;
            UpdatePreview();
        }

        private void CmbCloudWidth_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            UpdatePreview();
        }

        private void CmbCloudStyle_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            UpdatePreview();
        }

        #region 双向滑块与输入框数据联动
        private void SliderArcLength_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing) return;
            if (TxtArcLengthVal != null && !_isUpdatingFromCode)
            {
                _isUpdatingFromCode = true;
                TxtArcLengthVal.Text = SliderArcLength.Value.ToString("0.0");
                _isUpdatingFromCode = false;
            }
            UpdatePreview();
        }

        private void TxtArcLengthVal_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing || _isUpdatingFromCode || SliderArcLength == null) return;
            if (double.TryParse(TxtArcLengthVal.Text.Trim(), out double val))
            {
                if (val >= SliderArcLength.Minimum && val <= SliderArcLength.Maximum)
                {
                    _isUpdatingFromCode = true;
                    SliderArcLength.Value = val;
                    _isUpdatingFromCode = false;
                    UpdatePreview();
                }
            }
        }

        private void SliderBulge_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing) return;
            if (TxtBulgeVal != null && !_isUpdatingFromCode)
            {
                _isUpdatingFromCode = true;
                TxtBulgeVal.Text = SliderBulge.Value.ToString("0.00");
                _isUpdatingFromCode = false;
            }
            UpdatePreview();
        }

        private void TxtBulgeVal_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing || _isUpdatingFromCode || SliderBulge == null) return;
            if (double.TryParse(TxtBulgeVal.Text.Trim(), out double val))
            {
                if (val >= SliderBulge.Minimum && val <= SliderBulge.Maximum)
                {
                    _isUpdatingFromCode = true;
                    SliderBulge.Value = val;
                    _isUpdatingFromCode = false;
                    UpdatePreview();
                }
            }
        }

        private void SliderTextHeight_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing) return;
            if (TxtTextHeightVal != null && !_isUpdatingFromCode)
            {
                _isUpdatingFromCode = true;
                TxtTextHeightVal.Text = SliderTextHeight.Value.ToString("0.0");
                if (TxtTextHeightInLayer != null) TxtTextHeightInLayer.Text = SliderTextHeight.Value.ToString("0.0");
                _isUpdatingFromCode = false;
            }
            UpdatePreview();
        }

        private void TxtTextHeightVal_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing || _isUpdatingFromCode || SliderTextHeight == null) return;
            if (double.TryParse(TxtTextHeightVal.Text.Trim(), out double val))
            {
                if (val >= SliderTextHeight.Minimum && val <= SliderTextHeight.Maximum)
                {
                    _isUpdatingFromCode = true;
                    SliderTextHeight.Value = val;
                    if (TxtTextHeightInLayer != null) TxtTextHeightInLayer.Text = val.ToString("0.0");
                    _isUpdatingFromCode = false;
                    UpdatePreview();
                }
            }
        }

        private void TxtTextHeightInLayer_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing || _isUpdatingFromCode || SliderTextHeight == null) return;
            if (double.TryParse(TxtTextHeightInLayer.Text.Trim(), out double val))
            {
                if (val >= SliderTextHeight.Minimum && val <= SliderTextHeight.Maximum)
                {
                    _isUpdatingFromCode = true;
                    SliderTextHeight.Value = val;
                    if (TxtTextHeightVal != null) TxtTextHeightVal.Text = val.ToString("0.0");
                    _isUpdatingFromCode = false;
                    UpdatePreview();
                }
            }
        }

        private void SliderArrowSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing) return;
            if (TxtArrowSizeVal != null && !_isUpdatingFromCode)
            {
                _isUpdatingFromCode = true;
                TxtArrowSizeVal.Text = SliderArrowSize.Value.ToString("0.0");
                _isUpdatingFromCode = false;
            }
        }

        private void TxtArrowSizeVal_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing || _isUpdatingFromCode || SliderArrowSize == null) return;
            if (double.TryParse(TxtArrowSizeVal.Text.Trim(), out double val))
            {
                if (val >= SliderArrowSize.Minimum && val <= SliderArrowSize.Maximum)
                {
                    _isUpdatingFromCode = true;
                    SliderArrowSize.Value = val;
                    _isUpdatingFromCode = false;
                }
            }
        }

        private void SliderAiThreshold_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing) return;
            if (TxtAiThresholdVal != null && !_isUpdatingFromCode)
            {
                _isUpdatingFromCode = true;
                TxtAiThresholdVal.Text = SliderAiThreshold.Value.ToString("0.00");
                _isUpdatingFromCode = false;
            }
        }

        private void TxtAiThresholdVal_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing || _isUpdatingFromCode || SliderAiThreshold == null) return;
            if (double.TryParse(TxtAiThresholdVal.Text.Trim(), out double val))
            {
                if (val >= SliderAiThreshold.Minimum && val <= SliderAiThreshold.Maximum)
                {
                    _isUpdatingFromCode = true;
                    SliderAiThreshold.Value = val;
                    _isUpdatingFromCode = false;
                }
            }
        }
        #endregion

        #region 规范知识库管理 (CRUD)
        private void RefreshKnowledgeList()
        {
            if (ListKnowledge == null) return;
            string kw = TxtKbSearch != null ? TxtKbSearch.Text.Trim() : string.Empty;
            string disc = null;
            if (CmbKbFilterDiscipline != null && CmbKbFilterDiscipline.SelectedIndex > 0)
            {
                disc = CmbKbFilterDiscipline.SelectedItem as string;
            }

            string source = "全部";
            if (CmbKbFilterSource != null && CmbKbFilterSource.SelectedIndex > 0)
            {
                source = CmbKbFilterSource.SelectedItem as string;
            }

            var items = KnowledgeBaseService.Search(kw, disc, source);
            ListKnowledge.ItemsSource = items;
        }

        private void CmbKbFilterSource_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            RefreshKnowledgeList();
        }

        private void TxtKbSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing) return;
            RefreshKnowledgeList();
        }

        private void CmbKbFilterDiscipline_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            RefreshKnowledgeList();
        }

        private void ListKnowledge_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListKnowledge.SelectedItem is KnowledgeItem item && BorderKbEditor.Visibility == Visibility.Visible)
            {
                TxtKbItemCode.Text = item.Id;
                TxtKbItemTitle.Text = item.Title;
                TxtKbItemStdCode.Text = item.StandardCode;
                TxtKbItemSuggestion.Text = item.Suggestion;
                int idx = CmbKbItemDiscipline.Items.IndexOf(item.Discipline);
                CmbKbItemDiscipline.SelectedIndex = idx >= 0 ? idx : 0;
            }
        }

        private void BtnAddKb_Click(object sender, RoutedEventArgs e)
        {
            BorderKbEditor.Visibility = Visibility.Visible;
            TxtKbItemCode.Text = "KB" + (KnowledgeBaseService.GetAll().Count + 1).ToString("D2");
            TxtKbItemTitle.Text = string.Empty;
            TxtKbItemStdCode.Text = string.Empty;
            TxtKbItemSuggestion.Text = string.Empty;
            CmbKbItemDiscipline.SelectedIndex = 0;
            TxtKbItemTitle.Focus();
        }

        private void BtnEditKb_Click(object sender, RoutedEventArgs e)
        {
            if (ListKnowledge.SelectedItem is KnowledgeItem item)
            {
                BorderKbEditor.Visibility = Visibility.Visible;
                TxtKbItemCode.Text = item.Id;
                TxtKbItemTitle.Text = item.Title;
                TxtKbItemStdCode.Text = item.StandardCode;
                TxtKbItemSuggestion.Text = item.Suggestion;
                int idx = CmbKbItemDiscipline.Items.IndexOf(item.Discipline);
                CmbKbItemDiscipline.SelectedIndex = idx >= 0 ? idx : 0;
                TxtKbItemTitle.Focus();
            }
            else
            {
                CadMessageBox.ShowInfo("请先在列表中选中需要编辑的规范条目。", "提示", this);
            }
        }

        private void BtnSaveKbItem_Click(object sender, RoutedEventArgs e)
        {
            string id = TxtKbItemCode.Text.Trim();
            string title = TxtKbItemTitle.Text.Trim();
            string std = TxtKbItemStdCode.Text.Trim();
            string sugg = TxtKbItemSuggestion.Text.Trim();
            string disc = CmbKbItemDiscipline.SelectedItem as string ?? "通用";

            if (string.IsNullOrEmpty(title))
            {
                CadMessageBox.ShowWarning("请输入规范条目标题。", "提示", this);
                TxtKbItemTitle.Focus();
                return;
            }

            var item = new KnowledgeItem(id, disc, title, sugg, std);
            KnowledgeBaseService.AddOrUpdate(item);
            BorderKbEditor.Visibility = Visibility.Collapsed;
            RefreshKnowledgeList();
        }

        private void BtnCancelKbItem_Click(object sender, RoutedEventArgs e)
        {
            BorderKbEditor.Visibility = Visibility.Collapsed;
        }

        private void BtnDeleteKb_Click(object sender, RoutedEventArgs e)
        {
            if (ListKnowledge.SelectedItem is KnowledgeItem item)
            {
                if (CadMessageBox.Confirm(string.Format("确定要删除条目【{0}】{1} 吗？", item.Id, item.Title), "确认删除", this))
                {
                    KnowledgeBaseService.Delete(item.Id);
                    RefreshKnowledgeList();
                }
            }
            else
            {
                CadMessageBox.ShowInfo("请先在列表中选中需要删除的条目。", "提示", this);
            }
        }

        private void BtnResetKb_Click(object sender, RoutedEventArgs e)
        {
            if (CadMessageBox.Show("确定要重置规范库为内置现行国家规范条目吗？自定义条目将被覆盖。", "恢复系统标准", CadMessageBoxButtons.YesNo, CadMessageBoxIcon.Warning, this) == CadDialogResult.Yes)
            {
                KnowledgeBaseService.ResetToDefaults();
                RefreshKnowledgeList();
            }
        }
        #endregion

        #region 常用审查意见快捷词典与健康体检
        #region 极速快捷短语库与工程场景模板联动
        private void RefreshRapidPhrasesList()
        {
            if (ListRapidPhrases == null) return;
            string selectedDiscipline = CmbPhraseDisciplineFilter?.SelectedItem as string;
            string keyword = TxtPhraseSearch?.Text?.Trim();

            var list = RapidPhraseService.GetAll();
            if (!string.IsNullOrEmpty(selectedDiscipline) && selectedDiscipline != "全部")
            {
                list = list.FindAll(p => string.Equals(p.Discipline, selectedDiscipline, StringComparison.OrdinalIgnoreCase));
            }
            if (!string.IsNullOrEmpty(keyword))
            {
                list = list.FindAll(p => (p.Text != null && p.Text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                                      || (p.Category != null && p.Category.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            ListRapidPhrases.ItemsSource = null;
            ListRapidPhrases.ItemsSource = list;
            if (TxtPhraseCount != null) TxtPhraseCount.Text = list.Count.ToString();

            // 同步刷新极速默认短语下拉框（精选控制在 10 条以内的黄金高频预置短语，杜绝冗长滚动条）
            string currentDefault = CmbRapidDefaultPhrase?.Text;
            if (string.IsNullOrEmpty(currentDefault)) currentDefault = ConfigManager.Instance.CurrentSettings.RapidDefaultPhrase;
            var corePhrases = RapidPhraseService.GetCoreRapidPresetPhrases();
            if (!string.IsNullOrEmpty(currentDefault) && !corePhrases.Contains(currentDefault))
            {
                corePhrases.Insert(0, currentDefault);
            }

            _isUpdatingFromCode = true;
            if (CmbRapidDefaultPhrase != null)
            {
                CmbRapidDefaultPhrase.ItemsSource = null;
                CmbRapidDefaultPhrase.ItemsSource = corePhrases;
                CmbRapidDefaultPhrase.Text = currentDefault;
            }
            if (CmbBottomRapidPhrase != null)
            {
                CmbBottomRapidPhrase.ItemsSource = null;
                CmbBottomRapidPhrase.ItemsSource = corePhrases;
                CmbBottomRapidPhrase.SelectedItem = currentDefault;
                if (CmbBottomRapidPhrase.SelectedIndex < 0 && corePhrases.Count > 0)
                {
                    CmbBottomRapidPhrase.SelectedIndex = 0;
                }
            }
            _isUpdatingFromCode = false;
        }

        private void CmbPhraseDisciplineFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            RefreshRapidPhrasesList();
        }

        private void TxtPhraseSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing) return;
            RefreshRapidPhrasesList();
        }

        private void BtnSetAsDefaultPhrase_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is RapidPhraseItem item)
            {
                _isUpdatingFromCode = true;
                if (CmbRapidDefaultPhrase != null) CmbRapidDefaultPhrase.Text = item.Text;
                if (CmbBottomRapidPhrase != null) CmbBottomRapidPhrase.SelectedItem = item.Text;
                ConfigManager.Instance.CurrentSettings.RapidDefaultPhrase = item.Text;
                _isUpdatingFromCode = false;
                CadMessageBox.ShowInfo(string.Format("已将【{0}】设为极速免弹窗默认短语！", item.Text), "设置成功", this);
            }
        }

        private void BtnDeletePhrase_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is RapidPhraseItem item)
            {
                if (CadMessageBox.Confirm(string.Format("确定要删除短语【{0}】吗？", item.Text), "确认删除", this))
                {
                    RapidPhraseService.Delete(item.Id);
                    RefreshRapidPhrasesList();
                }
            }
        }

        private void BtnAddPhrase_Click(object sender, RoutedEventArgs e)
        {
            string txt = TxtNewPhraseText?.Text?.Trim();
            if (string.IsNullOrEmpty(txt))
            {
                CadMessageBox.ShowWarning("请输入快捷短语内容。", "提示", this);
                TxtNewPhraseText?.Focus();
                return;
            }
            string disc = CmbNewPhraseDiscipline?.SelectedItem as string ?? "通用";
            var item = new RapidPhraseItem(null, disc, "自定义", txt, true);
            RapidPhraseService.AddOrUpdate(item);
            if (TxtNewPhraseText != null) TxtNewPhraseText.Text = string.Empty;
            RefreshRapidPhrasesList();
        }

        private void BtnResetPhrases_Click(object sender, RoutedEventArgs e)
        {
            if (CadMessageBox.Confirm("确定要恢复内置默认快捷短语词库吗？\n（自定义添加的词条将被重置）", "恢复确认", this))
            {
                RapidPhraseService.ResetToDefaults();
                RefreshRapidPhrasesList();
            }
        }

        private void CmbRapidDefaultPhrase_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingFromCode || _isInitializing) return;
            string sel = CmbRapidDefaultPhrase.SelectedItem as string;
            if (string.IsNullOrEmpty(sel)) sel = CmbRapidDefaultPhrase.Text;
            if (!string.IsNullOrEmpty(sel))
            {
                _isUpdatingFromCode = true;
                if (CmbBottomRapidPhrase != null) CmbBottomRapidPhrase.SelectedItem = sel;
                _isUpdatingFromCode = false;
            }
        }

        private void CmbBottomRapidPhrase_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingFromCode || _isInitializing) return;
            string sel = CmbBottomRapidPhrase.SelectedItem as string;
            if (!string.IsNullOrEmpty(sel))
            {
                _isUpdatingFromCode = true;
                if (CmbRapidDefaultPhrase != null) CmbRapidDefaultPhrase.Text = sel;
                _isUpdatingFromCode = false;
            }
        }

        private void ChkSimpleMode_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingFromCode) return;
            _isUpdatingFromCode = true;
            bool isChecked = false;
            if (sender == ChkEnableSimpleNoteMode)
            {
                isChecked = ChkEnableSimpleNoteMode.IsChecked == true;
                if (ChkPanelEnableSimpleMode != null) ChkPanelEnableSimpleMode.IsChecked = isChecked;
            }
            else if (sender == ChkPanelEnableSimpleMode)
            {
                isChecked = ChkPanelEnableSimpleMode.IsChecked == true;
                if (ChkEnableSimpleNoteMode != null) ChkEnableSimpleNoteMode.IsChecked = isChecked;
            }
            _isUpdatingFromCode = false;
        }

        private void BtnJumpToRapid_Click(object sender, RoutedEventArgs e)
        {
            if (TabBtnRapid != null)
            {
                TabBtnRapid.IsChecked = true;
                TabNav_Click(this, new RoutedEventArgs());
            }
        }

        private void CmbTargetPersonaHero_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingFromCode) return;
            if (CmbTargetPersonaHero.SelectedItem is AnnotationTemplate tpl)
            {
                _isUpdatingFromCode = true;
                if (CmbProjectTemplates != null && CmbProjectTemplates.SelectedItem != tpl)
                {
                    CmbProjectTemplates.SelectedItem = tpl;
                }
                _isUpdatingFromCode = false;
                UpdateAudiencePanelDisplay(tpl);
            }
        }

        private void CmbProjectTemplates_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingFromCode) return;
            if (CmbProjectTemplates.SelectedItem is AnnotationTemplate tpl)
            {
                _isUpdatingFromCode = true;
                if (CmbTargetPersonaHero != null && CmbTargetPersonaHero.SelectedItem != tpl)
                {
                    CmbTargetPersonaHero.SelectedItem = tpl;
                }
                _isUpdatingFromCode = false;
                UpdateAudiencePanelDisplay(tpl);
            }
        }

        private void UpdateAudiencePanelDisplay(AnnotationTemplate tpl)
        {
            if (tpl == null) return;
            if (TxtTemplateRoleTitle != null) TxtTemplateRoleTitle.Text = tpl.RoleTitle ?? tpl.Name;
            if (TxtTemplateDesc != null) TxtTemplateDesc.Text = tpl.Description;
            if (TxtTemplateLayer != null) TxtTemplateLayer.Text = tpl.LayerName;
            if (TxtTemplatePrefix != null) TxtTemplatePrefix.Text = tpl.Prefix;
            if (TxtTemplatePlot != null) TxtTemplatePlot.Text = tpl.ForceNonPlotting ? "默认不打印" : "可打印";
            if (TxtTemplatePlotLock != null)
            {
                TxtTemplatePlotLock.Visibility = tpl.IsNonPlottingLocked ? Visibility.Visible : Visibility.Collapsed;
            }

            if (LstTemplateSnippets != null)
            {
                LstTemplateSnippets.ItemsSource = tpl.QuickReviewSnippets != null && tpl.QuickReviewSnippets.Count > 0
                    ? tpl.QuickReviewSnippets
                    : tpl.QuickPhrases;
            }

            // 更新适用专业白名单与不相关专业置灰指示
            UpdateDisciplineChipState(ChipAudienceArch, tpl.IsDisciplineApplicable("建筑"), "建筑");
            UpdateDisciplineChipState(ChipAudienceStruct, tpl.IsDisciplineApplicable("结构"), "结构");
            UpdateDisciplineChipState(ChipAudiencePlumb, tpl.IsDisciplineApplicable("给排水"), "给排水");
            UpdateDisciplineChipState(ChipAudienceHvac, tpl.IsDisciplineApplicable("暖通"), "暖通");
            UpdateDisciplineChipState(ChipAudienceElect, tpl.IsDisciplineApplicable("电气"), "电气");
            UpdateDisciplineChipState(ChipAudienceSite, tpl.IsDisciplineApplicable("总图"), "总图");
            UpdateDisciplineChipState(ChipAudienceGeneral, tpl.IsDisciplineApplicable("通用"), "通用");
        }

        private void UpdateDisciplineChipState(Border chip, bool applicable, string name)
        {
            if (chip == null) return;
            if (chip.Child is TextBlock tb)
            {
                if (applicable)
                {
                    chip.Opacity = 1.0;
                    chip.Background = _isDarkTheme 
                        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#263445"))
                        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0F2FE"));
                    chip.BorderBrush = _isDarkTheme 
                        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0084FF"))
                        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0284C7"));
                    tb.Text = name;
                    tb.Foreground = _isDarkTheme 
                        ? new SolidColorBrush(Colors.White)
                        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0369A1"));
                    tb.FontWeight = FontWeights.SemiBold;
                }
                else
                {
                    chip.Opacity = 0.4;
                    chip.Background = _isDarkTheme 
                        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#181E27"))
                        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9"));
                    chip.BorderBrush = _isDarkTheme 
                        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#36404F"))
                        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1"));
                    tb.Text = name + " [置灰禁用]";
                    tb.Foreground = _isDarkTheme 
                        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#78879B"))
                        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));
                    tb.FontWeight = FontWeights.Normal;
                }
            }
        }

        private void BtnApplyHeroTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (CmbTargetPersonaHero.SelectedItem is AnnotationTemplate tpl)
            {
                ApplyPersonaTemplate(tpl);
            }
        }

        private void BtnApplyTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (CmbProjectTemplates.SelectedItem is AnnotationTemplate tpl)
            {
                ApplyPersonaTemplate(tpl);
            }
        }

        private void ApplyPersonaTemplate(AnnotationTemplate tpl)
        {
            if (tpl == null) return;
            var s = ConfigManager.Instance.CurrentSettings;
            ProjectTemplateService.ApplyTemplateToSettings(tpl.Id, s);

            // 同步当前界面上的显示字段
            if (TxtCloudLayer != null) TxtCloudLayer.Text = tpl.LayerName;
            if (TxtTextLayer != null) TxtTextLayer.Text = tpl.LayerName + "_TEXT";
            if (TxtPrefix != null) TxtPrefix.Text = tpl.Prefix;
            if (ChkNonPlotting != null)
            {
                ChkNonPlotting.IsChecked = tpl.ForceNonPlotting;
                ChkNonPlotting.IsEnabled = !tpl.IsNonPlottingLocked;
            }
            SetColorCombo(CmbCloudColor, tpl.ColorIndex);
            SetColorCombo(CmbTextColor, 7); // 文字颜色始终保持标准 7 号黑白色

            if (tpl.QuickPhrases != null && tpl.QuickPhrases.Count > 0)
            {
                string firstPhrase = tpl.QuickPhrases[0];
                if (CmbRapidDefaultPhrase != null) CmbRapidDefaultPhrase.Text = firstPhrase;
                if (CmbBottomRapidPhrase != null) CmbBottomRapidPhrase.SelectedItem = firstPhrase;
            }

            RefreshRapidPhrasesList();
            UpdatePreview();
            CadMessageBox.ShowInfo(string.Format("已成功应用【{0}】规则！\n- 角色定位：{1}\n- 批注图层：{2}\n- 编号前缀：{3}\n- 打印控制：{4}\n- 专属审查短语库：已加载 {5} 条",
                tpl.Name, tpl.RoleTitle ?? tpl.Name, tpl.LayerName, tpl.Prefix,
                tpl.ForceNonPlotting ? (tpl.IsNonPlottingLocked ? "强制关闭打印 (已锁定)" : "关闭打印") : "开启打印",
                tpl.QuickReviewSnippets != null ? tpl.QuickReviewSnippets.Count : 0),
                "批注使用对象规则应用成功", this);
        }

        private void BtnExportTemplateExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                if (doc?.Database == null)
                {
                    CadMessageBox.ShowWarning("未检测到活动的 AutoCAD 图纸文档。", "提示", this);
                    return;
                }

                List<NoteRecord> notes = null;
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    notes = XRecordHelper.ReadAllNotes(doc.Database, tr);
                    tr.Commit();
                }

                if (notes == null || notes.Count == 0)
                {
                    CadMessageBox.ShowInfo("当前图纸中没有可导出的批注记录。", "提示", this);
                    return;
                }

                var tpl = CmbProjectTemplates.SelectedItem as AnnotationTemplate ?? ProjectTemplateService.GetTemplate("tpl_general");

                var sfd = new Microsoft.Win32.SaveFileDialog
                {
                    Title = string.Format("导出【{0}】差异清单", tpl.Name),
                    Filter = "Excel 电子表格 (*.xml)|*.xml|CSV 逗号分隔文件 (*.csv)|*.csv",
                    FileName = string.Format("{0}_批注汇总_{1:yyyyMMdd_HHmm}.xml", tpl.Name, DateTime.Now)
                };

                if (sfd.ShowDialog() == true)
                {
                    if (sfd.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                    {
                        ExportService.ExportToCsv(notes, sfd.FileName);
                    }
                    else
                    {
                        ExportService.ExportTemplateReport(notes, tpl.Id, sfd.FileName);
                    }
                    CadMessageBox.ShowInfo(string.Format("批注清单已成功导出至:\n{0}", sfd.FileName), "导出成功", this);
                }
            }
            catch (Exception ex)
            {
                CadMessageBox.ShowError("导出清单时发生异常: " + ex.Message, "错误", this);
            }
        }
        #endregion

        private void BtnCompactDatabase_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                if (doc == null || doc.Database == null)
                {
                    CadMessageBox.ShowWarning("未检测到活动的 AutoCAD 绘图文档。", "提示", this);
                    return;
                }

                using (doc.LockDocument())
                {
                    int cleaned = NoteService.CompactDrawingDatabase(doc.Database);
                    CadMessageBox.ShowInfo(string.Format("图纸批注健康体检完成！\n已扫描并清理无效/孤儿数据 {0} 项，图纸批注数据库处于健康运行状态。", cleaned), 
                        "健康体检报告", this);
                }
            }
            catch (Exception ex)
            {
                CadMessageBox.ShowError("执行健康体检时发生异常: " + ex.Message, "错误", this);
            }
        }
        #endregion

        private void UpdatePreview()
        {
            if (CanvasCloudPreview == null) return;
            CanvasCloudPreview.Children.Clear();

            double arc = SliderArcLength != null ? SliderArcLength.Value : 10.0;
            double bulge = SliderBulge != null ? SliderBulge.Value : 0.52;
            double textH = SliderTextHeight != null ? SliderTextHeight.Value : 3.5;
            double cloudWidth = GetSelectedCloudWidth();
            int cloudStyle = CmbCloudStyle != null && CmbCloudStyle.SelectedIndex >= 0 ? CmbCloudStyle.SelectedIndex : 1;

            // 获取选定的云线颜色和文字颜色
            Color cloudColor = Color.FromRgb(255, 68, 68);
            if (CmbCloudColor != null && CmbCloudColor.SelectedItem is ColorItem cci && cci.Brush is SolidColorBrush scb)
            {
                cloudColor = scb.Color;
                if (cci.ColorIndex == 7 || cci.ColorIndex == 256)
                {
                    cloudColor = Color.FromRgb(0, 229, 255); // 白色/随层在预览框使用青色高对比呈现
                }
            }

            Color textColor = Color.FromRgb(0, 200, 255);
            if (CmbTextColor != null && CmbTextColor.SelectedItem is ColorItem tci && tci.Brush is SolidColorBrush tscb)
            {
                textColor = tscb.Color;
                if (tci.ColorIndex == 7 || tci.ColorIndex == 256)
                {
                    textColor = Color.FromRgb(255, 255, 255);
                }
            }

            // 动态测量 Canvas 实际物理视口尺寸并自适应水平垂直居中
            double canvasW = CanvasCloudPreview.ActualWidth > 50.0 ? CanvasCloudPreview.ActualWidth : 320.0;
            double canvasH = CanvasCloudPreview.ActualHeight > 15.0 ? CanvasCloudPreview.ActualHeight : 38.0;

            // 计算居中包围盒 (预留边距容纳饱满向外隆起的圆弧与毛笔笔锋)
            double padX = 14.0;
            double padY = 6.0;
            double boxW = Math.Max(60.0, canvasW - padX * 2.0);
            double boxH = Math.Max(16.0, canvasH - padY * 2.0);

            double x0 = (canvasW - boxW) / 2.0;
            double y0 = (canvasH - boxH) / 2.0;
            double x1 = x0 + boxW;
            double y1 = y0 + boxH;

            // 依据设定弧长进行对称整除步长细分，杜绝拐角出现破碎小残弧
            double targetStep = Math.Max(12.0, arc * 1.4);
            int countX = Math.Max(4, (int)Math.Round(boxW / targetStep));
            double stepX = boxW / (double)countX;
            int countY = Math.Max(2, (int)Math.Round(boxH / targetStep));
            double stepY = boxH / (double)countY;

            // 基于凸度数学公式求得真实圆弧半径: R = step / (2 * sin(theta/2))
            double absBulge = Math.Abs(bulge > 0.05 ? bulge : 0.520567);
            double theta = 4.0 * Math.Atan(absBulge);
            double sinHalf = Math.Sin(theta / 2.0);
            double rx = sinHalf > 0.01 ? Math.Abs(stepX / (2.0 * sinHalf)) : stepX * 0.65;
            double ry = sinHalf > 0.01 ? Math.Abs(stepY / (2.0 * sinHalf)) : stepY * 0.65;
            Size sizeX = new Size(rx, rx);
            Size sizeY = new Size(ry, ry);

            double strokeThickness = cloudWidth > 0 ? Math.Max(2.0, cloudWidth * 2.5) : 2.0;

            // 1. 底层半透明闭合充填区域 (所有样式通用，方向严格 Clockwise 顺时针外凸饱满)
            Path fillPath = new Path
            {
                Fill = new SolidColorBrush(Color.FromArgb(28, cloudColor.R, cloudColor.G, cloudColor.B))
            };
            PathGeometry fillGeom = new PathGeometry();
            PathFigure fillFig = new PathFigure { StartPoint = new Point(x0, y0), IsClosed = true };

            // Top: Left -> Right
            for (int i = 0; i < countX; i++)
                fillFig.Segments.Add(new ArcSegment(new Point(x0 + (i + 1) * stepX, y0), sizeX, 0, false, SweepDirection.Clockwise, true));
            // Right: Top -> Bottom
            for (int i = 0; i < countY; i++)
                fillFig.Segments.Add(new ArcSegment(new Point(x1, y0 + (i + 1) * stepY), sizeY, 0, false, SweepDirection.Clockwise, true));
            // Bottom: Right -> Left
            for (int i = 0; i < countX; i++)
                fillFig.Segments.Add(new ArcSegment(new Point(x1 - (i + 1) * stepX, y1), sizeX, 0, false, SweepDirection.Clockwise, true));
            // Left: Bottom -> Top
            for (int i = 0; i < countY; i++)
                fillFig.Segments.Add(new ArcSegment(new Point(x0, y1 - (i + 1) * stepY), sizeY, 0, false, SweepDirection.Clockwise, true));

            fillGeom.Figures.Add(fillFig);
            fillPath.Data = fillGeom;
            CanvasCloudPreview.Children.Add(fillPath);

            // 2. 云线轮廓：普通等宽 vs 书法笔锋
            if (cloudStyle == 1) // 书法笔锋 (起笔细、落笔粗变截面，呈现图 2 效果)
            {
                double taperW = Math.Max(2.2, Math.Min(4.8, stepX * 0.18));
                Path calligraphyPath = new Path
                {
                    Fill = new SolidColorBrush(cloudColor)
                };
                PathGeometry calligGeom = new PathGeometry();

                Action<Point, Point, Vector, Size> addWedge = (p1, p2, inNormal, sz) =>
                {
                    PathFigure wf = new PathFigure { StartPoint = p1, IsClosed = true };
                    // 外弧：顺时针外凸
                    wf.Segments.Add(new ArcSegment(p2, sz, 0, false, SweepDirection.Clockwise, true));
                    // 拐点落笔粗截面
                    Point pInner = p2 + inNormal * taperW;
                    wf.Segments.Add(new LineSegment(pInner, true));
                    // 内弧：逆时针收回起笔细点
                    wf.Segments.Add(new ArcSegment(p1, new Size(Math.Max(1.0, sz.Width - taperW * 0.4), Math.Max(1.0, sz.Height - taperW * 0.4)), 0, false, SweepDirection.Counterclockwise, true));
                    calligGeom.Figures.Add(wf);
                };

                for (int i = 0; i < countX; i++)
                    addWedge(new Point(x0 + i * stepX, y0), new Point(x0 + (i + 1) * stepX, y0), new Vector(0, 1), sizeX);
                for (int i = 0; i < countY; i++)
                    addWedge(new Point(x1, y0 + i * stepY), new Point(x1, y0 + (i + 1) * stepY), new Vector(-1, 0), sizeY);
                for (int i = 0; i < countX; i++)
                    addWedge(new Point(x1 - i * stepX, y1), new Point(x1 - (i + 1) * stepX, y1), new Vector(0, -1), sizeX);
                for (int i = 0; i < countY; i++)
                    addWedge(new Point(x0, y1 - i * stepY), new Point(x0, y1 - (i + 1) * stepY), new Vector(1, 0), sizeY);

                calligraphyPath.Data = calligGeom;
                CanvasCloudPreview.Children.Add(calligraphyPath);
            }
            else // 普通样式：全局等宽
            {
                Path strokePath = new Path
                {
                    Stroke = new SolidColorBrush(cloudColor),
                    StrokeThickness = strokeThickness
                };
                PathGeometry strokeGeom = new PathGeometry();
                PathFigure strokeFig = new PathFigure { StartPoint = new Point(x0, y0), IsClosed = true };

                for (int i = 0; i < countX; i++)
                    strokeFig.Segments.Add(new ArcSegment(new Point(x0 + (i + 1) * stepX, y0), sizeX, 0, false, SweepDirection.Clockwise, true));
                for (int i = 0; i < countY; i++)
                    strokeFig.Segments.Add(new ArcSegment(new Point(x1, y0 + (i + 1) * stepY), sizeY, 0, false, SweepDirection.Clockwise, true));
                for (int i = 0; i < countX; i++)
                    strokeFig.Segments.Add(new ArcSegment(new Point(x1 - (i + 1) * stepX, y1), sizeX, 0, false, SweepDirection.Clockwise, true));
                for (int i = 0; i < countY; i++)
                    strokeFig.Segments.Add(new ArcSegment(new Point(x0, y1 - (i + 1) * stepY), sizeY, 0, false, SweepDirection.Clockwise, true));

                strokeGeom.Figures.Add(strokeFig);
                strokePath.Data = strokeGeom;
                CanvasCloudPreview.Children.Add(strokePath);
            }

            // 绘制批注示例文字 (自动测量尺寸并精确在视窗与云线框中居中对齐)
            TextBlock txt = new TextBlock
            {
                Text = cloudStyle == 1 ? "NOTE-01 示例批注 - 书法笔锋" : "NOTE-01 示例批注 - 普通等宽",
                Foreground = new SolidColorBrush(textColor),
                FontSize = Math.Max(9.0, textH * 3.0),
                FontWeight = FontWeights.SemiBold
            };
            txt.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double txtW = txt.DesiredSize.Width;
            double txtHReal = txt.DesiredSize.Height;
            double txtX = Math.Max(x0 + 4.0, (canvasW - txtW) / 2.0);
            double txtY = (canvasH - txtHReal) / 2.0;
            Canvas.SetLeft(txt, txtX);
            Canvas.SetTop(txt, txtY);
            CanvasCloudPreview.Children.Add(txt);
        }

        private void BtnHelp_Click(object sender, RoutedEventArgs e)
        {
            HelpService.OpenReadme();
        }

        private void LnkHelp_Click(object sender, RoutedEventArgs e)
        {
            HelpService.OpenReadme();
        }

        private void BtnExecCmd_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string cmd)
            {
                if (cmd.Equals("CNHELP", StringComparison.OrdinalIgnoreCase) || cmd.Equals("CNH", StringComparison.OrdinalIgnoreCase))
                {
                    HelpService.OpenReadme();
                    return;
                }

                try
                {
                    var doc = AcApp.DocumentManager.MdiActiveDocument;
                    if (doc != null)
                    {
                        this.DialogResult = true;
                        this.Close();
                        doc.SendStringToExecute(cmd + "\n", true, false, false);
                    }
                }
                catch { }
            }
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
                e.Handled = true;
            }
            catch { }
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            if (CadMessageBox.Confirm("确定要将所有设置恢复为系统默认值吗？", "提示", this))
            {
                ConfigManager.ResetToDefaults();
                LoadSettingsToUI();
                UpdatePreview();
            }
        }

        private bool ApplyAndSaveSettings()
        {
            var s = ConfigManager.Instance.CurrentSettings;

            // 1. 图层
            s.CloudLayer = string.IsNullOrEmpty(TxtCloudLayer.Text.Trim()) ? "CAD_NOTE_CLOUD" : TxtCloudLayer.Text.Trim();
            s.TextLayer = string.IsNullOrEmpty(TxtTextLayer.Text.Trim()) ? "CAD_NOTE_TEXT" : TxtTextLayer.Text.Trim();
            s.ForceNonPlotting = ChkNonPlotting.IsChecked == true;
            string selectedStyle = CmbTextStyle.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedStyle)) selectedStyle = CmbTextStyle.Text;
            s.TextStyleName = string.IsNullOrEmpty(selectedStyle?.Trim()) ? "Standard" : selectedStyle.Trim();
            s.CloudColorIndex = GetColorFromCombo(CmbCloudColor);
            s.TextColorIndex = GetColorFromCombo(CmbTextColor);

            // 2. 几何
            s.ArcLengthRatio = SliderArcLength.Value;
            s.BulgeCurvature = SliderBulge.Value;
            if (CmbCloudStyle != null && CmbCloudStyle.SelectedIndex >= 0) s.CloudStyle = CmbCloudStyle.SelectedIndex;
            s.CloudWidth = GetSelectedCloudWidth();
            s.DefaultCloudType = CmbCloudType.SelectedIndex;
            if (double.TryParse(TxtMinArc.Text.Trim(), out double minArc)) s.MinArcLength = Math.Max(1.0, minArc);
            if (double.TryParse(TxtMaxArc.Text.Trim(), out double maxArc)) s.MaxArcLengthRatio = Math.Max(s.ArcLengthRatio, maxArc);

            // 3. 文字与引线
            s.TextHeightRatio = SliderTextHeight.Value;
            s.ArrowSizeRatio = SliderArrowSize.Value;
            s.LeaderType = CmbLeaderType.SelectedIndex;
            s.AutoNumberPrefix = string.IsNullOrEmpty(TxtPrefix.Text.Trim()) ? "【" : TxtPrefix.Text.Trim();

            // 4. 业务
            s.DefaultDiscipline = CmbDiscipline.SelectedItem as string ?? "建筑";
            s.DefaultAssignee = TxtAssignee.Text.Trim();
            s.DefaultPriority = CmbPriority.SelectedItem as string ?? "重要";
            s.AutoZoomOnSelect = ChkAutoZoom.IsChecked == true;
            s.EnableSoundNotification = ChkSound.IsChecked == true;
            s.EnableSimpleNoteMode = ChkEnableSimpleNoteMode.IsChecked == true;
            string rapidPhrase = CmbRapidDefaultPhrase?.Text?.Trim();
            if (string.IsNullOrEmpty(rapidPhrase) && CmbBottomRapidPhrase?.SelectedItem is string botSel) rapidPhrase = botSel;
            s.RapidDefaultPhrase = string.IsNullOrEmpty(rapidPhrase) ? "待修改" : rapidPhrase;
            s.EnableRapidCmdKeySelection = ChkEnableRapidCmdKey?.IsChecked == true;
            if (CmbProjectTemplates?.SelectedItem is AnnotationTemplate selTpl)
            {
                s.CurrentTemplateId = selTpl.Id;
            }

            // 5. 规范与 AI
            s.EnableAiSemantic = ChkEnableAi.IsChecked == true;
            s.AiSimilarityThreshold = SliderAiThreshold.Value;
            s.AutoFillStandardCode = ChkAutoFillCode.IsChecked == true;

            // 6. 存储与审计
            if (int.TryParse(TxtMaxNotesVolume.Text.Trim(), out int maxV)) s.MaxNotesPerVolume = Math.Max(10, maxV);
            s.EnableAuditTrail = ChkAuditTrail.IsChecked == true;
            s.AutoBackupOnSave = ChkAutoBackup.IsChecked == true;

            // 7. 高级联动设置
            if (ChkPriorityColorLink != null) s.EnablePriorityColorLink = ChkPriorityColorLink.IsChecked == true;
            if (CmbColorImportant != null) s.PriorityColorImportant = GetColorFromCombo(CmbColorImportant);
            if (CmbColorUrgent != null) s.PriorityColorUrgent = GetColorFromCombo(CmbColorUrgent);


            // 8. 若当前有活动 AutoCAD 文档，即刻将最新图层配置（双图层与不打印状态）热同步至当前图纸
            try
            {
                var doc = AcApp.DocumentManager?.MdiActiveDocument;
                if (doc?.Database != null)
                {
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        ValidationService.EnsureNoteLayers(doc.Database, tr, s, NotePriority.Important);
                        tr.Commit();
                    }
                }
            }
            catch { }

            ConfigManager.Instance.Save();
            return true;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ApplyAndSaveSettings();
                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                CadMessageBox.ShowError("保存配置时发生错误: " + ex.Message, "错误", this);
            }
        }

        private void BtnSaveAndCreate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ApplyAndSaveSettings();
                this.DialogResult = true;
                this.Close();

                var doc = AcApp.DocumentManager.MdiActiveDocument;
                doc?.SendStringToExecute("CNOTE\n", true, false, false);
            }
            catch (Exception ex)
            {
                CadMessageBox.ShowError("保存配置时发生错误: " + ex.Message, "错误", this);
            }
        }
    }
}
#endif
