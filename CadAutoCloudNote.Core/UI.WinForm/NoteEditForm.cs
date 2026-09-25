using System;
using System.Drawing;
using System.Windows.Forms;
using CadAutoCloudNote.Core.Models;
using CadAutoCloudNote.Core.Protocols;
using CadAutoCloudNote.Core.Services;

namespace CadAutoCloudNote.Core.UI.WinForm
{
    /// <summary>
    /// 批注新建与编辑对话框（WinForms 通用实现，适配 AutoCAD 2007~2012）
    /// </summary>
    public class NoteEditForm : Form
    {
        private ComboBox cmbTargetPersona;
        private ComboBox cmbPrefix;
        private TextBox txtSequence;
        private ComboBox cmbTitle;
        private TextBox txtContent;
        private ComboBox cmbDiscipline;
        private ComboBox cmbPriority;
        private TextBox txtAssignee;
        private ComboBox cmbKnowledge;
        private CheckBox chkSaveToLibrary;
        private Button btnOk;
        private Button btnCancel;
        private System.Collections.Generic.List<KnowledgeItem> _allKbItems = new System.Collections.Generic.List<KnowledgeItem>();
        private System.Collections.Generic.List<KnowledgeItem> _loadedKbItems = new System.Collections.Generic.List<KnowledgeItem>();

        public string NoteTitle => cmbTitle.Text.Trim();
        public string NoteContent => txtContent.Text.Trim();
        public string Discipline => cmbDiscipline.SelectedItem as string ?? "建筑";
        public NotePriority Priority => (cmbPriority.SelectedIndex == 1) ? NotePriority.Urgent : NotePriority.Important;
        public string Assignee => txtAssignee.Text.Trim();
        public string Prefix => cmbPrefix?.Text?.Trim() ?? "【";
        public int SequenceNumber { get; private set; } = 1;

        public NoteEditForm(string defaultTitle = "", string defaultContent = "", string defaultDiscipline = "建筑", string defaultAssignee = "", NotePriority? defaultPriority = null, int defaultSeq = 1, string defaultPrefix = null)
        {
            InitializeComponent();
            try
            {
                var appIcon = CadAutoCloudNote.Core.Helpers.IconHelper.GetAppIcon();
                if (appIcon != null) this.Icon = appIcon;
            }
            catch { }

            var settings = Config.ConfigManager.Instance.CurrentSettings;
            var allTemplates = ProjectTemplateService.GetAllTemplates();
            foreach (var t in allTemplates)
            {
                cmbTargetPersona.Items.Add(t);
            }
            cmbTargetPersona.DisplayMember = "Name";
            var curTpl = allTemplates.Find(t => t.Id == settings.CurrentTemplateId)
                         ?? ProjectTemplateService.CurrentTemplate
                         ?? allTemplates[0];
            cmbTargetPersona.SelectedItem = curTpl;
            cmbTargetPersona.SelectedIndexChanged += CmbTargetPersona_SelectedIndexChanged;

            ApplyPersonaToForm(curTpl, string.IsNullOrEmpty(defaultDiscipline) ? "建筑" : defaultDiscipline, defaultTitle);

            txtContent.Text = defaultContent;
            txtAssignee.Text = defaultAssignee;

            NotePriority initPriority = defaultPriority ?? Config.NoteSettings.ParsePriority(settings.DefaultPriority);
            cmbPriority.SelectedIndex = (initPriority == NotePriority.Urgent) ? 1 : 0;
            _allKbItems = KnowledgeBaseService.GetOrganizedList();
            FilterKnowledgeBaseByDiscipline(Discipline);

            // 前缀与序号
            if (cmbPrefix != null)
            {
                cmbPrefix.Items.Clear();
                cmbPrefix.Items.AddRange(new object[] { "【", "#", "NOTE-", "批注-", "审-" });
                string initialPrefix = !string.IsNullOrEmpty(defaultPrefix) ? defaultPrefix : (settings.AutoNumberPrefix ?? "【");
                cmbPrefix.Text = initialPrefix;
            }
            SequenceNumber = defaultSeq > 0 ? defaultSeq : 1;
            if (txtSequence != null)
            {
                txtSequence.Text = SequenceNumber.ToString();
            }
        }

