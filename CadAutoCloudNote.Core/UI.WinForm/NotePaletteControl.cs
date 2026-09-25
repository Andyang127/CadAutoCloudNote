using System;
using System.Collections.Generic;
using System.Drawing;
using Font = System.Drawing.Font;
using Color = System.Drawing.Color;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.ApplicationServices;
#if CAD_R17 || CAD_R18
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
#else
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
#endif
using CadAutoCloudNote.Core.Helpers;
using CadAutoCloudNote.Core.Models;
using CadAutoCloudNote.Core.Protocols;
using CadAutoCloudNote.Core.Services;

namespace CadAutoCloudNote.Core.UI.WinForm
{
    /// <summary>
    /// 现代化批注侧边栏看板控件（WinForms 版，支持 AutoCAD COLORTHEME 双色自适应、多字段检索与状态胶囊交互）
    /// </summary>
    public class NotePaletteControl : UserControl
    {
        private ComboBox cmbTargetPersona;
        private TextBox txtSearch;
        private Button btnClearSearch;
        private ComboBox cmbStatusFilter;
        private ListView lvNotes;
        private Button btnRefresh;
        private Button btnAdd;
        private Button btnReply;
        private Button btnTable;
        private Button btnExport;
        private Button btnSettings;
        private Button btnHelp;
        private Label lblMatchSummary;
        private ContextMenuStrip contextMenuNotes;

        // 底部控制台与流转详情面板 (保留字段满足自动化架构测试断言)
        private Panel pnlBottomConsole;
        private FlowLayoutPanel pnlStatusPills;
        private Button btnPillAll;
        private Button btnPillPending;
        private Button btnPillResolved;
        private Button btnPillRejected;
        private Button btnPillClosed;

        private Panel pnlDetail;
        private Label lblDetailTitle;
        private Button btnQuickZoom;
        private Button btnQuickReply;
        private Label lblDetailContent;
        private Panel pnlReplyBubble;
        private Label lblDetailReply;

        private const string SEARCH_WATERMARK = "🔍 搜索编号/标题/意见/专业/批注人/答复...";
        private bool _isWatermarkActive = true;
        private bool _isDarkTheme = false;

        public NotePaletteControl()
        {
            InitializeComponent();
            ApplyAutoCadSmartTheme();
            this.Load += (s, e) => ApplyAutoCadSmartTheme();
        }

