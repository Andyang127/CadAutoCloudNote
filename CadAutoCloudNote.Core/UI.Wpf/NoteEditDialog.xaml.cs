#if USE_WPF
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CadAutoCloudNote.Core.Helpers;
using CadAutoCloudNote.Core.Models;
using CadAutoCloudNote.Core.Protocols;
using CadAutoCloudNote.Core.Services;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace CadAutoCloudNote.Core.UI.Wpf
{
    /// <summary>
    /// 现代化 Fluent 批注编辑对话框
    /// </summary>
    public partial class NoteEditDialog : Window
    {
        public string NoteTitle => CmbTitle.Text.Trim();
        public string NoteContent => TxtContent.Text.Trim();
        public string Discipline { get; private set; } = "建筑";
        public NotePriority Priority => (CmbPriority.SelectedIndex == 1) ? NotePriority.Urgent : NotePriority.Important;
        public string Assignee => TxtAssignee.Text.Trim();
        public string Prefix => CmbPrefix?.Text?.Trim() ?? "#";
        public int SequenceNumber { get; private set; } = 1;

        private bool _isInitializing = true;

        public NoteEditDialog(string defaultTitle = "", string defaultContent = "", string defaultDiscipline = "建筑", string defaultAssignee = "", NotePriority? defaultPriority = null, int defaultSeq = 1, string defaultPrefix = null)
        {
            InitializeComponent();
            try
            {
                var iconSource = IconHelper.GetAppImageSource();
                if (iconSource != null) this.Icon = iconSource;
            }
            catch { }
            ApplyAutoCadSmartTheme();

            var settings = Config.ConfigManager.Instance.CurrentSettings;
            var allTemplates = ProjectTemplateService.GetAllTemplates();
            if (CmbTargetPersona != null)
            {
                CmbTargetPersona.ItemsSource = allTemplates;
                var curTpl = allTemplates.Find(t => t.Id == settings.CurrentTemplateId) ?? ProjectTemplateService.CurrentTemplate ?? allTemplates[0];
                CmbTargetPersona.SelectedItem = curTpl;
                if (TxtTemplateBadge != null)
                {
                    TxtTemplateBadge.Text = curTpl.Name;
                }
            }

            Discipline = string.IsNullOrEmpty(defaultDiscipline) ? "建筑" : defaultDiscipline;
            InitDisciplineChips(Discipline);
            ApplyPersonaFilter(CmbTargetPersona?.SelectedItem as AnnotationTemplate ?? ProjectTemplateService.CurrentTemplate, defaultTitle);

            TxtContent.Text = defaultContent;
            TxtAssignee.Text = defaultAssignee;
            if (ChkSaveToLibrary != null)
            {
                ChkSaveToLibrary.IsChecked = settings.SaveToPhraseLibraryByDefault;
            }

            NotePriority initPriority = defaultPriority ?? Config.NoteSettings.ParsePriority(settings.DefaultPriority);

            InitPriorities(initPriority);
            _allKbItems = KnowledgeBaseService.GetOrganizedList();
            FilterKnowledgeBaseByDiscipline(Discipline);

            // 标号前缀与序号初始化
            if (CmbPrefix != null)
            {
                CmbPrefix.Items.Clear();
                CmbPrefix.Items.Add("【");
                CmbPrefix.Items.Add("#");
                CmbPrefix.Items.Add("NOTE-");
                CmbPrefix.Items.Add("批注-");
                CmbPrefix.Items.Add("审-");
                string initialPrefix = !string.IsNullOrEmpty(defaultPrefix) ? defaultPrefix : (settings.AutoNumberPrefix ?? "【");
                CmbPrefix.Text = initialPrefix;
                CmbPrefix.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent, new TextChangedEventHandler((s, e) => { if (!_isInitializing) UpdatePreviewCard(); }));
                CmbPrefix.SelectionChanged += (s, e) => { if (!_isInitializing) UpdatePreviewCard(); };
            }

            SequenceNumber = defaultSeq > 0 ? defaultSeq : 1;
            if (TxtSequence != null)
            {
                TxtSequence.Text = SequenceNumber.ToString();
            }

            CmbTitle.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent, new TextChangedEventHandler(CmbTitle_TextChanged));
            CmbTitle.SelectionChanged += CmbTitle_SelectionChanged;

            _isInitializing = false;
            UpdatePreviewCard();
        }

        private void CmbTargetPersona_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            if (CmbTargetPersona.SelectedItem is AnnotationTemplate tpl)
            {
                if (TxtTemplateBadge != null)
                {
                    TxtTemplateBadge.Text = tpl.Name;
                }
                var settings = Config.ConfigManager.Instance.CurrentSettings;
                ProjectTemplateService.ApplyTemplateToSettings(tpl.Id, settings);
                ApplyPersonaFilter(tpl, null);
                UpdatePreviewCard();
            }
        }

        private void ApplyPersonaFilter(AnnotationTemplate tpl, string preserveTitle = null)
        {
            if (tpl == null) return;
            RadioButton firstValidRb = null;
            RadioButton currentlyCheckedRb = null;

            foreach (var child in PanelDisciplineChips.Children)
            {
                if (child is RadioButton rb)
                {
                    string disc = rb.Content?.ToString();
                    bool applicable = tpl.IsDisciplineApplicable(disc);
                    rb.IsEnabled = applicable;
                    if (applicable && firstValidRb == null) firstValidRb = rb;
                    if (rb.IsChecked == true) currentlyCheckedRb = rb;
                }
            }

            if (currentlyCheckedRb != null && !currentlyCheckedRb.IsEnabled && firstValidRb != null)
            {
                firstValidRb.IsChecked = true;
                Discipline = firstValidRb.Content?.ToString() ?? "通用";
            }

            InitTitleComboBox(Discipline, preserveTitle, tpl);
        }

        private void InitTitleComboBox(string discipline, string defaultTitle, AnnotationTemplate tpl = null)
        {
            CmbTitle.Items.Clear();
            var set = new System.Collections.Generic.HashSet<string>();

            // 1. 优先载入当前使用对象的专属审查意见库
            if (tpl == null) tpl = CmbTargetPersona?.SelectedItem as AnnotationTemplate ?? ProjectTemplateService.CurrentTemplate;
            if (tpl?.QuickReviewSnippets != null)
            {
                foreach (var snippet in tpl.QuickReviewSnippets)
                {
                    if (set.Add(snippet))
                    {
                        CmbTitle.Items.Add(snippet);
                    }
                }
            }

            // 2. 载入当前专业的快捷短语
            var phrases = RapidPhraseService.GetByDiscipline(discipline);
            if (phrases == null || phrases.Count == 0)
            {
                phrases = RapidPhraseService.GetAll();
            }
            foreach (var p in phrases)
            {
                if (set.Add(p.Text))
                {
                    CmbTitle.Items.Add(p.Text);
                }
            }

            if (!string.IsNullOrEmpty(defaultTitle))
            {
                CmbTitle.Text = defaultTitle;
            }
            else if (CmbTitle.Items.Count > 0)
            {
                CmbTitle.SelectedIndex = 0;
            }
        }

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

            if (!isDarkTheme)
            {
                this.Resources["WindowBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F4F6F9"));
                this.Resources["CardBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
                this.Resources["TitleBarBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E9F0"));
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
                this.Resources["PreviewCardBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9"));
                this.Resources["ChipBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0"));
                this.Resources["ChipBorder"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1"));
                this.Resources["ChipDisabledBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC"));
                this.Resources["BadgeBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0F2FE"));
                this.Resources["BadgeBorder"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BAE6FD"));
            }
        }

        private void InitPriorities(NotePriority defaultPriority)
        {
            CmbPriority.Items.Clear();
            CmbPriority.Items.Add("重要");
            CmbPriority.Items.Add("紧急");

            switch (defaultPriority)
            {
                case NotePriority.Urgent:
                    CmbPriority.SelectedIndex = 1;
                    break;
                case NotePriority.Important:
                case NotePriority.Normal:
                default:
                    CmbPriority.SelectedIndex = 0;
                    break;
            }
        }

        private void InitDisciplineChips(string activeDiscipline)
        {
            foreach (var child in PanelDisciplineChips.Children)
            {
                if (child is RadioButton rb)
                {
                    if (string.Equals(rb.Content?.ToString(), activeDiscipline, StringComparison.OrdinalIgnoreCase))
                    {
                        rb.IsChecked = true;
                        break;
                    }
                }
            }
        }

        private System.Collections.Generic.List<KnowledgeItem> _allKbItems = new System.Collections.Generic.List<KnowledgeItem>();
        private System.Collections.Generic.List<KnowledgeItem> _loadedKbItems = new System.Collections.Generic.List<KnowledgeItem>();
        private KnowledgeItem _selectedKbItem = null;

        private void FilterKnowledgeBaseByDiscipline(string discipline)
        {
            if (CmbKnowledge == null) return;
            if (_allKbItems == null || _allKbItems.Count == 0)
            {
                _allKbItems = KnowledgeBaseService.GetOrganizedList();
            }

            _loadedKbItems.Clear();
            CmbKnowledge.Items.Clear();
            CmbKnowledge.Items.Add(string.Format("-- 援引现行工程规范 [{0}/通用] 自动填入意见详情 --", discipline ?? "通用"));

            var stdItems = new System.Collections.Generic.List<KnowledgeItem>();
            var customItems = new System.Collections.Generic.List<KnowledgeItem>();

            foreach (var item in _allKbItems)
            {
                if (string.IsNullOrEmpty(discipline) ||
                    discipline == "通用" ||
                    string.Equals(item.Discipline, discipline, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(item.Discipline, "通用", StringComparison.OrdinalIgnoreCase))
                {
                    if (!item.IsCustom) stdItems.Add(item);
                    else customItems.Add(item);
                }
            }

            foreach (var item in stdItems)
            {
                _loadedKbItems.Add(item);
                string code = string.IsNullOrEmpty(item.StandardCode) ? "" : string.Format(" ({0})", item.StandardCode);
                CmbKnowledge.Items.Add(string.Format("[规范·{0}] {1}{2}", item.Discipline, item.Title, code));
            }

            foreach (var item in customItems)
            {
                _loadedKbItems.Add(item);
                string code = string.IsNullOrEmpty(item.StandardCode) || item.StandardCode == "手动录入" ? "" : string.Format(" ({0})", item.StandardCode);
                CmbKnowledge.Items.Add(string.Format("[经验·{0}] {1}{2}", item.Discipline, item.Title, code));
            }

            CmbKnowledge.SelectedIndex = 0;
        }

        private void CmbKnowledge_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            if (CmbKnowledge.SelectedIndex > 0)
            {
                int idx = CmbKnowledge.SelectedIndex - 1;
                if (idx >= 0 && idx < _loadedKbItems.Count)
                {
                    var item = _loadedKbItems[idx];
                    _selectedKbItem = item;

                    // 1. 核心解耦规则：仅当【批注标题】为空或为默认占位短语时，才智能建议性地填入短标题；
                    //    若用户已经手动输入或选好了具体标题，绝对保留用户标题，决不冲刷覆盖！
                    string currentTitle = CmbTitle.Text.Trim();
                    bool isTitleEmptyOrDefault = string.IsNullOrEmpty(currentTitle) ||
                                                 currentTitle == "待修改" ||
                                                 currentTitle == "请核实" ||
                                                 currentTitle == NoteService.GetDefaultSimpleTitle();
                    if (isTitleEmptyOrDefault)
                    {
                        CmbTitle.Text = item.Title;
                    }

                    // 2. 规范条款精准回填至【审查意见详情】文本框，附带权威标准编号
                    string citation = string.IsNullOrEmpty(item.StandardCode) || item.StandardCode == "手动录入"
                        ? item.Suggestion
                        : item.Suggestion + " 【依据: " + item.StandardCode + "】";
                    TxtContent.Text = citation;

                    UpdatePreviewCard();

                    // 3. 记录规范调用频次
                    KnowledgeBaseService.IncrementUsage(item.Id);
                }
            }
        }

        private void DisciplineChip_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Content != null)
            {
                Discipline = rb.Content.ToString();
                if (!_isInitializing)
                {
                    string currentText = CmbTitle.Text;
                    InitTitleComboBox(Discipline, currentText, CmbTargetPersona?.SelectedItem as AnnotationTemplate);
                    FilterKnowledgeBaseByDiscipline(Discipline);
                    UpdatePreviewCard();
                }
            }
        }

        private void CmbTitle_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitializing)
            {
                UpdatePreviewCard();
            }
        }

        private void CmbTitle_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isInitializing)
            {
                UpdatePreviewCard();
            }
        }

        private void CmbPriority_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitializing)
            {
                UpdatePreviewCard();
            }
        }

        private void Inputs_Changed(object sender, TextChangedEventArgs e)
        {
            if (!_isInitializing)
            {
                // 手动输入审查意见时，保持上方批注标题与下拉选项不动
                UpdatePreviewCard();
            }
        }

        private void UpdatePreviewCard()
        {
            if (TxtPreviewTitle == null) return;

            string pfx = CmbPrefix != null && !string.IsNullOrEmpty(CmbPrefix.Text.Trim()) ? CmbPrefix.Text.Trim() : "【";
            int seqNum = 1;
            if (TxtSequence != null && int.TryParse(TxtSequence.Text.Trim(), out int parsedSeq) && parsedSeq > 0)
            {
                seqNum = parsedSeq;
            }
            string seqTag = NoteService.FormatSequencePrefix(pfx, seqNum);
            string rawTitle = string.IsNullOrEmpty(CmbTitle.Text.Trim()) ? "批注标题 (实时预览)" : CmbTitle.Text.Trim();
            if (rawTitle.StartsWith("【") && rawTitle.Contains("】"))
            {
                int cIdx = rawTitle.IndexOf('】');
                rawTitle = rawTitle.Substring(cIdx + 1).Trim();
            }
            string displayPreviewTitle = string.Format("{0} {1}", seqTag, rawTitle);

            string content = string.IsNullOrEmpty(TxtContent.Text.Trim()) ? "审查意见详情内容与依据规范..." : TxtContent.Text.Trim();
            string assignee = string.IsNullOrEmpty(TxtAssignee.Text.Trim()) ? Environment.UserName : TxtAssignee.Text.Trim();

            TxtPreviewTitle.Text = displayPreviewTitle;
            TxtPreviewContent.Text = content;
            TxtPreviewAssignee.Text = "批注者: " + assignee;
            TxtPreviewDiscipline.Text = Discipline;

            // 优先级徽标呈现 (二分化：重要 / 紧急)
            if (CmbPriority.SelectedIndex == 1) // 紧急
            {
                TxtPreviewPriority.Text = "紧急";
                BadgePriority.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626")); // 高度警示鲜红
            }
            else // 0 或默认: 重要
            {
                TxtPreviewPriority.Text = "重要";
                BadgePriority.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D97706")); // 琥珀警示橙
            }
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            // 1. 健壮性校验：非快捷批注必须录入具体审查意见或引用规范，严防空白无效批注
            string content = TxtContent.Text != null ? TxtContent.Text.Trim() : string.Empty;
            if (string.IsNullOrEmpty(content))
            {
                CadMessageBox.ShowWarning("请在【审查意见详情 / 规范条文】中输入具体审查意见，或从上方“依据规范条文速查”选择工程规范条文！", "输入提示");
                TxtContent.Focus();
                return;
            }

            string title = CmbTitle.Text.Trim();
            if (string.IsNullOrEmpty(title))
            {
                int nl = content.IndexOfAny(new[] { '\r', '\n', '。', '；', ';' });
                title = nl > 0 ? content.Substring(0, nl).Trim() : (content.Length > 15 ? content.Substring(0, 15).Trim() : content);
                CmbTitle.Text = title;
            }

            if (string.IsNullOrEmpty(title))
            {
                title = NoteService.GetDefaultSimpleTitle();
                CmbTitle.Text = title;
            }

            // 2. 记忆用户偏好设置 (持久化保存至本地 JSON 配置)
            var settings = Config.ConfigManager.Instance.CurrentSettings;
            settings.SaveToPhraseLibraryByDefault = (ChkSaveToLibrary != null && ChkSaveToLibrary.IsChecked == true);
            settings.DefaultDiscipline = Discipline;
            settings.DefaultPriority = (CmbPriority.SelectedIndex == 1) ? "紧急" : "重要";
            if (!string.IsNullOrEmpty(TxtAssignee.Text.Trim()))
            {
                settings.DefaultAssignee = TxtAssignee.Text.Trim();
            }
            if (CmbTargetPersona?.SelectedItem is AnnotationTemplate selTpl)
            {
                settings.CurrentTemplateId = selTpl.Id;
            }
            int userSeq = 1;
            if (TxtSequence != null && int.TryParse(TxtSequence.Text.Trim(), out int parsedSeq) && parsedSeq > 0)
            {
                userSeq = parsedSeq;
            }
            SequenceNumber = userSeq;
            string userPrefix = CmbPrefix != null && !string.IsNullOrEmpty(CmbPrefix.Text.Trim()) ? CmbPrefix.Text.Trim() : "【";
            settings.AutoNumberPrefix = userPrefix;
            Config.ConfigManager.Instance.SaveSettings(settings);

            // 3. 若勾选了【存入常用快捷短语库】，自动持久化到本地短语库与知识库
            if (ChkSaveToLibrary != null && ChkSaveToLibrary.IsChecked == true)
            {
                RapidPhraseService.AddOrUpdate(new RapidPhraseItem
                {
                    Text = title,
                    Discipline = Discipline,
                    IsCustom = true
                });
                RapidPhraseService.IncrementUsage(title);

                if (_selectedKbItem != null && _selectedKbItem.Title == title)
                {
                    KnowledgeBaseService.IncrementUsage(_selectedKbItem.Id);
                }
                else
                {
                    string std = (_selectedKbItem != null && !string.IsNullOrEmpty(_selectedKbItem.StandardCode))
                        ? _selectedKbItem.StandardCode
                        : string.Empty;
                    KnowledgeBaseService.SaveUserCustomItem(title, Discipline, content, std);
                }
            }

            this.DialogResult = true;
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
#endif