        private void InitializeComponent()
        {
            var settings = Config.ConfigManager.Instance.CurrentSettings;
            this.Text = "新建/编辑云线批注";
            this.Size = new Size(460, 520);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Microsoft YaHei", 9F, FontStyle.Regular, GraphicsUnit.Point);

            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 7,
                Padding = new Padding(16, 12, 16, 8)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 74F));

            // 0. 批注使用对象 (核心驱动)
            layout.Controls.Add(new Label { Text = "使用对象*:", AutoSize = true, Anchor = AnchorStyles.Left, Font = new Font("Microsoft YaHei", 9F, FontStyle.Bold) }, 0, 0);
            cmbTargetPersona = new ComboBox { Width = 295, DropDownStyle = ComboBoxStyle.DropDownList };
            layout.Controls.Add(cmbTargetPersona, 1, 0);

            // 1. 所属专业置顶
            layout.Controls.Add(new Label { Text = "所属专业*:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
            cmbDiscipline = new ComboBox { Width = 295, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbDiscipline.SelectedIndexChanged += CmbDiscipline_SelectedIndexChanged;
            layout.Controls.Add(cmbDiscipline, 1, 1);

            // 2. 标号前缀与序号 (递加)
            layout.Controls.Add(new Label { Text = "标号与序号:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
            FlowLayoutPanel seqPanel = new FlowLayoutPanel { Width = 295, Height = 28, Margin = new Padding(0) };
            cmbPrefix = new ComboBox { Width = 90, DropDownStyle = ComboBoxStyle.DropDown };
            seqPanel.Controls.Add(new Label { Text = "前缀:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 5, 2, 0) });
            seqPanel.Controls.Add(cmbPrefix);
            seqPanel.Controls.Add(new Label { Text = "序号:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(8, 5, 2, 0) });
            txtSequence = new TextBox { Width = 70 };
            seqPanel.Controls.Add(txtSequence);
            layout.Controls.Add(seqPanel, 1, 2);

            // 3. 批注标题（支持下拉速选与手动录入）
            layout.Controls.Add(new Label { Text = "批注标题*:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
            cmbTitle = new ComboBox { Width = 295, DropDownStyle = ComboBoxStyle.DropDown };
            layout.Controls.Add(cmbTitle, 1, 3);

            // 4. 问题等级与批注者
            layout.Controls.Add(new Label { Text = "等级与人员:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 4);
            FlowLayoutPanel subPanel = new FlowLayoutPanel { Width = 295, Height = 28, Margin = new Padding(0) };
            cmbPriority = new ComboBox { Width = 80, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbPriority.Items.AddRange(new object[] { "重要", "紧急" });

            subPanel.Controls.Add(new Label { Text = "等级:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 5, 2, 0) });
            subPanel.Controls.Add(cmbPriority);
            subPanel.Controls.Add(new Label { Text = "批注者:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(8, 5, 2, 0) });
            txtAssignee = new TextBox { Width = 100 };
            subPanel.Controls.Add(txtAssignee);
            layout.Controls.Add(subPanel, 1, 4);

            // 5. 依据规范条文速查
            layout.Controls.Add(new Label { Text = "援引规范标准:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 5);
            cmbKnowledge = new ComboBox { Width = 295, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbKnowledge.SelectedIndexChanged += CmbKnowledge_SelectedIndexChanged;
            layout.Controls.Add(cmbKnowledge, 1, 5);

            // 6. 审查意见详情
            layout.Controls.Add(new Label { Text = "审查意见详情:", AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Left }, 0, 6);
            txtContent = new TextBox { Width = 295, Height = 95, Multiline = true, ScrollBars = ScrollBars.Vertical };
            layout.Controls.Add(txtContent, 1, 6);

            // 底部按钮与存库复选框
            FlowLayoutPanel btnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 45,
                Padding = new Padding(8)
            };

            btnCancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Width = 80, Height = 28 };
            btnOk = new Button { Text = "确定生成", DialogResult = DialogResult.OK, Width = 85, Height = 28 };
            btnOk.Click += BtnOk_Click;

            chkSaveToLibrary = new CheckBox
            {
                Text = "存入常用快捷短语库",
                Checked = settings.SaveToPhraseLibraryByDefault,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(8, 6, 8, 6)
            };

            btnPanel.Controls.Add(btnCancel);
            btnPanel.Controls.Add(btnOk);
            btnPanel.Controls.Add(chkSaveToLibrary);

            this.Controls.Add(layout);
            this.Controls.Add(btnPanel);
            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }

        private void CmbTargetPersona_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbTargetPersona.SelectedItem is AnnotationTemplate tpl)
            {
                ApplyPersonaToForm(tpl, Discipline, null);
                var settings = Config.ConfigManager.Instance.CurrentSettings;
                ProjectTemplateService.ApplyTemplateToSettings(tpl.Id, settings);
            }
        }

        private void ApplyPersonaToForm(AnnotationTemplate tpl, string preferredDiscipline, string defaultTitle)
        {
            if (tpl == null) return;
            this.Text = string.Format("新建/编辑云线批注 [{0}]", tpl.Name);

            // 联动适用专业：选定对象后，不相关专业自动过滤禁用
            string[] allDiscs = new string[] { "建筑", "结构", "给排水", "暖通", "电气", "总图", "通用" };
            cmbDiscipline.Items.Clear();
            foreach (var d in allDiscs)
            {
                if (tpl.IsDisciplineApplicable(d))
                {
                    cmbDiscipline.Items.Add(d);
                }
            }
            if (cmbDiscipline.Items.Count > 0)
            {
                int idx = cmbDiscipline.Items.IndexOf(preferredDiscipline);
                cmbDiscipline.SelectedIndex = idx >= 0 ? idx : 0;
            }

            ReloadPhrasesForDiscipline(Discipline, defaultTitle, tpl);
        }

        private void CmbDiscipline_SelectedIndexChanged(object sender, EventArgs e)
        {
            ReloadPhrasesForDiscipline(Discipline, cmbTitle?.Text, cmbTargetPersona?.SelectedItem as AnnotationTemplate);
            FilterKnowledgeBaseByDiscipline(Discipline);
        }

        private void FilterKnowledgeBaseByDiscipline(string discipline)
        {
            if (cmbKnowledge == null) return;
            if (_allKbItems == null || _allKbItems.Count == 0)
            {
                _allKbItems = KnowledgeBaseService.GetOrganizedList();
            }

            _loadedKbItems.Clear();
            cmbKnowledge.Items.Clear();
            cmbKnowledge.Items.Add(string.Format("-- 援引现行工程规范 [{0}/通用] 自动填入意见详情 --", discipline ?? "通用"));

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
                cmbKnowledge.Items.Add(string.Format("[规范·{0}] {1}{2}", item.Discipline, item.Title, code));
            }

            foreach (var item in customItems)
            {
                _loadedKbItems.Add(item);
                string code = string.IsNullOrEmpty(item.StandardCode) || item.StandardCode == "手动录入" ? "" : string.Format(" ({0})", item.StandardCode);
                cmbKnowledge.Items.Add(string.Format("[经验·{0}] {1}{2}", item.Discipline, item.Title, code));
            }

            cmbKnowledge.SelectedIndex = 0;
        }

        private void ReloadPhrasesForDiscipline(string discipline, string preserveText = null, AnnotationTemplate tpl = null)
        {
            if (cmbTitle == null) return;
            string currentText = preserveText ?? cmbTitle.Text;
            cmbTitle.Items.Clear();
            var set = new System.Collections.Generic.HashSet<string>();

            if (tpl == null) tpl = cmbTargetPersona?.SelectedItem as AnnotationTemplate ?? ProjectTemplateService.CurrentTemplate;
            if (tpl?.QuickReviewSnippets != null)
            {
                foreach (var snippet in tpl.QuickReviewSnippets)
                {
                    if (set.Add(snippet))
                    {
                        cmbTitle.Items.Add(snippet);
                    }
                }
            }

            var phrases = RapidPhraseService.GetByDiscipline(discipline);
            if (phrases == null || phrases.Count == 0)
            {
                phrases = RapidPhraseService.GetAll();
            }
            foreach (var p in phrases)
            {
                if (set.Add(p.Text))
                {
                    cmbTitle.Items.Add(p.Text);
                }
            }
            if (!string.IsNullOrEmpty(currentText))
            {
                cmbTitle.Text = currentText;
            }
            else if (cmbTitle.Items.Count > 0)
            {
                cmbTitle.SelectedIndex = 0;
            }
        }

        private void CmbKnowledge_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbKnowledge.SelectedIndex > 0)
            {
                int idx = cmbKnowledge.SelectedIndex - 1;
                if (idx >= 0 && idx < _loadedKbItems.Count)
                {
                    var item = _loadedKbItems[idx];
                    _selectedKbItem = item;

                    // 1. 核心解耦规则：仅当【批注标题】为空或为默认占位词时才填入短标题；用户若已有具体标题绝不冲刷覆盖！
                    string currentTitle = cmbTitle.Text.Trim();
                    bool isTitleEmptyOrDefault = string.IsNullOrEmpty(currentTitle) ||
                                                 currentTitle == "待修改" ||
                                                 currentTitle == "请核实" ||
                                                 currentTitle == NoteService.GetDefaultSimpleTitle();
                    if (isTitleEmptyOrDefault)
                    {
                        cmbTitle.Text = item.Title;
                    }

                    // 2. 规范条款精准回填至【审查意见详情】文本框，附带权威标准编号
                    string citation = string.IsNullOrEmpty(item.StandardCode) || item.StandardCode == "手动录入"
                        ? item.Suggestion
                        : item.Suggestion + " 【依据: " + item.StandardCode + "】";
                    txtContent.Text = citation;

                    KnowledgeBaseService.IncrementUsage(item.Id);
                }
            }
        }

        private KnowledgeItem _selectedKbItem = null;

        private void BtnOk_Click(object sender, EventArgs e)
        {
            // 1. 健壮性校验：非快捷批注必须录入具体审查意见或引用规范，严防空白无效批注
            string content = txtContent.Text != null ? txtContent.Text.Trim() : string.Empty;
            if (string.IsNullOrEmpty(content))
            {
                CadWinFormMessageBox.ShowWarning("请在【审查意见详情】中输入具体审查意见，或从上方“援引规范标准”选择工程规范条文！", "输入提示", this);
                this.DialogResult = DialogResult.None;
                txtContent.Focus();
                return;
            }

            string title = cmbTitle.Text.Trim();
            if (string.IsNullOrEmpty(title))
            {
                int nl = content.IndexOfAny(new[] { '\r', '\n', '。', '；', ';' });
                title = nl > 0 ? content.Substring(0, nl).Trim() : (content.Length > 15 ? content.Substring(0, 15).Trim() : content);
                cmbTitle.Text = title;
            }

            if (string.IsNullOrEmpty(title))
            {
                title = NoteService.GetDefaultSimpleTitle();
                cmbTitle.Text = title;
            }

            // 2. 记忆用户偏好设置 (持久化保存至本地 JSON 配置)
            var settings = Config.ConfigManager.Instance.CurrentSettings;
            settings.SaveToPhraseLibraryByDefault = (chkSaveToLibrary != null && chkSaveToLibrary.Checked);
            settings.DefaultDiscipline = Discipline;
            settings.DefaultPriority = (cmbPriority.SelectedIndex == 1) ? "紧急" : "重要";
            if (!string.IsNullOrEmpty(txtAssignee.Text.Trim()))
            {
                settings.DefaultAssignee = txtAssignee.Text.Trim();
            }
            if (cmbTargetPersona?.SelectedItem is AnnotationTemplate selTpl)
            {
                settings.CurrentTemplateId = selTpl.Id;
            }
            int userSeq = 1;
            if (txtSequence != null && int.TryParse(txtSequence.Text.Trim(), out int parsedSeq) && parsedSeq > 0)
            {
                userSeq = parsedSeq;
            }
            SequenceNumber = userSeq;
            string userPrefix = cmbPrefix != null && !string.IsNullOrEmpty(cmbPrefix.Text.Trim()) ? cmbPrefix.Text.Trim() : "#";
            settings.AutoNumberPrefix = userPrefix;
            Config.ConfigManager.Instance.SaveSettings(settings);

            // 3. 若勾选了【存入常用快捷短语库】，自动持久化到本地短语库与知识库
            if (chkSaveToLibrary != null && chkSaveToLibrary.Checked)
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

            this.Close();
        }
    }
}