        private void InitializeComponent()
        {
            this.Font = new System.Drawing.Font("Microsoft YaHei", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);

            // ================= 顶部自适应控制面板 =================
            TableLayoutPanel topPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(4, 5, 4, 4)
            };
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            // 行0：批注使用对象快速切换 (规范顶置架构)
            TableLayoutPanel personaRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 26,
                Margin = new Padding(0, 0, 0, 4),
                ColumnCount = 2,
                RowCount = 1
            };
            personaRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62F));
            personaRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            Label lblPersona = new Label
            {
                Text = "使用对象:",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Microsoft YaHei", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105)
            };
            cmbTargetPersona = new ComboBox
            {
                Dock = DockStyle.Fill,
                Height = 23,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Microsoft YaHei", 8.5F)
            };

            var allTemplates = ProjectTemplateService.GetAllTemplates();
            cmbTargetPersona.DisplayMember = "Name";
            cmbTargetPersona.ValueMember = "Id";
            cmbTargetPersona.DataSource = allTemplates;
            var curTpl = allTemplates.Find(t => t.Id == Config.ConfigManager.Instance.CurrentSettings.CurrentTemplateId)
                         ?? ProjectTemplateService.CurrentTemplate
                         ?? allTemplates[0];
            cmbTargetPersona.SelectedItem = curTpl;
            cmbTargetPersona.SelectedIndexChanged += CmbTargetPersona_SelectedIndexChanged;

            personaRow.Controls.Add(lblPersona, 0, 0);
            personaRow.Controls.Add(cmbTargetPersona, 1, 0);

            // 行1：搜索输入与状态下拉筛选
            TableLayoutPanel filterRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 27,
                Margin = new Padding(0, 0, 0, 4),
                ColumnCount = 3,
                RowCount = 1
            };
            filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92F));
            filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48F));

            // 搜索输入容器 (内嵌一键清除 ✕ 按钮与水印提示)
            Panel pnlSearchBox = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 24,
                Margin = new Padding(0, 1, 4, 1),
                BorderStyle = BorderStyle.FixedSingle
            };

            btnClearSearch = new Button
            {
                Dock = DockStyle.Right,
                Width = 18,
                Text = "✕",
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Visible = false,
                Font = new Font("Microsoft YaHei", 7.5F, FontStyle.Regular),
                Margin = new Padding(0)
            };
            btnClearSearch.FlatAppearance.BorderSize = 0;
            btnClearSearch.Click += (s, e) =>
            {
                txtSearch.Text = string.Empty;
                txtSearch.Focus();
            };

            txtSearch = new TextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                Font = new Font("Microsoft YaHei", 8.5F),
                Margin = new Padding(2, 3, 2, 0)
            };
            txtSearch.Text = SEARCH_WATERMARK;
            txtSearch.ForeColor = Color.Gray;

            txtSearch.Enter += (s, e) =>
            {
                if (_isWatermarkActive)
                {
                    txtSearch.Text = string.Empty;
                    txtSearch.ForeColor = _isDarkTheme ? Color.FromArgb(226, 232, 240) : Color.FromArgb(30, 41, 59);
                    _isWatermarkActive = false;
                }
            };
            txtSearch.Leave += (s, e) =>
            {
                if (string.IsNullOrEmpty(txtSearch.Text != null ? txtSearch.Text.Trim() : string.Empty))
                {
                    _isWatermarkActive = true;
                    txtSearch.Text = SEARCH_WATERMARK;
                    txtSearch.ForeColor = Color.Gray;
                    btnClearSearch.Visible = false;
                }
            };
            txtSearch.TextChanged += (s, e) =>
            {
                if (!_isWatermarkActive)
                {
                    btnClearSearch.Visible = !string.IsNullOrEmpty(txtSearch.Text);
                    RefreshList();
                }
            };
            txtSearch.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    txtSearch.Text = string.Empty;
                    e.SuppressKeyPress = true;
                }
            };

            pnlSearchBox.Controls.Add(txtSearch);
            pnlSearchBox.Controls.Add(btnClearSearch);

            cmbStatusFilter = new ComboBox
            {
                Dock = DockStyle.Fill,
                Height = 23,
                Margin = new Padding(0, 1, 4, 0),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Microsoft YaHei", 8.5F)
            };
            cmbStatusFilter.Items.AddRange(new object[] { "全部状态", "待办", "已改", "驳回", "已闭环" });
            cmbStatusFilter.SelectedIndex = 0;
            cmbStatusFilter.SelectedIndexChanged += (s, e) => RefreshList();

            btnRefresh = new Button
            {
                Dock = DockStyle.Fill,
                Height = 24,
                Text = "刷新",
                Margin = new Padding(0),
                FlatStyle = FlatStyle.System
            };
            btnRefresh.Click += (s, e) => RefreshList();

            filterRow.Controls.Add(pnlSearchBox, 0, 0);
            filterRow.Controls.Add(cmbStatusFilter, 1, 0);
            filterRow.Controls.Add(btnRefresh, 2, 0);

            // 行2：操作按钮栏 (自适应折行排版)
            FlowLayoutPanel actionRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, 3)
            };

            btnAdd = new Button { AutoSize = true, Height = 24, Text = "+ 新建", Margin = new Padding(0, 0, 3, 3), FlatStyle = FlatStyle.System };
            btnAdd.Click += (s, e) => ExecuteCommand("CLOUDNOTE");

            btnReply = new Button { AutoSize = true, Height = 24, Text = "回复/流转", Margin = new Padding(0, 0, 3, 3), FlatStyle = FlatStyle.System };
            btnReply.Click += BtnReply_Click;

            btnTable = new Button { AutoSize = true, Height = 24, Text = "汇总表", Margin = new Padding(0, 0, 3, 3), FlatStyle = FlatStyle.System };
            btnTable.Click += (s, e) => ExecuteCommand("NOTETABLE");

            btnExport = new Button { AutoSize = true, Height = 24, Text = "导出", Margin = new Padding(0, 0, 3, 3), FlatStyle = FlatStyle.System };
            btnExport.Click += (s, e) => ExecuteCommand("NOTEEXPORT");

            btnSettings = new Button { AutoSize = true, Height = 24, Text = "设置", Margin = new Padding(0, 0, 3, 3), FlatStyle = FlatStyle.System };
            btnSettings.Click += (s, e) => NoteUIProvider.Instance.ShowConfigDialog();

            btnHelp = new Button { AutoSize = true, Height = 24, Text = "帮助", Margin = new Padding(0, 0, 0, 3), FlatStyle = FlatStyle.System };
            btnHelp.Click += (s, e) => HelpService.OpenReadme();

            actionRow.Controls.AddRange(new Control[] { btnAdd, btnReply, btnTable, btnExport, btnSettings, btnHelp });

            // 行3：匹配统计摘要栏
            lblMatchSummary = new Label
            {
                Dock = DockStyle.Top,
                Height = 16,
                Font = new Font("Microsoft YaHei", 8F),
                ForeColor = Color.FromArgb(100, 116, 139),
                Text = "共 0 条批注 · 双击对焦",
                TextAlign = ContentAlignment.MiddleLeft
            };

            topPanel.Controls.Add(personaRow, 0, 0);
            topPanel.Controls.Add(filterRow, 0, 1);
            topPanel.Controls.Add(actionRow, 0, 2);
            topPanel.Controls.Add(lblMatchSummary, 0, 3);

            // 右键菜单
            contextMenuNotes = new ContextMenuStrip();
            var mnuNew = new ToolStripMenuItem("新建云线批注(N)");
            mnuNew.Click += (s, e) => ExecuteCommand("CLOUDNOTE");
            var mnuConvert = new ToolStripMenuItem("对象转云线(O)...");
            mnuConvert.Click += (s, e) => ExecuteCommand("CNCONVERT");
            var mnuReply = new ToolStripMenuItem("查看/流转批注(V)");
            mnuReply.Click += BtnReply_Click;
            var mnuTable = new ToolStripMenuItem("生成图面汇总表(T)");
            mnuTable.Click += (s, e) => ExecuteCommand("NOTETABLE");
            var mnuRefresh = new ToolStripMenuItem("刷新批注列表(R)");
            mnuRefresh.Click += (s, e) => RefreshList();
            var mnuSettings = new ToolStripMenuItem("全局参数设置(S)...");
            mnuSettings.Click += (s, e) => NoteUIProvider.Instance.ShowConfigDialog();
            var mnuHelp = new ToolStripMenuItem("帮助与使用手册(H)...");
            mnuHelp.Click += (s, e) => HelpService.OpenReadme();

            contextMenuNotes.Items.AddRange(new ToolStripItem[] { mnuNew, mnuConvert, mnuReply, new ToolStripSeparator(), mnuTable, mnuRefresh, new ToolStripSeparator(), mnuSettings, mnuHelp });

            // ================= 中部批注列表 =================
            lvNotes = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false,
                ShowItemToolTips = true, // 自动化测试断言要求
                ContextMenuStrip = contextMenuNotes
            };
            lvNotes.Columns.Add("标题", 130);
            lvNotes.Columns.Add("专业", 50);
            lvNotes.Columns.Add("状态", 55);
            lvNotes.Columns.Add("批注者", 85);
            lvNotes.DoubleClick += LvNotes_DoubleClick;
            lvNotes.SelectedIndexChanged += LvNotes_SelectedIndexChanged;

            lvNotes.Resize += (s, e) =>
            {
                if (lvNotes.Columns.Count >= 4)
                {
                    int fixedW = lvNotes.Columns[1].Width + lvNotes.Columns[2].Width + lvNotes.Columns[3].Width;
                    int scrollW = SystemInformation.VerticalScrollBarWidth + 4;
                    int avail = lvNotes.ClientSize.Width - fixedW - scrollW;
                    lvNotes.Columns[0].Width = Math.Max(60, avail);
                }
            };

            // ================= 底部现代控制台 (胶囊统计栏 + 详情卡片) =================
            pnlBottomConsole = new Panel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(0)
            };

            // 1. 全图状态分类胶囊栏 (支持一键交互点击即时切换筛选过滤)
            pnlStatusPills = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                Padding = new Padding(4, 4, 4, 3),
                BackColor = Color.FromArgb(248, 250, 252)
            };

            btnPillAll = CreatePillButton("全部 0", -1, Color.FromArgb(51, 65, 85), Color.FromArgb(241, 245, 249));
            btnPillPending = CreatePillButton("● 待办 0", 0, Color.FromArgb(185, 28, 28), Color.FromArgb(254, 242, 242));
            btnPillResolved = CreatePillButton("● 已改 0", 1, Color.FromArgb(4, 120, 87), Color.FromArgb(236, 253, 245));
            btnPillRejected = CreatePillButton("● 驳回 0", 2, Color.FromArgb(180, 83, 9), Color.FromArgb(255, 251, 235));
            btnPillClosed = CreatePillButton("● 闭环 0", 3, Color.FromArgb(75, 85, 99), Color.FromArgb(243, 244, 246));

            pnlStatusPills.Controls.AddRange(new Control[] { btnPillAll, btnPillPending, btnPillResolved, btnPillRejected, btnPillClosed });

            // 2. 详情卡片面板 (必须包含 pnlDetail, lblDetailTitle, lblDetailContent, lblDetailReply 满足测试断言)
            pnlDetail = new Panel
            {
                Dock = DockStyle.Top,
                Height = 100,
                Padding = new Padding(6, 4, 6, 4),
                BackColor = Color.FromArgb(248, 249, 250)
            };

            Panel pnlDetailHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 22
            };

            btnQuickReply = new Button
            {
                Dock = DockStyle.Right,
                Width = 54,
                Height = 20,
                Text = "✍️ 流转",
                Font = new Font("Microsoft YaHei", 7.5F),
                FlatStyle = FlatStyle.System,
                Enabled = false,
                Margin = new Padding(2, 0, 0, 0)
            };
            btnQuickReply.Click += BtnReply_Click;

            btnQuickZoom = new Button
            {
                Dock = DockStyle.Right,
                Width = 54,
                Height = 20,
                Text = "🎯 定位",
                Font = new Font("Microsoft YaHei", 7.5F),
                FlatStyle = FlatStyle.System,
                Enabled = false,
                Margin = new Padding(2, 0, 2, 0)
            };
            btnQuickZoom.Click += LvNotes_DoubleClick;

            lblDetailTitle = new Label
            {
                Dock = DockStyle.Fill,
                Font = new System.Drawing.Font("Microsoft YaHei", 8.5F, System.Drawing.FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Text = "批注详情与流转答复预览",
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };

            pnlDetailHeader.Controls.Add(lblDetailTitle);
            pnlDetailHeader.Controls.Add(btnQuickZoom);
            pnlDetailHeader.Controls.Add(btnQuickReply);

            lblDetailContent = new Label
            {
                Dock = DockStyle.Top,
                Height = 36,
                Font = new System.Drawing.Font("Microsoft YaHei", 8F, System.Drawing.FontStyle.Regular),
                ForeColor = Color.FromArgb(71, 85, 105),
                Text = "在列表中选中批注可实时查看完整审查意见与最新流转答复。\n支持悬停查看浮动卡片，双击自动平移定位图纸。",
                AutoEllipsis = true,
                Padding = new Padding(0, 2, 0, 2)
            };

            pnlReplyBubble = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(5, 3, 5, 3),
                BackColor = Color.FromArgb(240, 253, 250),
                BorderStyle = BorderStyle.FixedSingle
            };

            lblDetailReply = new Label
            {
                Dock = DockStyle.Fill,
                Font = new System.Drawing.Font("Microsoft YaHei", 8F, System.Drawing.FontStyle.Regular),
                ForeColor = Color.FromArgb(15, 118, 110),
                Text = "最新答复: -",
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft
            };

            pnlReplyBubble.Controls.Add(lblDetailReply);

            pnlDetail.Controls.Add(pnlReplyBubble);
            pnlDetail.Controls.Add(lblDetailContent);
            pnlDetail.Controls.Add(pnlDetailHeader);

            pnlDetail.Paint += (s, e) =>
            {
                using (Pen p = new Pen(Color.FromArgb(226, 232, 240), 1))
                {
                    e.Graphics.DrawLine(p, 0, 0, pnlDetail.Width, 0);
                }
            };

            pnlBottomConsole.Controls.Add(pnlDetail);
            pnlBottomConsole.Controls.Add(pnlStatusPills);

            this.Controls.Add(lvNotes);
            this.Controls.Add(pnlBottomConsole);
            this.Controls.Add(topPanel);
        }

        private Button CreatePillButton(string text, int tag, Color foreColor, Color backColor)
        {
            Button btn = new Button
            {
                Text = text,
                Tag = tag,
                AutoSize = true,
                Height = 22,
                Margin = new Padding(0, 0, 3, 2),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei", 8F, FontStyle.Regular),
                ForeColor = foreColor,
                BackColor = backColor,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            btn.FlatAppearance.BorderSize = 1;
            btn.Click += PillButton_Click;
            return btn;
        }

        private void PillButton_Click(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                int tag = (int)btn.Tag;
                int targetIdx = tag + 1; // -1 -> 0, 0 -> 1, 1 -> 2, 2 -> 3, 3 -> 4
                if (cmbStatusFilter.SelectedIndex == targetIdx && targetIdx != 0)
                {
                    cmbStatusFilter.SelectedIndex = 0;
                }
                else
                {
                    cmbStatusFilter.SelectedIndex = targetIdx;
                }
            }
        }

        private void CmbTargetPersona_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbTargetPersona.SelectedItem is AnnotationTemplate tpl)
            {
                var settings = Config.ConfigManager.Instance.CurrentSettings;
                ProjectTemplateService.ApplyTemplateToSettings(tpl.Id, settings);
            }
        }

        /// <summary>
        /// 智能感知 AutoCAD COLORTHEME 变量并自适应深浅双色主题
        /// </summary>
        public void ApplyAutoCadSmartTheme()
        {
            try
            {
#if CAD_R17 || CAD_R18 || CAD_R19
                _isDarkTheme = false;
#elif !CAD_TEST
                if (Autodesk.AutoCAD.ApplicationServices.Application.Version.Major < 20)
                {
                    _isDarkTheme = false;
                }
                else
                {
                    object themeVar = Autodesk.AutoCAD.ApplicationServices.Application.GetSystemVariable("COLORTHEME");
                    _isDarkTheme = themeVar != null && Convert.ToInt16(themeVar) == 0;
                }
#else
                _isDarkTheme = false;
#endif
            }
            catch { _isDarkTheme = false; }

            if (_isDarkTheme)
            {
                this.BackColor = Color.FromArgb(30, 31, 34);
                this.ForeColor = Color.FromArgb(226, 232, 240);
                lvNotes.BackColor = Color.FromArgb(30, 31, 34);
                lvNotes.ForeColor = Color.FromArgb(226, 232, 240);
                pnlStatusPills.BackColor = Color.FromArgb(43, 45, 48);
                pnlDetail.BackColor = Color.FromArgb(43, 45, 48);
                pnlReplyBubble.BackColor = Color.FromArgb(26, 46, 53);
                lblDetailReply.ForeColor = Color.FromArgb(56, 189, 248);
                lblDetailTitle.ForeColor = Color.FromArgb(241, 245, 249);
                lblDetailContent.ForeColor = Color.FromArgb(148, 163, 184);
                txtSearch.BackColor = Color.FromArgb(26, 27, 30);
                txtSearch.ForeColor = _isWatermarkActive ? Color.Gray : Color.FromArgb(226, 232, 240);
            }
            else
            {
                this.BackColor = Color.FromArgb(248, 250, 252);
                this.ForeColor = Color.FromArgb(30, 41, 59);
                lvNotes.BackColor = Color.White;
                lvNotes.ForeColor = Color.FromArgb(30, 41, 59);
                pnlStatusPills.BackColor = Color.FromArgb(248, 250, 252);
                pnlDetail.BackColor = Color.FromArgb(248, 249, 250);
                pnlReplyBubble.BackColor = Color.FromArgb(240, 253, 250);
                lblDetailReply.ForeColor = Color.FromArgb(15, 118, 110);
                lblDetailTitle.ForeColor = Color.FromArgb(30, 41, 59);
                lblDetailContent.ForeColor = Color.FromArgb(71, 85, 105);
                txtSearch.BackColor = Color.White;
                txtSearch.ForeColor = _isWatermarkActive ? Color.Gray : Color.FromArgb(30, 41, 59);
            }
        }

        public void RefreshList()
        {
            try
            {
                Document doc = AcApp.DocumentManager.MdiActiveDocument;
                if (doc?.Database == null) return;

                List<NoteRecord> notes = NoteService.Instance.GetAllNotes(doc.Database);
                notes.Sort((a, b) => a.SequenceNumber.CompareTo(b.SequenceNumber));

                string kw = (_isWatermarkActive || txtSearch.Text == null) ? string.Empty : txtSearch.Text.Trim();
                int filterStateIdx = cmbStatusFilter.SelectedIndex;

                lvNotes.Items.Clear();

                int countAll = 0;
                int countPending = 0;
                int countResolved = 0;
                int countRejected = 0;
                int countClosed = 0;

                string prefix = Config.ConfigManager.Instance.CurrentSettings.AutoNumberPrefix;

                foreach (var n in notes)
                {
                    // 幽灵批注净化：若该批注的所有图元均已在图纸中不存在/被手动删除，自动清理并不予展示
                    bool hasAliveEntity = false;
                    if (n.Cloud?.EntityHandles != null)
                    {
                        foreach (var h in n.Cloud.EntityHandles)
                        {
                            ObjectId cid;
                            if (XDataHelper.SafeGetObjectId(doc.Database, h, out cid) && !cid.IsNull && !cid.IsErased)
                            {
                                hasAliveEntity = true;
                                break;
                            }
                        }
                    }
                    if (!hasAliveEntity && n.Text != null)
                    {
                        ObjectId tid;
                        if (!string.IsNullOrEmpty(n.Text.MTextHandle) && XDataHelper.SafeGetObjectId(doc.Database, n.Text.MTextHandle, out tid) && !tid.IsNull && !tid.IsErased)
                            hasAliveEntity = true;

                        ObjectId fid;
                        if (!string.IsNullOrEmpty(n.Text.FrameHandle) && XDataHelper.SafeGetObjectId(doc.Database, n.Text.FrameHandle, out fid) && !fid.IsNull && !fid.IsErased)
                            hasAliveEntity = true;
                    }

                    if (!hasAliveEntity)
                    {
                        continue;
                    }

                    countAll++;
                    switch (n.State)
                    {
                        case NoteLifecycleState.Pending: countPending++; break;
                        case NoteLifecycleState.Resolved: countResolved++; break;
                        case NoteLifecycleState.Rejected: countRejected++; break;
                        case NoteLifecycleState.Closed: countClosed++; break;
                    }

                    if (filterStateIdx > 0)
                    {
                        NoteLifecycleState target = (NoteLifecycleState)(filterStateIdx - 1);
                        if (n.State != target) continue;
                    }

                    // 多字段全维度检索 (编号、格式化编号、标题、审查内容、专业、批注者、状态、答复内容、答复人、审计操作)
                    if (!string.IsNullOrEmpty(kw))
                    {
                        string formattedSeq = NoteService.FormatSequencePrefix(prefix, n.SequenceNumber);
                        string stateName = NoteLifecycleStateExtensions.ToDisplayName(n.State);
                        string assignee = n.Assignee ?? n.CreatedBy ?? string.Empty;

                        bool match = false;
                        if ((n.Title != null && n.Title.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (n.Content != null && n.Content.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (n.Discipline != null && n.Discipline.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (assignee.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (stateName.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0) ||
                            n.SequenceNumber.ToString() == kw ||
                            formattedSeq.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            match = true;
                        }

                        if (!match && n.Replies != null)
                        {
                            foreach (var r in n.Replies)
                            {
                                if ((r.Content != null && r.Content.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                    (r.Author != null && r.Author.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0))
                                {
                                    match = true;
                                    break;
                                }
                            }
                        }

                        if (!match && n.AuditLogs != null)
                        {
                            foreach (var a in n.AuditLogs)
                            {
                                if ((a.Action != null && a.Action.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                    (a.FieldChanges != null && a.FieldChanges.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                    (a.Operator != null && a.Operator.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0))
                                {
                                    match = true;
                                    break;
                                }
                            }
                        }

                        if (!match) continue;
                    }

                    string formattedSeqStr = NoteService.FormatSequencePrefix(prefix, n.SequenceNumber);
                    string cleanTitle = n.Title ?? string.Empty;
                    if (cleanTitle.StartsWith("【") && cleanTitle.Contains("】"))
                    {
                        int cIdx = cleanTitle.IndexOf('】');
                        cleanTitle = cleanTitle.Substring(cIdx + 1).Trim();
                    }
                    string displayTitle = n.SequenceNumber > 0 ? string.Format("{0} {1}", formattedSeqStr, cleanTitle) : cleanTitle;

                    string latestReplyInfo = "暂无流转答复";
                    if (n.Replies != null && n.Replies.Count > 0)
                    {
                        var r = n.Replies[n.Replies.Count - 1];
                        latestReplyInfo = string.Format("[{0}] {1}: {2} ({3:MM-dd HH:mm})", 
                            NoteLifecycleStateExtensions.ToDisplayName(r.StateTransitionTo),
                            !string.IsNullOrEmpty(r.Author) ? r.Author : "协同人",
                            !string.IsNullOrEmpty(r.Content) ? r.Content : "(无说明)",
                            r.Timestamp);
                    }
                    else if (n.AuditLogs != null && n.AuditLogs.Count > 0)
                    {
                        for (int k = n.AuditLogs.Count - 1; k >= 0; k--)
                        {
                            var log = n.AuditLogs[k];
                            if (!string.IsNullOrEmpty(log.FieldChanges) || !string.IsNullOrEmpty(log.Action))
                            {
                                latestReplyInfo = string.Format("{0} ({1} · {2:MM-dd HH:mm})",
                                    log.FieldChanges ?? log.Action,
                                    log.Operator ?? "系统",
                                    log.Timestamp);
                                break;
                            }
                        }
                    }

                    ListViewItem lvi = new ListViewItem(displayTitle);
                    lvi.SubItems.Add(n.Discipline ?? "");
                    lvi.SubItems.Add(NoteLifecycleStateExtensions.ToDisplayName(n.State));
                    lvi.SubItems.Add(n.Assignee ?? "");
                    lvi.Tag = n;

                    if (n.State == NoteLifecycleState.Pending)
                        lvi.ForeColor = Color.Red;
                    else if (n.State == NoteLifecycleState.Resolved)
                        lvi.ForeColor = Color.DarkGreen;
                    else if (n.State == NoteLifecycleState.Closed)
                        lvi.ForeColor = Color.Gray;

                    lvi.ToolTipText = string.Format(
                        "【批注】{0}\n" +
                        "【专业】{1}  【批注者】{2}  【状态】{3}\n" +
                        "【审查意见】{4}\n" +
                        "─────────────────────────────────\n" +
                        "【最新答复】{5}",
                        displayTitle,
                        n.Discipline ?? "通用",
                        n.Assignee ?? n.CreatedBy ?? "未指定",
                        NoteLifecycleStateExtensions.ToDisplayName(n.State),
                        !string.IsNullOrEmpty(n.Content) ? n.Content : "(无详细意见)",
                        latestReplyInfo);

                    lvNotes.Items.Add(lvi);
                }

                // 更新状态胶囊数值与视觉高亮
                btnPillAll.Text = "全部 " + countAll;
                btnPillPending.Text = "● 待办 " + countPending;
                btnPillResolved.Text = "● 已改 " + countResolved;
                btnPillRejected.Text = "● 驳回 " + countRejected;
                btnPillClosed.Text = "● 闭环 " + countClosed;

                Button[] pills = { btnPillAll, btnPillPending, btnPillResolved, btnPillRejected, btnPillClosed };
                for (int i = 0; i < pills.Length; i++)
                {
                    bool isActive = (i == filterStateIdx);
                    pills[i].FlatAppearance.BorderSize = isActive ? 2 : 1;
                    pills[i].Font = new Font("Microsoft YaHei", 8F, isActive ? FontStyle.Bold : FontStyle.Regular);
                }

                // 更新摘要提示
                if (string.IsNullOrEmpty(kw) && filterStateIdx == 0)
                {
                    lblMatchSummary.Text = string.Format("共 {0} 条批注 · 双击平移定位", countAll);
                }
                else
                {
                    lblMatchSummary.Text = string.Format("匹配 {0} / 共 {1} 条批注", lvNotes.Items.Count, countAll);
                }

                LvNotes_SelectedIndexChanged(null, EventArgs.Empty);
            }
            catch { }
        }

        private void LvNotes_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lblDetailTitle == null) return;
            if (lvNotes.SelectedItems.Count == 0)
            {
                lblDetailTitle.Text = "批注详情与流转答复预览";
                lblDetailContent.Text = "在列表中选中批注可实时查看完整审查意见与最新流转答复。\n支持悬停查看浮动卡片，双击自动平移定位图纸。";
                lblDetailReply.Text = "最新答复: -";
                btnQuickZoom.Enabled = false;
                btnQuickReply.Enabled = false;
                return;
            }

            var note = lvNotes.SelectedItems[0].Tag as NoteRecord;
            if (note == null) return;

            string prefix = Config.ConfigManager.Instance.CurrentSettings.AutoNumberPrefix;
            string formattedSeq = NoteService.FormatSequencePrefix(prefix, note.SequenceNumber);
            string title = note.Title ?? string.Empty;
            lblDetailTitle.Text = string.Format("{0} {1} [{2}] · {3} · 批注者: {4}",
                formattedSeq,
                title,
                NoteLifecycleStateExtensions.ToDisplayName(note.State),
                note.Discipline ?? "通用",
                note.Assignee ?? note.CreatedBy ?? "未指定");

            lblDetailContent.Text = "审查意见: " + (!string.IsNullOrEmpty(note.Content) ? note.Content : "(无详细意见)");

            string replyStr = "最新答复: -";
            if (note.Replies != null && note.Replies.Count > 0)
            {
                var r = note.Replies[note.Replies.Count - 1];
                replyStr = string.Format("最新答复: [{0}] {1}: {2} ({3:MM-dd HH:mm})",
                    NoteLifecycleStateExtensions.ToDisplayName(r.StateTransitionTo),
                    !string.IsNullOrEmpty(r.Author) ? r.Author : "协同人",
                    !string.IsNullOrEmpty(r.Content) ? r.Content : "(无说明)",
                    r.Timestamp);
            }
            else if (note.AuditLogs != null && note.AuditLogs.Count > 0)
            {
                for (int k = note.AuditLogs.Count - 1; k >= 0; k--)
                {
                    var log = note.AuditLogs[k];
                    if (!string.IsNullOrEmpty(log.FieldChanges) || !string.IsNullOrEmpty(log.Action))
                    {
                        replyStr = string.Format("最新流转: {0} ({1} · {2:MM-dd HH:mm})",
                            log.FieldChanges ?? log.Action,
                            log.Operator ?? "系统",
                            log.Timestamp);
                        break;
                    }
                }
            }
            lblDetailReply.Text = replyStr;
            btnQuickZoom.Enabled = true;
            btnQuickReply.Enabled = true;
        }

        private void LvNotes_DoubleClick(object sender, EventArgs e)
        {
            if (lvNotes.SelectedItems.Count == 0) return;
            var note = lvNotes.SelectedItems[0].Tag as NoteRecord;
            if (note == null) return;

            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            // 定位到该批注
            if (note.Cloud != null && (note.Cloud.MinPoint.X != 0 || note.Cloud.MaxPoint.X != 0))
            {
                double margin = Math.Max(note.Cloud.ArcLength * 3.0, 50.0);
                string cmd = string.Format("._ZOOM _W {0:0.###},{1:0.###} {2:0.###},{3:0.###}\n",
                    note.Cloud.MinPoint.X - margin,
                    note.Cloud.MinPoint.Y - margin,
                    note.Cloud.MaxPoint.X + margin,
                    note.Cloud.MaxPoint.Y + margin);
                doc.SendStringToExecute(cmd, true, false, false);
            }
        }

        private void BtnReply_Click(object sender, EventArgs e)
        {
            if (lvNotes.SelectedItems.Count == 0)
            {
                CadWinFormMessageBox.ShowInfo("请先在列表中选中一条批注！", "提示", this);
                return;
            }

            var note = lvNotes.SelectedItems[0].Tag as NoteRecord;
            if (note == null) return;

            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc?.Database == null) return;

            string replyContent;
            NoteLifecycleState targetState;
            string replyAuthor;
            if (NoteUIProvider.Instance.ShowNoteReplyDialog(note, out replyContent, out targetState, out replyAuthor))
            {
                string err = string.Empty;
                bool success = false;
                try
                {
                    using (doc.LockDocument())
                    {
                        success = ReplyService.AddReply(doc.Database, note.NoteId, replyContent, replyAuthor, targetState, out err);
                    }
                }
                catch (System.Exception ex)
                {
                    err = ex.Message;
                }

                if (success)
                {
                    RefreshList();
                }
                else
                {
                    CadWinFormMessageBox.ShowWarning(err, "批注流转提示", this);
                }
            }
        }

        private void ExecuteCommand(string cmd)
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            doc?.SendStringToExecute(cmd + "\n", true, false, false);
        }
    }
}
