#pragma warning disable CA1416
#pragma warning disable WFO1000
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using CadAutoCloudNote.Core.Config;
using CadAutoCloudNote.Core.Services;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace CadAutoCloudNote.Core.UI.WinForm
{
    /// <summary>
    /// 批注全局参数设置窗体（自适应暗黑/高亮风，支持 AutoCAD 2007~2027 全版本 WinForms/降级模式）
    /// </summary>
    public class ConfigForm : Form
    {
        private Color WindowBg = ColorTranslator.FromHtml("#F4F6F9");
        private Color CardBg = ColorTranslator.FromHtml("#FFFFFF");
        private Color TitleBarBg = ColorTranslator.FromHtml("#E5E9F0");
        private Color HeroBarBg = ColorTranslator.FromHtml("#EDF1F7");
        private Color HeroBarText = ColorTranslator.FromHtml("#1E293B");
        private Color TagBg = ColorTranslator.FromHtml("#E2EEFA");
        private Color TagText = ColorTranslator.FromHtml("#0070D2");
        private Color TextPrimary = ColorTranslator.FromHtml("#1E293B");
        private Color TextSecondary = ColorTranslator.FromHtml("#64748B");
        private Color BorderLine = ColorTranslator.FromHtml("#CBD5E1");
        private Color ButtonBg = ColorTranslator.FromHtml("#E2E8F0");
        private Color ButtonHover = ColorTranslator.FromHtml("#CBD5E1");
        private Color AccentColor = ColorTranslator.FromHtml("#0070D2");
        private Color AccentHover = ColorTranslator.FromHtml("#005FB2");

        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        // 0. 板块 0：批注使用对象与规则
        private Label lblAudienceRoleTitle;
        private Label lblAudienceDesc;
        private Label lblAudienceLayer;
        private Label lblAudiencePrefix;
        private Label lblAudiencePlot;
        private FlowLayoutPanel pnlAudienceDisciplines;
        private SleekButton btnExportTemplateExcel;

        // 1. 板块 1：绘图与样式
        private TextBox txtCloudLayer;
        private TextBox txtTextLayer;
        private ComboBox cmbCloudColor;
        private ComboBox cmbTextColor;
        private TextBox txtTextStyle;
        private CheckBox chkNonPlottable;

        private TextBox txtArcLength;
        private TextBox txtBulge;
        private ComboBox cmbCloudStyle;
        private TextBox txtCloudWidth;
        private ComboBox cmbCloudType;
        private TextBox txtMinArc;

        private TextBox txtTextHeight;
        private TextBox txtArrowSize;
        private ComboBox cmbLeaderType;
        private TextBox txtPrefix;

        // 严重等级颜色联动
        private CheckBox chkPriorityColorLink;
        private ComboBox cmbColorImportant;
        private ComboBox cmbColorUrgent;

        // 2. 专业协同与工作流分配（置于板块0中展示与联动）
        private ComboBox cmbDiscipline;
        private TextBox txtAssignee;
        private ComboBox cmbPriority;

        // 3. 板块 2：极速与短语
        private CheckBox chkSimpleMode;
        private ComboBox cmbRapidDefaultPhrase;
        private CheckBox chkEnableRapidCmdKey;

        // 常用审查意见快捷词典
        private ListBox lstSnippets;
        private TextBox txtNewSnippet;
        private SleekButton btnAddSnippet;
        private SleekButton btnDeleteSnippet;
        private SleekButton btnResetSnippets;

        // 存储与审计 + 健康体检
        private TextBox txtMaxNotesVolume;
        private CheckBox chkAuditTrail;
        private CheckBox chkAutoBackup;
        private SleekButton btnCompactDb;

        // 4. 板块 3：规范知识库
        private CheckBox chkEnableAi;
        private TextBox txtAiThreshold;
        private CheckBox chkAutoFillCode;

        // 5. 板块 4：快捷与系统
        private CheckBox chkAutoZoom;
        private CheckBox chkSound;
        private Panel pnlCommands;

        // 批注使用对象顶置控制
        private ComboBox cmbTargetPersonaHero;
        private SleekButton btnApplyPersonaHero;

        // 底部操作按钮与极速快捷入口
        private SleekButton btnReset;
        private CheckBox chkBottomSimpleMode;
        private ComboBox cmbBottomRapidPhrase;
        private SleekButton btnCancel;
        private SleekButton btnOk;
        private SleekButton btnSaveAndCreate;

        private bool _isUpdatingFromCode = false;

        public ConfigForm()
        {
            try
            {
                var appIcon = CadAutoCloudNote.Core.Helpers.IconHelper.GetAppIcon();
                if (appIcon != null) this.Icon = appIcon;
            }
            catch { }
            ApplyAutoCadSmartTheme();
            InitializeComponent();
            LoadCurrentSettings();
            Action updateRegion = () => {
                try { this.Region = new Region(SleekPanel.GetRoundedPath(new Rectangle(0, 0, this.Width, this.Height), 8)); } catch { }
            };
            updateRegion();
            this.Resize += (s, e) => updateRegion();
            this.KeyPreview = true;
            this.KeyDown += (sender, e) =>
            {
                if (e.KeyCode == Keys.F1)
                {
                    e.Handled = true;
                    HelpService.OpenReadme();
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    this.DialogResult = DialogResult.Cancel;
                    this.Close();
                }
                else if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
                {
                    Control focused = GetFocusedControl();
                    if (!(focused is TextBox))
                    {
                        e.Handled = true;
                        e.SuppressKeyPress = true;
                        btnSaveAndCreate?.PerformClick();
                    }
                }
            };
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetFocus();

        private Control GetFocusedControl()
        {
            try
            {
                IntPtr handle = GetFocus();
                if (handle != IntPtr.Zero)
                {
                    return Control.FromChildHandle(handle);
                }
            }
            catch { }
            return this.ActiveControl;
        }

        private void ApplyAutoCadSmartTheme()
        {
            bool isDarkTheme = false;
            try
            {
#if CAD_R17 || CAD_R18 || CAD_R19
                isDarkTheme = false;
#elif !CAD_TEST
                if (AcApp.Version.Major >= 20)
                {
                    object themeVar = AcApp.GetSystemVariable("COLORTHEME");
                    if (themeVar != null && Convert.ToInt16(themeVar) == 0) isDarkTheme = true;
                }
#endif
            }
            catch { isDarkTheme = false; }

            if (!isDarkTheme)
            {
                WindowBg = ColorTranslator.FromHtml("#F4F6F9");
                CardBg = ColorTranslator.FromHtml("#FFFFFF");
                TitleBarBg = ColorTranslator.FromHtml("#E5E9F0");
                HeroBarBg = ColorTranslator.FromHtml("#EDF1F7");
                HeroBarText = ColorTranslator.FromHtml("#1E293B");
                TagBg = ColorTranslator.FromHtml("#E2EEFA");
                TagText = ColorTranslator.FromHtml("#0070D2");
                TextPrimary = ColorTranslator.FromHtml("#1E293B");
                TextSecondary = ColorTranslator.FromHtml("#64748B");
                BorderLine = ColorTranslator.FromHtml("#CBD5E1");
                ButtonBg = ColorTranslator.FromHtml("#E2E8F0");
                ButtonHover = ColorTranslator.FromHtml("#CBD5E1");
                AccentColor = ColorTranslator.FromHtml("#0070D2");
                AccentHover = ColorTranslator.FromHtml("#005FB2");
            }
            else
            {
                WindowBg = ColorTranslator.FromHtml("#323B47");
                CardBg = ColorTranslator.FromHtml("#282F3B");
                TitleBarBg = ColorTranslator.FromHtml("#222933");
                HeroBarBg = ColorTranslator.FromHtml("#263140");
                HeroBarText = Color.White;
                TagBg = ColorTranslator.FromHtml("#1B3552");
                TagText = Color.FromArgb(0, 229, 255);
                TextPrimary = ColorTranslator.FromHtml("#F5F5F5");
                TextSecondary = ColorTranslator.FromHtml("#A2B0C4");
                BorderLine = ColorTranslator.FromHtml("#434E60");
                ButtonBg = ColorTranslator.FromHtml("#384352");
                ButtonHover = ColorTranslator.FromHtml("#475568");
                AccentColor = ColorTranslator.FromHtml("#0084FF");
                AccentHover = ColorTranslator.FromHtml("#2997FF");
            }
        }

        private void DragWindow(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(this.Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void InitializeComponent()
        {
            this.Text = "CAD 云线批注 - 全局参数设置";
            this.ClientSize = new Size(560, 460);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = WindowBg;
            this.Font = new Font("Microsoft YaHei", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.ForeColor = TextPrimary;

            // 顶部标题栏
            Panel pnlTitle = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = TitleBarBg };
            pnlTitle.MouseDown += DragWindow;

            Label lblTitle = new Label { Text = "CAD 云线批注 - 全局设置", Font = new Font(this.Font.FontFamily, 10F, FontStyle.Bold), ForeColor = TextPrimary, AutoSize = true, Location = new Point(14, 9), BackColor = Color.Transparent };
            lblTitle.MouseDown += DragWindow;

            Label lblVersion = new Label { Text = "v0.1.0", Font = new Font(this.Font.FontFamily, 8.5F), ForeColor = Color.FromArgb(0, 180, 210), AutoSize = true, Location = new Point(lblTitle.PreferredWidth + 24, 11), BackColor = Color.Transparent };
            lblVersion.MouseDown += DragWindow;

            Button btnHelpTitle = new Button { Text = "?", Size = new Size(38, 38), Location = new Point(this.ClientSize.Width - 80, 0), Anchor = AnchorStyles.Top | AnchorStyles.Right, FlatStyle = FlatStyle.Flat, ForeColor = TextSecondary, BackColor = TitleBarBg, Cursor = Cursors.Hand };
            btnHelpTitle.Font = new Font(this.Font.FontFamily, 10F, FontStyle.Bold);
            btnHelpTitle.FlatAppearance.BorderSize = 0;
            btnHelpTitle.Click += (s, e) => HelpService.OpenReadme();
            btnHelpTitle.MouseEnter += (s, e) => { btnHelpTitle.BackColor = ButtonHover; btnHelpTitle.ForeColor = TextPrimary; };
            btnHelpTitle.MouseLeave += (s, e) => { btnHelpTitle.BackColor = TitleBarBg; btnHelpTitle.ForeColor = TextSecondary; };
            ToolTip ttHelp = new ToolTip();
            ttHelp.SetToolTip(btnHelpTitle, "帮助与使用手册 (F1 / Readme.html)");

            Button btnClose = new Button { Text = "✕", Size = new Size(42, 38), Location = new Point(this.ClientSize.Width - 42, 0), Anchor = AnchorStyles.Top | AnchorStyles.Right, FlatStyle = FlatStyle.Flat, ForeColor = TextSecondary, BackColor = TitleBarBg, Cursor = Cursors.Hand };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
            btnClose.MouseEnter += (s, e) => { btnClose.BackColor = Color.FromArgb(232, 17, 35); btnClose.ForeColor = Color.White; };
            btnClose.MouseLeave += (s, e) => { btnClose.BackColor = TitleBarBg; btnClose.ForeColor = TextSecondary; };

            pnlTitle.Controls.AddRange(new Control[] { lblTitle, lblVersion, btnHelpTitle, btnClose });

            // 底部操作与版权栏
            Panel pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 68,
                BackColor = TitleBarBg,
                Padding = new Padding(14, 8, 14, 6)
            };

            btnReset = new SleekButton
            {
                Text = "恢复默认",
                Location = new Point(14, 8),
                Size = new Size(74, 28),
                DefaultColor = Color.Transparent,
                HoverColor = ButtonHover,
                BorderColor = BorderLine,
                ForeColor = TextSecondary
            };
            btnReset.Click += BtnReset_Click;

            chkBottomSimpleMode = new CheckBox
            {
                Text = "急速批注",
                AutoSize = true,
                ForeColor = TextPrimary,
                Cursor = Cursors.Hand
            };
            chkBottomSimpleMode.CheckedChanged += (s, e) =>
            {
                if (cmbBottomRapidPhrase != null) cmbBottomRapidPhrase.Enabled = chkBottomSimpleMode.Checked;
                if (!_isUpdatingFromCode && chkSimpleMode != null && chkSimpleMode.Checked != chkBottomSimpleMode.Checked)
                {
                    _isUpdatingFromCode = true;
                    try { chkSimpleMode.Checked = chkBottomSimpleMode.Checked; }
                    finally { _isUpdatingFromCode = false; }
                }
            };

            cmbBottomRapidPhrase = new ComboBox
            {
                Width = 110,
                Height = 24,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = CardBg,
                ForeColor = TextPrimary
            };
            var corePresetPhrases = RapidPhraseService.GetCoreRapidPresetPhrases();
            foreach (var p in corePresetPhrases) cmbBottomRapidPhrase.Items.Add(p);
            if (cmbBottomRapidPhrase.Items.Count > 0) cmbBottomRapidPhrase.SelectedIndex = 0;
            cmbBottomRapidPhrase.SelectedIndexChanged += (s, e) =>
            {
                if (!_isUpdatingFromCode && cmbRapidDefaultPhrase != null && cmbBottomRapidPhrase.SelectedItem != null)
                {
                    _isUpdatingFromCode = true;
                    try { cmbRapidDefaultPhrase.Text = cmbBottomRapidPhrase.SelectedItem.ToString(); }
                    finally { _isUpdatingFromCode = false; }
                }
            };

            btnCancel = new SleekButton
            {
                Text = "取消",
                Location = new Point(250, 8),
                Size = new Size(70, 28),
                DefaultColor = ButtonBg,
                HoverColor = ButtonHover,
                ForeColor = TextPrimary
            };
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            btnOk = new SleekButton
            {
                Text = "保存并应用",
                Location = new Point(328, 8),
                Size = new Size(85, 28),
                DefaultColor = AccentColor,
                HoverColor = AccentHover,
                ForeColor = Color.White
            };
            btnOk.Click += BtnOk_Click;

            btnSaveAndCreate = new SleekButton
            {
                Text = "保存并新建",
                Visible = false
            };

            SleekButton btnHelp = new SleekButton
            {
                Text = "帮助",
                Size = new Size(60, 28),
                DefaultColor = ButtonBg,
                HoverColor = ButtonHover,
                BorderColor = BorderLine,
                ForeColor = TextPrimary
            };
            btnHelp.Click += (s, e) => HelpService.OpenReadme();

            // 底部开源与版权链接
            LinkLabel lnkHelp = new LinkLabel
            {
                Text = "📖 使用帮助",
                Font = new Font(this.Font.FontFamily, 8.5F),
                LinkColor = AccentColor,
                AutoSize = true,
                BackColor = Color.Transparent,
                LinkBehavior = LinkBehavior.HoverUnderline
            };
            lnkHelp.Click += (s, e) => HelpService.OpenReadme();

            Label lblSep = new Label
            {
                Text = "|",
                Font = new Font(this.Font.FontFamily, 8.5F),
                ForeColor = BorderLine,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            LinkLabel lnkSource = new LinkLabel
            {
                Text = "开源地址",
                Font = new Font(this.Font.FontFamily, 8.5F),
                LinkColor = AccentColor,
                AutoSize = true,
                Location = new Point(14, 42),
                BackColor = Color.Transparent,
                LinkBehavior = LinkBehavior.HoverUnderline
            };
            lnkSource.Click += (s, e) => {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/Andyang127/CadAutoCloudNote") { UseShellExecute = true }); } catch { }
            };

            Label lblDevBy = new Label
            {
                Text = "Developed by ",
                Font = new Font(this.Font.FontFamily, 8.5F),
                ForeColor = TextSecondary,
                AutoSize = true,
                Location = new Point(340, 42),
                BackColor = Color.Transparent
            };

            LinkLabel lnkAuthor = new LinkLabel
            {
                Text = "浅醉·墨语",
                Font = new Font(this.Font.FontFamily, 8.5F),
                LinkColor = AccentColor,
                AutoSize = true,
                Location = new Point(lblDevBy.Right - 4, 42),
                BackColor = Color.Transparent,
                LinkBehavior = LinkBehavior.HoverUnderline
            };
            lnkAuthor.Click += (s, e) => {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://v.douyin.com/EQGXP6k0l2g/") { UseShellExecute = true }); } catch { }
            };

            Action layoutBottom = () =>
            {
                int w = pnlBottom.ClientSize.Width;
                int rightMargin = 14;
                btnOk.Location = new Point(w - rightMargin - btnOk.Width, 8);
                btnCancel.Location = new Point(btnOk.Left - 8 - btnCancel.Width, 8);
                btnHelp.Location = new Point(btnCancel.Left - 8 - btnHelp.Width, 8);
                btnReset.Location = new Point(14, 8);
                chkBottomSimpleMode.Location = new Point(btnReset.Right + 10, 12);
                cmbBottomRapidPhrase.Location = new Point(chkBottomSimpleMode.Right + 6, 9);

                lnkHelp.Location = new Point(14, 42);
                lblSep.Location = new Point(lnkHelp.Right + 4, 42);
                lnkSource.Location = new Point(lblSep.Right + 4, 42);
                lnkAuthor.Location = new Point(w - rightMargin - lnkAuthor.PreferredWidth, 42);
                lblDevBy.Location = new Point(lnkAuthor.Left - lblDevBy.PreferredWidth, 42);
            };
            pnlBottom.Resize += (s, e) => layoutBottom();
            layoutBottom();

            pnlBottom.Controls.AddRange(new Control[] { btnReset, chkBottomSimpleMode, cmbBottomRapidPhrase, btnHelp, btnCancel, btnOk, lnkHelp, lblSep, lnkSource, lblDevBy, lnkAuthor });

            // 批注使用对象 - 顶置全宽核心驱动控制栏
            Panel pnlPersonaHero = new Panel
            {
                Dock = DockStyle.Top,
                Height = 34,
                BackColor = HeroBarBg,
                Padding = new Padding(10, 4, 10, 4)
            };

            Label lblPersonaTitle = new Label
            {
                Text = "使用对象:",
                ForeColor = HeroBarText,
                Font = new Font(this.Font.FontFamily, 9F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(10, 8),
                BackColor = Color.Transparent
            };

            cmbTargetPersonaHero = new ComboBox
            {
                Location = new Point(lblPersonaTitle.Right + 6, 5),
                Width = 240,
                Height = 24,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = CardBg,
                ForeColor = TextPrimary
            };

            var allTpls = ProjectTemplateService.GetAllTemplates();
            foreach (var t in allTpls) cmbTargetPersonaHero.Items.Add(t);
            cmbTargetPersonaHero.DisplayMember = "Name";
            cmbTargetPersonaHero.SelectedIndexChanged += CmbTargetPersonaHero_SelectedIndexChanged;

            btnApplyPersonaHero = new SleekButton
            {
                Text = "应用规则",
                Location = new Point(cmbTargetPersonaHero.Right + 8, 4),
                Size = new Size(80, 26),
                DefaultColor = AccentColor,
                HoverColor = AccentHover,
                ForeColor = Color.White
            };
            btnApplyPersonaHero.Click += BtnApplyPersonaHero_Click;

            pnlPersonaHero.Controls.AddRange(new Control[] { lblPersonaTitle, cmbTargetPersonaHero, btnApplyPersonaHero });

            // 中间 Tab 选项卡（5 大紧凑板块，严格对齐 WPF）
            TabControl tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Padding = new Point(12, 6)
            };

            TabPage tabAudience = new TabPage("对象与规则");
            TabPage tabDrawing = new TabPage("绘图与样式");
            TabPage tabRapid = new TabPage("极速与短语");
            TabPage tabKnowledge = new TabPage("规范知识库");
            TabPage tabSystem = new TabPage("快捷与系统");

            tabAudience.BackColor = WindowBg;
            tabDrawing.BackColor = WindowBg;
            tabRapid.BackColor = WindowBg;
            tabKnowledge.BackColor = WindowBg;
            tabSystem.BackColor = WindowBg;

            InitTabAudience(tabAudience);
            InitTabDrawing(tabDrawing);
            InitTabRapid(tabRapid);
            InitTabKnowledge(tabKnowledge);
            InitTabSystem(tabSystem);

            tabControl.TabPages.AddRange(new TabPage[] { tabAudience, tabDrawing, tabRapid, tabKnowledge, tabSystem });

            Panel pnlCenter = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 6, 10, 6), BackColor = WindowBg };
            pnlCenter.Controls.Add(tabControl);

            this.Controls.Add(pnlCenter);
            this.Controls.Add(pnlPersonaHero);
            this.Controls.Add(pnlTitle);
            this.Controls.Add(pnlBottom);
            this.CancelButton = btnCancel;
        }

        // ==================== 板块 1：绘图与样式 ====================
        private void InitTabDrawing(TabPage page)
        {
            Panel scrollPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = WindowBg, Padding = new Padding(6) };

            int yOffset = 6;

            // 1. 图层与外观
            Label lblGroup1 = new Label { Text = "图层与基础外观", Font = new Font(this.Font, FontStyle.Bold), ForeColor = AccentColor, AutoSize = true, Location = new Point(8, yOffset) };
            scrollPanel.Controls.Add(lblGroup1);
            yOffset += 22;

            TableLayoutPanel p1 = CreateTableLayout(5);
            p1.Location = new Point(8, yOffset);
            p1.Controls.Add(new Label { Text = "云线图层名称:", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = TextSecondary }, 0, 0);
            txtCloudLayer = new TextBox { Width = 280, BackColor = CardBg, ForeColor = TextPrimary };
            p1.Controls.Add(txtCloudLayer, 1, 0);

            p1.Controls.Add(new Label { Text = "文字图层名称:", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = TextSecondary }, 0, 1);
            txtTextLayer = new TextBox { Width = 280, BackColor = CardBg, ForeColor = TextPrimary };
            p1.Controls.Add(txtTextLayer, 1, 1);

            p1.Controls.Add(new Label { Text = "云线默认颜色:", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = TextSecondary }, 0, 2);
            cmbCloudColor = CreateColorCombo();
            p1.Controls.Add(cmbCloudColor, 1, 2);

            p1.Controls.Add(new Label { Text = "文字默认颜色:", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = TextSecondary }, 0, 3);
            cmbTextColor = CreateColorCombo();
            p1.Controls.Add(cmbTextColor, 1, 3);

            p1.Controls.Add(new Label { Text = "文字样式:", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = TextSecondary }, 0, 4);
            txtTextStyle = new TextBox { Width = 280, BackColor = CardBg, ForeColor = TextPrimary };
            p1.Controls.Add(txtTextStyle, 1, 4);

            scrollPanel.Controls.Add(p1);
            yOffset = p1.Bottom + 4;

            chkNonPlottable = new CheckBox { Text = "批注图层设置为不打印", AutoSize = true, Location = new Point(12, yOffset), ForeColor = TextPrimary };
            scrollPanel.Controls.Add(chkNonPlottable);
            yOffset += 26;

            // 2. 云线几何与线宽
            Label lblGroup2 = new Label { Text = "云线几何算法与笔锋", Font = new Font(this.Font, FontStyle.Bold), ForeColor = AccentColor, AutoSize = true, Location = new Point(8, yOffset) };
            scrollPanel.Controls.Add(lblGroup2);
            yOffset += 22;

            TableLayoutPanel p2 = CreateTableLayout(6);
            p2.Location = new Point(8, yOffset);
            p2.Controls.Add(new Label { Text = "默认绘制模式:", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = TextSecondary }, 0, 0);
            cmbCloudType = new ComboBox { Width = 280, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = CardBg, ForeColor = TextPrimary };
            cmbCloudType.Items.AddRange(new object[] { "矩形云线", "多边形云线", "徒手画云线" });
            p2.Controls.Add(cmbCloudType, 1, 0);

            p2.Controls.Add(new Label { Text = "云线样式:", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = TextSecondary }, 0, 1);
            cmbCloudStyle = new ComboBox { Width = 280, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = CardBg, ForeColor = TextPrimary };
            cmbCloudStyle.Items.AddRange(new object[] { "普通等宽", "书法笔锋" });
            p2.Controls.Add(cmbCloudStyle, 1, 1);

            p2.Controls.Add(new Label { Text = "云线线宽 (mm):", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = TextSecondary }, 0, 2);
            txtCloudWidth = new TextBox { Width = 280, BackColor = CardBg, ForeColor = TextPrimary };
            p2.Controls.Add(txtCloudWidth, 1, 2);

            p2.Controls.Add(new Label { Text = "弧长比例系数:", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = TextSecondary }, 0, 3);
            txtArcLength = new TextBox { Width = 280, BackColor = CardBg, ForeColor = TextPrimary };
            p2.Controls.Add(txtArcLength, 1, 3);

            p2.Controls.Add(new Label { Text = "圆弧凸度:", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = TextSecondary }, 0, 4);
            txtBulge = new TextBox { Width = 280, BackColor = CardBg, ForeColor = TextPrimary };
            p2.Controls.Add(txtBulge, 1, 4);

            p2.Controls.Add(new Label { Text = "最小弧长保护:", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = TextSecondary }, 0, 5);
            txtMinArc = new TextBox { Width = 280, BackColor = CardBg, ForeColor = TextPrimary };
            p2.Controls.Add(txtMinArc, 1, 5);

            scrollPanel.Controls.Add(p2);
            yOffset = p2.Bottom + 10;

            // 3. 文字与引线
            Label lblGroup3 = new Label { Text = "文字排版与引线控制", Font = new Font(this.Font, FontStyle.Bold), ForeColor = AccentColor, AutoSize = true, Location = new Point(8, yOffset) };
            scrollPanel.Controls.Add(lblGroup3);
            yOffset += 22;

            TableLayoutPanel p3 = CreateTableLayout(4);
            p3.Location = new Point(8, yOffset);
            p3.Controls.Add(new Label { Text = "字高比例系数:", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = TextSecondary }, 0, 0);
            txtTextHeight = new TextBox { Width = 280, BackColor = CardBg, ForeColor = TextPrimary };
            p3.Controls.Add(txtTextHeight, 1, 0);

            p3.Controls.Add(new Label { Text = "引线绘制模式:", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = TextSecondary }, 0, 1);
            cmbLeaderType = new ComboBox { Width = 280, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = CardBg, ForeColor = TextPrimary };
            cmbLeaderType.Items.AddRange(new object[] { "带箭头引线", "点引线", "无引线独立放置" });
            p3.Controls.Add(cmbLeaderType, 1, 1);

            p3.Controls.Add(new Label { Text = "箭头大小系数:", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = TextSecondary }, 0, 2);
            txtArrowSize = new TextBox { Width = 280, BackColor = CardBg, ForeColor = TextPrimary };
            p3.Controls.Add(txtArrowSize, 1, 2);

            p3.Controls.Add(new Label { Text = "批注编号前缀:", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = TextSecondary }, 0, 3);
            txtPrefix = new TextBox { Width = 280, BackColor = CardBg, ForeColor = TextPrimary };
            p3.Controls.Add(txtPrefix, 1, 3);

            scrollPanel.Controls.Add(p3);
            yOffset = p3.Bottom + 10;

            // 4. 【高级联动】严重等级与颜色智能映射
            Label lblGroup4 = new Label { Text = "【高级联动】严重等级与颜色智能联动", Font = new Font(this.Font, FontStyle.Bold), ForeColor = AccentColor, AutoSize = true, Location = new Point(8, yOffset) };
            scrollPanel.Controls.Add(lblGroup4);
            yOffset += 22;

            chkPriorityColorLink = new CheckBox { Text = "启用优先级与云线颜色智能联动", AutoSize = true, Location = new Point(12, yOffset), ForeColor = TextPrimary, Checked = true };
            scrollPanel.Controls.Add(chkPriorityColorLink);
            yOffset += 26;

            TableLayoutPanel p4 = CreateTableLayout(2);
            p4.Location = new Point(8, yOffset);

            p4.Controls.Add(new Label { Text = "重要优先级颜色:", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = TextSecondary }, 0, 0);
            cmbColorImportant = CreateColorCombo();
            p4.Controls.Add(cmbColorImportant, 1, 0);

            p4.Controls.Add(new Label { Text = "紧急优先级颜色:", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = TextSecondary }, 0, 1);
            cmbColorUrgent = CreateColorCombo();
            p4.Controls.Add(cmbColorUrgent, 1, 1);

            scrollPanel.Controls.Add(p4);
            yOffset = p4.Bottom + 16;

            // 颜色与严重等级双向智能联动事件
            cmbCloudColor.SelectedIndexChanged += (s, e) => {
                if (_isUpdatingFromCode) return;
                if (chkPriorityColorLink != null && chkPriorityColorLink.Checked)
                {
                    _isUpdatingFromCode = true;
                    try
                    {
                        if (cmbPriority != null && cmbPriority.SelectedItem?.ToString() == "重要" && cmbColorImportant != null)
                        {
                            cmbColorImportant.SelectedIndex = cmbCloudColor.SelectedIndex;
                        }
                        else if (cmbPriority != null && cmbPriority.SelectedItem?.ToString() == "紧急" && cmbColorUrgent != null)
                        {
                            cmbColorUrgent.SelectedIndex = cmbCloudColor.SelectedIndex;
                        }
                    }
                    finally { _isUpdatingFromCode = false; }
                }
            };

            cmbColorImportant.SelectedIndexChanged += (s, e) => {
                if (_isUpdatingFromCode) return;
                if (chkPriorityColorLink != null && chkPriorityColorLink.Checked && cmbPriority != null && cmbPriority.SelectedItem?.ToString() == "重要" && cmbCloudColor != null)
                {
                    _isUpdatingFromCode = true;
                    try { cmbCloudColor.SelectedIndex = cmbColorImportant.SelectedIndex; }
                    finally { _isUpdatingFromCode = false; }
                }
            };

            cmbColorUrgent.SelectedIndexChanged += (s, e) => {
                if (_isUpdatingFromCode) return;
                if (chkPriorityColorLink != null && chkPriorityColorLink.Checked && cmbPriority != null && cmbPriority.SelectedItem?.ToString() == "紧急" && cmbCloudColor != null)
                {
                    _isUpdatingFromCode = true;
                    try { cmbCloudColor.SelectedIndex = cmbColorUrgent.SelectedIndex; }
                    finally { _isUpdatingFromCode = false; }
                }
            };

            cmbTextColor.SelectedIndexChanged += (s, e) => {
                if (_isUpdatingFromCode) return;
                // 文字颜色强制锁定 7 号黑白 (索引 6)
                if (cmbTextColor.SelectedIndex != 6)
                {
                    _isUpdatingFromCode = true;
                    try { cmbTextColor.SelectedIndex = 6; }
                    finally { _isUpdatingFromCode = false; }
                }
            };

            page.Controls.Add(scrollPanel);
        }

        // ==================== 板块 0：对象与规则 ====================
        private void InitTabAudience(TabPage page)
        {
            Panel scrollPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = WindowBg, Padding = new Padding(6) };
            int yOffset = 6;

            // 0.1 角色定位与职责说明卡片
            Label lblGroup1 = new Label { Text = "角色身份与专属职责规则", Font = new Font(this.Font, FontStyle.Bold), ForeColor = AccentColor, AutoSize = true, Location = new Point(8, yOffset) };
            scrollPanel.Controls.Add(lblGroup1);
            yOffset += 22;

            Panel pnlRoleCard = new Panel
            {
                Location = new Point(8, yOffset),
                Size = new Size(505, 92),
                BackColor = CardBg,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(8)
            };

            lblAudienceRoleTitle = new Label
            {
                Text = "角色身份: 通用工程",
                Font = new Font(this.Font, FontStyle.Bold),
                ForeColor = TagText,
                AutoSize = true,
                Location = new Point(8, 8)
            };

            lblAudienceDesc = new Label
            {
                Text = "适用于各类常规专业图纸的工程协同与问题批注",
                ForeColor = TextSecondary,
                Location = new Point(8, 28),
                Size = new Size(485, 34)
            };

            lblAudienceLayer = new Label { Text = "专属图层: CAD_NOTE_CLOUD", AutoSize = true, Location = new Point(8, 66), ForeColor = TextSecondary, Font = new Font(this.Font.FontFamily, 8.5F) };
            lblAudiencePrefix = new Label { Text = "编号前缀: #", AutoSize = true, Location = new Point(190, 66), ForeColor = TextSecondary, Font = new Font(this.Font.FontFamily, 8.5F) };
            lblAudiencePlot = new Label { Text = "打印控制: 可打印", AutoSize = true, Location = new Point(340, 66), ForeColor = TextSecondary, Font = new Font(this.Font.FontFamily, 8.5F) };

            pnlRoleCard.Controls.AddRange(new Control[] { lblAudienceRoleTitle, lblAudienceDesc, lblAudienceLayer, lblAudiencePrefix, lblAudiencePlot });
            scrollPanel.Controls.Add(pnlRoleCard);
            yOffset = pnlRoleCard.Bottom + 10;

            // 0.2 适用专业白名单指示卡片
            Label lblGroup2 = new Label { Text = "适用专业 (当前对象不相关专业自动置灰)", Font = new Font(this.Font, FontStyle.Bold), ForeColor = AccentColor, AutoSize = true, Location = new Point(8, yOffset) };
            scrollPanel.Controls.Add(lblGroup2);
            yOffset += 22;

            pnlAudienceDisciplines = new FlowLayoutPanel
            {
                Location = new Point(8, yOffset),
                Size = new Size(505, 34),
                BackColor = CardBg,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(4)
            };
            string[] allDisciplines = new[] { "建筑", "结构", "给排水", "暖通", "电气", "总图", "通用" };
            foreach (var disc in allDisciplines)
            {
                Label lblDiscChip = new Label
                {
                    Text = disc,
                    Name = "chip_" + disc,
                    AutoSize = true,
                    Padding = new Padding(6, 3, 6, 3),
                    Margin = new Padding(2, 2, 4, 2),
                    BackColor = TagBg,
                    ForeColor = TagText,
                    Font = new Font(this.Font.FontFamily, 8.5F, FontStyle.Bold)
                };
                pnlAudienceDisciplines.Controls.Add(lblDiscChip);
            }
            scrollPanel.Controls.Add(pnlAudienceDisciplines);
            yOffset = pnlAudienceDisciplines.Bottom + 10;

            // 0.3 专业协同与工作流分配
            Label lblGroup3 = new Label { Text = "专业协同与工作流默认分配", Font = new Font(this.Font, FontStyle.Bold), ForeColor = AccentColor, AutoSize = true, Location = new Point(8, yOffset) };
            scrollPanel.Controls.Add(lblGroup3);
            yOffset += 22;

            TableLayoutPanel pWorkflow = CreateTableLayout(3);
            pWorkflow.Location = new Point(8, yOffset);
            pWorkflow.Controls.Add(new Label { Text = "默认专业类别:", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = TextSecondary }, 0, 0);
            cmbDiscipline = new ComboBox { Width = 280, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = CardBg, ForeColor = TextPrimary };
            cmbDiscipline.Items.AddRange(new object[] { "建筑", "结构", "给排水", "暖通", "电气", "总图", "通用" });
            pWorkflow.Controls.Add(cmbDiscipline, 1, 0);

            pWorkflow.Controls.Add(new Label { Text = "默认批注者:", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = TextSecondary }, 0, 1);
            txtAssignee = new TextBox { Width = 280, BackColor = CardBg, ForeColor = TextPrimary };
            pWorkflow.Controls.Add(txtAssignee, 1, 1);

            pWorkflow.Controls.Add(new Label { Text = "默认优先级:", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = TextSecondary }, 0, 2);
            cmbPriority = new ComboBox { Width = 280, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = CardBg, ForeColor = TextPrimary };
            cmbPriority.Items.AddRange(new object[] { "重要", "紧急" });
            cmbPriority.SelectedIndexChanged += (s, e) => {
                if (_isUpdatingFromCode) return;
                if (chkPriorityColorLink != null && chkPriorityColorLink.Checked && cmbCloudColor != null)
                {
                    _isUpdatingFromCode = true;
                    try
                    {
                        if (cmbPriority.SelectedItem?.ToString() == "重要" && cmbColorImportant != null)
                        {
                            cmbCloudColor.SelectedIndex = cmbColorImportant.SelectedIndex;
                        }
                        else if (cmbPriority.SelectedItem?.ToString() == "紧急" && cmbColorUrgent != null)
                        {
                            cmbCloudColor.SelectedIndex = cmbColorUrgent.SelectedIndex;
                        }
                    }
                    finally { _isUpdatingFromCode = false; }
                }
            };
            pWorkflow.Controls.Add(cmbPriority, 1, 2);

            scrollPanel.Controls.Add(pWorkflow);
            yOffset = pWorkflow.Bottom + 10;

            // 0.4 台账导出按钮
            btnExportTemplateExcel = new SleekButton
            {
                Text = "导出当前对象批注台账 Excel",
                Location = new Point(8, yOffset),
                Size = new Size(200, 28),
                DefaultColor = ButtonBg,
                HoverColor = ButtonHover,
                ForeColor = TextPrimary
            };
            btnExportTemplateExcel.Click += (s, e) => {
                try
                {
                    var doc = AcApp.DocumentManager.MdiActiveDocument;
                    if (doc != null)
                    {
                        this.Close();
                        doc.SendStringToExecute("CNEXPORT\n", true, false, false);
                    }
                }
                catch (Exception ex)
                {
                    CadWinFormMessageBox.ShowWarning("导出台账失败: " + ex.Message, "提示", this);
                }
            };
            scrollPanel.Controls.Add(btnExportTemplateExcel);
            yOffset = btnExportTemplateExcel.Bottom + 16;

            page.Controls.Add(scrollPanel);
        }

        // ==================== 板块 2：极速与短语 ====================
        private void InitTabRapid(TabPage page)
        {
            Panel scrollPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = WindowBg, Padding = new Padding(6) };
            int yOffset = 6;

            // 1. 急速批注成图策略
            Label lblGroup1 = new Label { Text = "急速批注成图策略", Font = new Font(this.Font, FontStyle.Bold), ForeColor = AccentColor, AutoSize = true, Location = new Point(8, yOffset) };
            scrollPanel.Controls.Add(lblGroup1);
            yOffset += 22;

            chkSimpleMode = new CheckBox { Text = "启用急速批注模式 (免弹窗快速出图)", AutoSize = true, Location = new Point(12, yOffset), ForeColor = TextPrimary, Checked = true };
            chkSimpleMode.CheckedChanged += (s, e) => {
                if (!_isUpdatingFromCode && chkBottomSimpleMode != null && chkBottomSimpleMode.Checked != chkSimpleMode.Checked)
                {
                    _isUpdatingFromCode = true;
                    try { chkBottomSimpleMode.Checked = chkSimpleMode.Checked; }
                    finally { _isUpdatingFromCode = false; }
                }
            };
            scrollPanel.Controls.Add(chkSimpleMode);
            yOffset += 26;

            TableLayoutPanel pRapidTbl = CreateTableLayout(1);
            pRapidTbl.Location = new Point(8, yOffset);
            pRapidTbl.Controls.Add(new Label { Text = "极速默认短语:", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = TextSecondary }, 0, 0);

            cmbRapidDefaultPhrase = new ComboBox
            {
                Width = 280,
                DropDownStyle = ComboBoxStyle.DropDown,
                BackColor = CardBg,
                ForeColor = TextPrimary
            };
            var presetPhrases = RapidPhraseService.GetCoreRapidPresetPhrases();
            foreach (var phrase in presetPhrases) cmbRapidDefaultPhrase.Items.Add(phrase);
            if (cmbRapidDefaultPhrase.Items.Count > 0) cmbRapidDefaultPhrase.SelectedIndex = 0;
            cmbRapidDefaultPhrase.TextChanged += (s, e) => {
                if (!_isUpdatingFromCode && cmbBottomRapidPhrase != null && !string.IsNullOrEmpty(cmbRapidDefaultPhrase.Text))
                {
                    if (cmbBottomRapidPhrase.Items.Contains(cmbRapidDefaultPhrase.Text))
                    {
                        _isUpdatingFromCode = true;
                        try { cmbBottomRapidPhrase.SelectedItem = cmbRapidDefaultPhrase.Text; }
                        finally { _isUpdatingFromCode = false; }
                    }
                }
            };
            pRapidTbl.Controls.Add(cmbRapidDefaultPhrase, 1, 0);

            scrollPanel.Controls.Add(pRapidTbl);
            yOffset = pRapidTbl.Bottom + 4;

            chkEnableRapidCmdKey = new CheckBox { Text = "引线放置时支持快捷切词 (按 S 选短语 / 按 T 手动输入)", AutoSize = true, Location = new Point(12, yOffset), ForeColor = TextPrimary, Checked = true };
            scrollPanel.Controls.Add(chkEnableRapidCmdKey);
            yOffset += 22;

            Label lblRapidTip = new Label { Text = "说明：开启急速批注后，绘制云线并定位引线即可直接生成批注。", AutoSize = true, Location = new Point(12, yOffset), ForeColor = TextSecondary, Font = new Font(this.Font.FontFamily, 8.5F) };
            scrollPanel.Controls.Add(lblRapidTip);
            yOffset += 26;

            // 2. 常用审查意见快捷词典
            Label lblGroup2 = new Label { Text = "常用审查意见快捷词典", Font = new Font(this.Font, FontStyle.Bold), ForeColor = AccentColor, AutoSize = true, Location = new Point(8, yOffset) };
            scrollPanel.Controls.Add(lblGroup2);
            yOffset += 20;

            lstSnippets = new ListBox
            {
                Location = new Point(10, yOffset),
                Size = new Size(505, 95),
                BackColor = CardBg,
                ForeColor = TextPrimary,
                BorderStyle = BorderStyle.FixedSingle
            };
            scrollPanel.Controls.Add(lstSnippets);
            yOffset = lstSnippets.Bottom + 6;

            txtNewSnippet = new TextBox
            {
                Location = new Point(10, yOffset),
                Size = new Size(275, 26),
                BackColor = CardBg,
                ForeColor = TextPrimary
            };
            btnAddSnippet = new SleekButton
            {
                Text = "添加",
                Location = new Point(292, yOffset),
                Size = new Size(65, 24),
                DefaultColor = ButtonBg,
                HoverColor = ButtonHover,
                ForeColor = TextPrimary
            };
            btnAddSnippet.Click += (s, e) => {
                string txt = txtNewSnippet.Text.Trim();
                if (!string.IsNullOrEmpty(txt) && !lstSnippets.Items.Contains(txt))
                {
                    lstSnippets.Items.Add(txt);
                    txtNewSnippet.Text = string.Empty;
                }
            };

            btnDeleteSnippet = new SleekButton
            {
                Text = "删除",
                Location = new Point(362, yOffset),
                Size = new Size(65, 24),
                DefaultColor = ButtonBg,
                HoverColor = ButtonHover,
                ForeColor = TextPrimary
            };
            btnDeleteSnippet.Click += (s, e) => {
                if (lstSnippets.SelectedIndex >= 0)
                {
                    lstSnippets.Items.RemoveAt(lstSnippets.SelectedIndex);
                }
            };

            btnResetSnippets = new SleekButton
            {
                Text = "恢复预设",
                Location = new Point(432, yOffset),
                Size = new Size(80, 24),
                DefaultColor = ButtonBg,
                HoverColor = ButtonHover,
                ForeColor = TextPrimary
            };
            btnResetSnippets.Click += (s, e) => {
                lstSnippets.Items.Clear();
                var core = RapidPhraseService.GetCoreRapidPresetPhrases();
                foreach (var phrase in core) lstSnippets.Items.Add(phrase);
            };

            scrollPanel.Controls.AddRange(new Control[] { txtNewSnippet, btnAddSnippet, btnDeleteSnippet, btnResetSnippets });
            yOffset += 32;

            // 3. 存储分卷与健康体检
            Label lblGroup3 = new Label { Text = "存储分卷、安全审计与图纸轻量化", Font = new Font(this.Font, FontStyle.Bold), ForeColor = AccentColor, AutoSize = true, Location = new Point(8, yOffset) };
            scrollPanel.Controls.Add(lblGroup3);
            yOffset += 22;

            TableLayoutPanel p3 = CreateTableLayout(1);
            p3.Location = new Point(8, yOffset);
            p3.Controls.Add(new Label { Text = "单卷批注上限 (条):", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = TextSecondary }, 0, 0);
            txtMaxNotesVolume = new TextBox { Width = 280, BackColor = CardBg, ForeColor = TextPrimary };
            p3.Controls.Add(txtMaxNotesVolume, 1, 0);

            scrollPanel.Controls.Add(p3);
            yOffset = p3.Bottom + 4;

            chkAuditTrail = new CheckBox { Text = "启用 SHA256 哈希链记录与防篡改审计日志", AutoSize = true, Location = new Point(12, yOffset), ForeColor = TextPrimary };
            scrollPanel.Controls.Add(chkAuditTrail);
            yOffset += 24;

            chkAutoBackup = new CheckBox { Text = "保存 DWG 图纸时同步自动增量备份批注元数据", AutoSize = true, Location = new Point(12, yOffset), ForeColor = TextPrimary };
            scrollPanel.Controls.Add(chkAutoBackup);
            yOffset += 28;

            btnCompactDb = new SleekButton
            {
                Text = "一键图纸健康体检与轻量化清理",
                Location = new Point(10, yOffset),
                Size = new Size(240, 28),
                DefaultColor = AccentColor,
                HoverColor = AccentHover,
                ForeColor = Color.White
            };
            btnCompactDb.Click += (s, e) => {
                try
                {
                    var doc = AcApp.DocumentManager.MdiActiveDocument;
                    if (doc == null || doc.Database == null)
                    {
                        CadWinFormMessageBox.ShowWarning("未检测到活动的 AutoCAD 绘图文档。", "提示", this);
                        return;
                    }
                    using (doc.LockDocument())
                    {
                        int cleaned = NoteService.CompactDrawingDatabase(doc.Database);
                        CadWinFormMessageBox.ShowInfo(string.Format("图纸批注健康体检完成！\n已扫描并清理无效/孤儿数据 {0} 项，图纸处于健康运行状态。", cleaned), "健康体检报告", this);
                    }
                }
                catch (Exception ex)
                {
                    CadWinFormMessageBox.ShowError("执行健康体检失败: " + ex.Message, "错误", this);
                }
            };
            scrollPanel.Controls.Add(btnCompactDb);
            yOffset = btnCompactDb.Bottom + 16;

            page.Controls.Add(scrollPanel);
        }

        // ==================== 板块 3：规范知识库 ====================
        private void InitTabKnowledge(TabPage page)
        {
            Panel scrollPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = WindowBg, Padding = new Padding(8) };

            chkEnableAi = new CheckBox { Text = "启用强条与规范知识库条目检索联想", AutoSize = true, Location = new Point(12, 12), ForeColor = TextPrimary };

            TableLayoutPanel p = CreateTableLayout(1);
            p.Location = new Point(10, 40);
            p.Controls.Add(new Label { Text = "语义匹配阈值:", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = TextSecondary }, 0, 0);
            txtAiThreshold = new TextBox { Width = 280, BackColor = CardBg, ForeColor = TextPrimary };
            p.Controls.Add(txtAiThreshold, 1, 0);

            chkAutoFillCode = new CheckBox { Text = "自动将规范条文编号 (如《建筑防火通用规范》) 附加到批注说明", AutoSize = true, Location = new Point(12, p.Bottom + 8), ForeColor = TextPrimary };

            Label lblNote = new Label
            {
                Text = "说明：强条条目库已在【管理看板 (CNP)】中提供统一检索与添加维护，设置完成后在看板或新建批注时可直接联想匹配。",
                Location = new Point(12, chkAutoFillCode.Bottom + 16),
                Size = new Size(500, 40),
                ForeColor = TextSecondary
            };

            scrollPanel.Controls.Add(chkEnableAi);
            scrollPanel.Controls.Add(p);
            scrollPanel.Controls.Add(chkAutoFillCode);
            scrollPanel.Controls.Add(lblNote);

            page.Controls.Add(scrollPanel);
        }

        // ==================== 板块 4：快捷与系统 ====================
        private void InitTabSystem(TabPage page)
        {
            Panel scrollPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = WindowBg, Padding = new Padding(8) };
            int yOffset = 6;

            Label lblGroup1 = new Label { Text = "系统交互与视口反馈偏好", Font = new Font(this.Font, FontStyle.Bold), ForeColor = AccentColor, AutoSize = true, Location = new Point(8, yOffset) };
            scrollPanel.Controls.Add(lblGroup1);
            yOffset += 22;

            chkAutoZoom = new CheckBox { Text = "双击看板列表项时自动缩放居中对焦到对应云线视口", AutoSize = true, Location = new Point(12, yOffset), ForeColor = TextPrimary };
            scrollPanel.Controls.Add(chkAutoZoom);
            yOffset += 24;

            chkSound = new CheckBox { Text = "批注流转状态变动或完成时播放系统声音提示", AutoSize = true, Location = new Point(12, yOffset), ForeColor = TextPrimary };
            scrollPanel.Controls.Add(chkSound);
            yOffset += 30;

            Label lblGroup2 = new Label { Text = "核心快捷指令一览与一键调用", Font = new Font(this.Font, FontStyle.Bold), ForeColor = AccentColor, AutoSize = true, Location = new Point(8, yOffset) };
            scrollPanel.Controls.Add(lblGroup2);
            yOffset += 22;

            pnlCommands = new Panel
            {
                Location = new Point(8, yOffset),
                Size = new Size(515, 230),
                BackColor = CardBg,
                AutoScroll = true,
                Padding = new Padding(6)
            };

            var commands = new[]
            {
                new { Alias = "CN", Full = "CNOTE", Desc = "新建批注：在图面框选范围生成闭合云线、引线与标注", ExecTag = "CNOTE" },
                new { Alias = "CNP", Full = "CNPANEL", Desc = "管理看板：呼出右侧看板，集中管理流转、回复与导出", ExecTag = "CNP" },
                new { Alias = "CNS", Full = "CNSETTINGS", Desc = "参数设置：打开当前全局配置窗口", ExecTag = "CNS" },
                new { Alias = "CNQ", Full = "CNQUICK", Desc = "极速批注：免弹窗命令行快速框选生成批注", ExecTag = "CNQ" },
                new { Alias = "CNAC", Full = "CNADDCLOUD", Desc = "追加云线：向已有批注追加新云线框与分枝引线", ExecTag = "CNADDCLOUD" },
                new { Alias = "CNCV", Full = "CNCONVERT", Desc = "对象转云线：选择多段线/圆/椭圆转换为云线批注", ExecTag = "CNCONVERT" },
                new { Alias = "CNR", Full = "CNREPLY", Desc = "快速回复：点选图面已有批注追加意见并流转状态", ExecTag = "CNR" },
                new { Alias = "CND", Full = "CNDEL", Desc = "快速删除：点选图元删除对应批注及绑定对象", ExecTag = "CND" },
                new { Alias = "CNH", Full = "CNHELP", Desc = "帮助手册：打开本地 Readme.html 完整离线手册与全功能说明", ExecTag = "CNHELP" }
            };

            int top = 6;
            foreach (var item in commands)
            {
                Panel row = new Panel { Location = new Point(4, top), Size = new Size(480, 26), BackColor = CardBg };

                Label lblAlias = new Label
                {
                    Text = item.Alias,
                    Font = new Font(this.Font.FontFamily, 9F, FontStyle.Bold),
                    BackColor = TagBg,
                    ForeColor = TagText,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Size = new Size(48, 22),
                    Location = new Point(2, 2)
                };

                Label lblFull = new Label
                {
                    Text = item.Full,
                    Font = new Font(this.Font.FontFamily, 8.5F, FontStyle.Bold),
                    BackColor = TagBg,
                    ForeColor = TagText,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Size = new Size(96, 22),
                    Location = new Point(54, 2)
                };

                Label lblDesc = new Label
                {
                    Text = item.Desc,
                    Font = new Font(this.Font.FontFamily, 8.5F),
                    ForeColor = TextSecondary,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Size = new Size(265, 22),
                    Location = new Point(154, 2),
                    AutoEllipsis = true
                };

                SleekButton btnExec = new SleekButton
                {
                    Text = "执行",
                    Size = new Size(46, 22),
                    Location = new Point(426, 2),
                    DefaultColor = ButtonBg,
                    HoverColor = ButtonHover,
                    ForeColor = TextPrimary,
                    Tag = item.ExecTag
                };
                btnExec.Click += (s, e) => {
                    if (s is SleekButton sb && sb.Tag is string cmd)
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
                                this.Close();
                                doc.SendStringToExecute(cmd + "\n", true, false, false);
                            }
                        }
                        catch (Exception ex)
                        {
                            CadWinFormMessageBox.ShowWarning("执行命令失败: " + ex.Message, "提示", this);
                        }
                    }
                };

                row.Controls.AddRange(new Control[] { lblAlias, lblFull, lblDesc, btnExec });
                btnExec.BringToFront();
                pnlCommands.Controls.Add(row);
                top += 28;
            }

            scrollPanel.Controls.Add(pnlCommands);
            page.Controls.Add(scrollPanel);
        }

        private void UpdateAudienceDisplay(AnnotationTemplate tpl)
        {
            if (tpl == null) return;
            if (lblAudienceRoleTitle != null) lblAudienceRoleTitle.Text = "角色身份: " + (tpl.RoleTitle ?? tpl.Name);
            if (lblAudienceDesc != null) lblAudienceDesc.Text = tpl.Description ?? "";
            if (lblAudienceLayer != null) lblAudienceLayer.Text = "专属图层: " + tpl.LayerName;
            if (lblAudiencePrefix != null) lblAudiencePrefix.Text = "编号前缀: " + tpl.Prefix;
            if (lblAudiencePlot != null) lblAudiencePlot.Text = "打印控制: " + (tpl.ForceNonPlotting ? (tpl.IsNonPlottingLocked ? "强制关闭打印 (已锁定)" : "关闭打印") : "开启打印");

            if (pnlAudienceDisciplines != null)
            {
                foreach (Control ctrl in pnlAudienceDisciplines.Controls)
                {
                    if (ctrl is Label lbl && lbl.Name.StartsWith("chip_"))
                    {
                        string disc = lbl.Text;
                        bool applicable = tpl.IsDisciplineApplicable(disc);
                        if (applicable)
                        {
                            lbl.BackColor = TagBg;
                            lbl.ForeColor = TagText;
                        }
                        else
                        {
                            lbl.BackColor = CardBg;
                            lbl.ForeColor = TextSecondary;
                        }
                    }
                }
            }
        }

        private TableLayoutPanel CreateTableLayout(int rowCount)
        {
            TableLayoutPanel p = new TableLayoutPanel
            {
                Location = new Point(10, 10),
                Size = new Size(505, rowCount * 33 + 4),
                ColumnCount = 2,
                RowCount = rowCount
            };
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (int i = 0; i < rowCount; i++)
            {
                p.RowStyles.Add(new RowStyle(SizeType.Absolute, 33F));
            }
            return p;
        }

        private ComboBox CreateColorCombo()
        {
            ComboBox cmb = new ComboBox
            {
                Width = 280,
                DropDownStyle = ComboBoxStyle.DropDownList,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 22,
                BackColor = CardBg,
                ForeColor = TextPrimary
            };
            cmb.Items.AddRange(new object[] {
                "红色", "黄色", "绿色", "青色", "蓝色", "洋红", "黑白", "随层"
            });
            cmb.DrawItem += CmbColor_DrawItem;
            cmb.SelectedIndex = 0;
            return cmb;
        }

        private void CmbColor_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            e.DrawBackground();

            Color swatchColor = Color.Red;
            switch (e.Index)
            {
                case 0: swatchColor = Color.FromArgb(255, 68, 68); break;
                case 1: swatchColor = Color.FromArgb(255, 215, 0); break;
                case 2: swatchColor = Color.FromArgb(0, 230, 118); break;
                case 3: swatchColor = Color.FromArgb(0, 229, 255); break;
                case 4: swatchColor = Color.FromArgb(41, 121, 255); break;
                case 5: swatchColor = Color.FromArgb(255, 64, 129); break;
                case 6: swatchColor = Color.FromArgb(240, 240, 240); break;
                case 7: swatchColor = Color.FromArgb(170, 170, 170); break;
            }

            Rectangle rectSwatch = new Rectangle(e.Bounds.X + 4, e.Bounds.Y + 4, 14, 14);
            using (SolidBrush sb = new SolidBrush(swatchColor))
            {
                e.Graphics.FillRectangle(sb, rectSwatch);
            }
            using (Pen p = new Pen(BorderLine, 1))
            {
                e.Graphics.DrawRectangle(p, rectSwatch);
            }

            string text = ((ComboBox)sender).Items[e.Index].ToString();
            Rectangle rectText = new Rectangle(e.Bounds.X + 24, e.Bounds.Y, e.Bounds.Width - 24, e.Bounds.Height);
            using (SolidBrush tb = new SolidBrush(TextPrimary))
            {
                StringFormat sf = new StringFormat { LineAlignment = StringAlignment.Center };
                e.Graphics.DrawString(text, e.Font, tb, rectText, sf);
            }

            e.DrawFocusRectangle();
        }

        private void LoadCurrentSettings()
        {
            _isUpdatingFromCode = true;
            try
            {
                var s = ConfigManager.Instance.CurrentSettings;

                // 1. 图层
                txtCloudLayer.Text = s.CloudLayer;
                txtTextLayer.Text = s.TextLayer;
                SetColorCombo(cmbCloudColor, s.CloudColorIndex);
                SetColorCombo(cmbTextColor, s.TextColorIndex);
                txtTextStyle.Text = s.TextStyleName;
                chkNonPlottable.Checked = s.ForceNonPlotting;

                // 2. 几何
                txtArcLength.Text = s.ArcLengthRatio.ToString("0.0");
                txtBulge.Text = s.BulgeCurvature.ToString("0.00");
                if (cmbCloudStyle != null) cmbCloudStyle.SelectedIndex = Math.Max(0, Math.Min(1, s.CloudStyle));
                txtCloudWidth.Text = s.CloudWidth.ToString("0.00");
                cmbCloudType.SelectedIndex = Math.Max(0, Math.Min(2, s.DefaultCloudType));
                txtMinArc.Text = s.MinArcLength.ToString("0.0");

                // 3. 文字引线
                txtTextHeight.Text = s.TextHeightRatio.ToString("0.0");
                txtArrowSize.Text = s.ArrowSizeRatio.ToString("0.0");
                cmbLeaderType.SelectedIndex = Math.Max(0, Math.Min(2, s.LeaderType));
                txtPrefix.Text = s.AutoNumberPrefix;

                // 严重等级联动
                chkPriorityColorLink.Checked = s.EnablePriorityColorLink;
                SetColorCombo(cmbColorImportant, s.PriorityColorImportant);
                SetColorCombo(cmbColorUrgent, s.PriorityColorUrgent);

                // 4. 业务
                cmbDiscipline.SelectedItem = string.IsNullOrEmpty(s.DefaultDiscipline) ? "建筑" : s.DefaultDiscipline;
                if (cmbDiscipline.SelectedIndex < 0) cmbDiscipline.SelectedIndex = 0;
                txtAssignee.Text = s.DefaultAssignee;
                cmbPriority.SelectedItem = string.IsNullOrEmpty(s.DefaultPriority) ? "重要" : s.DefaultPriority;
                if (cmbPriority.SelectedIndex < 0) cmbPriority.SelectedIndex = 0;
                chkAutoZoom.Checked = s.AutoZoomOnSelect;
                chkSound.Checked = s.EnableSoundNotification;
                chkSimpleMode.Checked = s.EnableSimpleNoteMode;
                if (chkBottomSimpleMode != null) chkBottomSimpleMode.Checked = s.EnableSimpleNoteMode;

                // 急速默认短语
                string defPhrase = string.IsNullOrEmpty(s.RapidDefaultPhrase) ? "待修改" : s.RapidDefaultPhrase;
                if (cmbRapidDefaultPhrase != null) cmbRapidDefaultPhrase.Text = defPhrase;
                if (cmbBottomRapidPhrase != null)
                {
                    if (cmbBottomRapidPhrase.Items.Contains(defPhrase)) cmbBottomRapidPhrase.SelectedItem = defPhrase;
                    else if (cmbBottomRapidPhrase.Items.Count > 0) cmbBottomRapidPhrase.SelectedIndex = 0;
                }

                // 审查意见词典
                lstSnippets.Items.Clear();
                if (s.QuickReviewSnippets != null && s.QuickReviewSnippets.Count > 0)
                {
                    foreach (var item in s.QuickReviewSnippets) lstSnippets.Items.Add(item);
                }
                else
                {
                    var core = RapidPhraseService.GetCoreRapidPresetPhrases();
                    foreach (var phrase in core) lstSnippets.Items.Add(phrase);
                }

                // 5. 智能规范
                chkEnableAi.Checked = s.EnableAiSemantic;
                txtAiThreshold.Text = s.AiSimilarityThreshold.ToString("0.00");
                chkAutoFillCode.Checked = s.AutoFillStandardCode;

                // 6. 存储审计
                txtMaxNotesVolume.Text = s.MaxNotesPerVolume.ToString();
                chkAuditTrail.Checked = s.EnableAuditTrail;
                chkAutoBackup.Checked = s.AutoBackupOnSave;

                // 7. 批注使用对象选中与规则卡片同步
                if (cmbTargetPersonaHero != null && cmbTargetPersonaHero.Items.Count > 0)
                {
                    var allTemplates = ProjectTemplateService.GetAllTemplates();
                    var curTpl = allTemplates.Find(t => t.Id == s.CurrentTemplateId) ?? allTemplates[0];
                    for (int i = 0; i < cmbTargetPersonaHero.Items.Count; i++)
                    {
                        if (cmbTargetPersonaHero.Items[i] is AnnotationTemplate t && t.Id == curTpl.Id)
                        {
                            cmbTargetPersonaHero.SelectedIndex = i;
                            break;
                        }
                    }
                    if (curTpl.IsNonPlottingLocked)
                    {
                        chkNonPlottable.Enabled = false;
                    }
                    UpdateAudienceDisplay(curTpl);
                }
            }
            finally
            {
                _isUpdatingFromCode = false;
            }
        }

        private void CmbTargetPersonaHero_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbTargetPersonaHero.SelectedItem is AnnotationTemplate tpl)
            {
                if (tpl.IsNonPlottingLocked)
                {
                    chkNonPlottable.Checked = true;
                    chkNonPlottable.Enabled = false;
                }
                else
                {
                    chkNonPlottable.Enabled = true;
                }
                UpdateAudienceDisplay(tpl);
            }
        }

        private void BtnApplyPersonaHero_Click(object sender, EventArgs e)
        {
            if (cmbTargetPersonaHero.SelectedItem is AnnotationTemplate tpl)
            {
                var s = ConfigManager.Instance.CurrentSettings;
                ProjectTemplateService.ApplyTemplateToSettings(tpl.Id, s);

                txtCloudLayer.Text = tpl.LayerName;
                txtTextLayer.Text = tpl.LayerName + "_TEXT";
                txtPrefix.Text = tpl.Prefix;
                chkNonPlottable.Checked = tpl.ForceNonPlotting;
                chkNonPlottable.Enabled = !tpl.IsNonPlottingLocked;
                SetColorCombo(cmbCloudColor, tpl.ColorIndex);
                SetColorCombo(cmbTextColor, 7); // 文字颜色始终保持标准 7 号黑白色

                lstSnippets.Items.Clear();
                if (tpl.QuickReviewSnippets != null)
                {
                    foreach (var snip in tpl.QuickReviewSnippets) lstSnippets.Items.Add(snip);
                }

                UpdateAudienceDisplay(tpl);

                CadWinFormMessageBox.ShowInfo(string.Format("已成功应用【{0}】规则！\n- 角色定位：{1}\n- 批注图层：{2}\n- 编号前缀：{3}\n- 打印控制：{4}",
                    tpl.Name, tpl.RoleTitle ?? tpl.Name, tpl.LayerName, tpl.Prefix,
                    tpl.ForceNonPlotting ? (tpl.IsNonPlottingLocked ? "强制关闭打印 (已锁定)" : "关闭打印") : "开启打印"),
                    "使用对象规则应用成功", this);
            }
        }

        private void SetColorCombo(ComboBox cmb, int colorIndex)
        {
            if (cmb == null) return;
            switch (colorIndex)
            {
                case 1: cmb.SelectedIndex = 0; break;
                case 2: cmb.SelectedIndex = 1; break;
                case 3: cmb.SelectedIndex = 2; break;
                case 4: cmb.SelectedIndex = 3; break;
                case 5: cmb.SelectedIndex = 4; break;
                case 6: cmb.SelectedIndex = 5; break;
                case 7: cmb.SelectedIndex = 6; break;
                case 256: cmb.SelectedIndex = 7; break;
                default: cmb.SelectedIndex = 0; break;
            }
        }

        private int GetColorFromCombo(ComboBox cmb)
        {
            if (cmb == null) return 1;
            switch (cmb.SelectedIndex)
            {
                case 0: return 1;
                case 1: return 2;
                case 2: return 3;
                case 3: return 4;
                case 4: return 5;
                case 5: return 6;
                case 6: return 7;
                case 7: return 256;
                default: return 1;
            }
        }

        private void BtnReset_Click(object sender, EventArgs e)
        {
            if (CadWinFormMessageBox.Confirm("确定要将所有设置恢复为系统默认值吗？", "提示", this))
            {
                ConfigManager.ResetToDefaults();
                LoadCurrentSettings();
            }
        }

        private bool ApplySettingsFromForm()
        {
            var s = ConfigManager.Instance.CurrentSettings;

            // 1. 图层
            s.CloudLayer = string.IsNullOrEmpty(txtCloudLayer.Text.Trim()) ? "CAD_NOTE_CLOUD" : txtCloudLayer.Text.Trim();
            s.TextLayer = string.IsNullOrEmpty(txtTextLayer.Text.Trim()) ? "CAD_NOTE_TEXT" : txtTextLayer.Text.Trim();
            s.CloudColorIndex = GetColorFromCombo(cmbCloudColor);
            s.TextColorIndex = GetColorFromCombo(cmbTextColor);
            s.TextStyleName = string.IsNullOrEmpty(txtTextStyle.Text.Trim()) ? "Standard" : txtTextStyle.Text.Trim();
            s.ForceNonPlotting = chkNonPlottable.Checked;

            // 2. 几何
            if (double.TryParse(txtArcLength.Text.Trim(), out double arc)) s.ArcLengthRatio = Math.Max(1.0, arc);
            if (double.TryParse(txtBulge.Text.Trim(), out double b)) s.BulgeCurvature = Math.Max(0.1, Math.Min(1.0, b));
            if (cmbCloudStyle != null && cmbCloudStyle.SelectedIndex >= 0) s.CloudStyle = cmbCloudStyle.SelectedIndex;
            if (double.TryParse(txtCloudWidth.Text.Trim(), out double cw)) s.CloudWidth = Math.Max(0.0, cw);
            s.DefaultCloudType = cmbCloudType.SelectedIndex;
            if (double.TryParse(txtMinArc.Text.Trim(), out double minArc)) s.MinArcLength = Math.Max(1.0, minArc);

            // 3. 文字引线
            if (double.TryParse(txtTextHeight.Text.Trim(), out double th)) s.TextHeightRatio = Math.Max(1.0, th);
            if (double.TryParse(txtArrowSize.Text.Trim(), out double arr)) s.ArrowSizeRatio = Math.Max(0.5, arr);
            s.LeaderType = cmbLeaderType.SelectedIndex;
            s.AutoNumberPrefix = string.IsNullOrEmpty(txtPrefix.Text.Trim()) ? "【" : txtPrefix.Text.Trim();

            // 严重等级联动
            s.EnablePriorityColorLink = chkPriorityColorLink.Checked;
            s.PriorityColorImportant = GetColorFromCombo(cmbColorImportant);
            s.PriorityColorUrgent = GetColorFromCombo(cmbColorUrgent);

            // 4. 业务
            s.DefaultDiscipline = cmbDiscipline.SelectedItem as string ?? "建筑";
            s.DefaultAssignee = txtAssignee.Text.Trim();
            s.DefaultPriority = cmbPriority.SelectedItem as string ?? "重要";
            s.AutoZoomOnSelect = chkAutoZoom.Checked;
            s.EnableSoundNotification = chkSound.Checked;
            s.EnableSimpleNoteMode = chkSimpleMode.Checked;
            if (cmbRapidDefaultPhrase != null && !string.IsNullOrEmpty(cmbRapidDefaultPhrase.Text.Trim()))
            {
                s.RapidDefaultPhrase = cmbRapidDefaultPhrase.Text.Trim();
            }
            else if (cmbBottomRapidPhrase != null && cmbBottomRapidPhrase.SelectedItem != null)
            {
                s.RapidDefaultPhrase = cmbBottomRapidPhrase.SelectedItem.ToString();
            }

            // 审查意见快捷词典保存
            s.QuickReviewSnippets = new List<string>();
            foreach (var item in lstSnippets.Items)
            {
                s.QuickReviewSnippets.Add(item.ToString());
            }

            // 5. 智能规范
            s.EnableAiSemantic = chkEnableAi.Checked;
            if (double.TryParse(txtAiThreshold.Text.Trim(), out double sim)) s.AiSimilarityThreshold = Math.Max(0.1, Math.Min(1.0, sim));
            s.AutoFillStandardCode = chkAutoFillCode.Checked;

            // 6. 存储审计
            if (int.TryParse(txtMaxNotesVolume.Text.Trim(), out int maxVol)) s.MaxNotesPerVolume = Math.Max(10, maxVol);
            s.EnableAuditTrail = chkAuditTrail.Checked;
            s.AutoBackupOnSave = chkAutoBackup.Checked;

            // 7. 若当前有活动 AutoCAD 文档，即刻将最新图层配置（双图层与不打印状态）热同步至当前图纸
            try
            {
                var doc = AcApp.DocumentManager?.MdiActiveDocument;
                if (doc?.Database != null)
                {
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        ValidationService.EnsureNoteLayers(doc.Database, tr, s, CadAutoCloudNote.Core.Protocols.NotePriority.Important);
                        tr.Commit();
                    }
                }
            }
            catch { }

            ConfigManager.Instance.Save();
            return true;
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            try
            {
                ApplySettingsFromForm();
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                CadWinFormMessageBox.ShowError("保存配置失败: " + ex.Message, "错误", this);
            }
        }

        private void BtnSaveAndCreate_Click(object sender, EventArgs e)
        {
            try
            {
                ApplySettingsFromForm();
                this.DialogResult = DialogResult.OK;
                this.Close();

                var doc = AcApp.DocumentManager.MdiActiveDocument;
                doc?.SendStringToExecute("CNOTE\n", true, false, false);
            }
            catch (Exception ex)
            {
                CadWinFormMessageBox.ShowError("保存配置失败: " + ex.Message, "错误", this);
            }
        }
    }

    public class SleekPanel : Panel
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Radius { get; set; } = 6;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color BorderColor { get; set; } = Color.Transparent;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public DashStyle BorderDashStyle { get; set; } = DashStyle.Solid;

        public SleekPanel()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (this.Parent != null)
            {
                using (SolidBrush pb = new SolidBrush(this.Parent.BackColor))
                    e.Graphics.FillRectangle(pb, this.ClientRectangle);
            }

            Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);

            using (GraphicsPath path = GetRoundedPath(rect, Radius))
            {
                using (SolidBrush b = new SolidBrush(this.BackColor))
                    e.Graphics.FillPath(b, path);

                if (BorderColor != Color.Transparent)
                {
                    using (Pen p = new Pen(BorderColor, 1))
                    {
                        p.DashStyle = this.BorderDashStyle;
                        if (this.BorderDashStyle == DashStyle.Dash) p.DashPattern = new float[] { 4, 4 };
                        e.Graphics.DrawPath(p, path);
                    }
                }
            }
        }

        public static GraphicsPath GetRoundedPath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (radius <= 0) { path.AddRectangle(rect); return path; }
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    public class SleekButton : Button
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Radius { get; set; } = 4;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color HoverColor { get; set; } = Color.FromArgb(90, 90, 90);
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color DefaultColor { get; set; } = Color.FromArgb(70, 70, 70);
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color BorderColor { get; set; } = Color.Transparent;
        private bool isHovered = false;

        public SleekButton()
        {
            this.FlatStyle = FlatStyle.Flat;
            this.FlatAppearance.BorderSize = 0;
            this.Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { isHovered = true; this.Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { isHovered = false; this.Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color parentBg = (this.Parent != null && this.Parent.BackColor != Color.Transparent)
                ? this.Parent.BackColor
                : (this.Parent?.Parent != null && this.Parent.Parent.BackColor != Color.Transparent ? this.Parent.Parent.BackColor : SystemColors.Control);
            using (SolidBrush pb = new SolidBrush(parentBg))
                g.FillRectangle(pb, this.ClientRectangle);

            Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
            using (GraphicsPath path = SleekPanel.GetRoundedPath(rect, Radius))
            {
                Color fill = isHovered ? HoverColor : DefaultColor;
                using (SolidBrush b = new SolidBrush(fill))
                    g.FillPath(b, path);

                if (BorderColor != Color.Transparent)
                {
                    using (Pen p = new Pen(BorderColor, 1))
                        g.DrawPath(p, path);
                }
            }

            TextRenderer.DrawText(g, this.Text, this.Font, rect, this.ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }
}
