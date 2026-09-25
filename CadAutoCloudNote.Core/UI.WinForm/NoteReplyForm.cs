using System;
using System.Drawing;
using System.Windows.Forms;
using CadAutoCloudNote.Core.Models;
using CadAutoCloudNote.Core.Protocols;
using CadAutoCloudNote.Core.Services;

namespace CadAutoCloudNote.Core.UI.WinForm
{
    /// <summary>
    /// 批注协同回复与状态流转对话框（WinForms Fluent 深色主题实现，专供低版本 CAD 2007~2012）
    /// 彻底取代系统原生白底丑陋弹窗，杜绝标题与文字截断
    /// </summary>
    public class NoteReplyForm : Form
    {
        private readonly Panel titleBar;
        private readonly Label lblTitle;
        private readonly Button btnClose;
        private readonly ComboBox cmbTargetState;
        private readonly TextBox txtReplyAuthor;
        private readonly TextBox txtReplyContent;
        private readonly Button btnSubmit;
        private readonly Button btnCancel;

        private bool dragging = false;
        private Point dragCursorPoint;
        private Point dragFormPoint;

        [System.Runtime.InteropServices.DllImport("gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(
            int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        public string ReplyContent => txtReplyContent.Text.Trim();
        public string ReplyAuthor => (txtReplyAuthor == null || string.IsNullOrEmpty(txtReplyAuthor.Text) || string.IsNullOrEmpty(txtReplyAuthor.Text.Trim())) ? Environment.UserName : txtReplyAuthor.Text.Trim();
        public NoteLifecycleState TargetState
        {
            get
            {
                switch (cmbTargetState.SelectedIndex)
                {
                    case 0: return NoteLifecycleState.Resolved;
                    case 1: return NoteLifecycleState.Closed;
                    case 2: return NoteLifecycleState.Pending;
                    case 3: return NoteLifecycleState.Rejected;
                    default: return NoteLifecycleState.Resolved;
                }
            }
        }

        public NoteReplyForm(NoteRecord note)
        {
            bool isDark = IsCurrentAutoCadDarkTheme();

            Color winBg = isDark ? ColorTranslator.FromHtml("#1E2530") : ColorTranslator.FromHtml("#F8FAFC");
            Color cardBg = isDark ? ColorTranslator.FromHtml("#252E3B") : ColorTranslator.FromHtml("#FFFFFF");
            Color titleBarBg = isDark ? ColorTranslator.FromHtml("#171D25") : ColorTranslator.FromHtml("#E8EEF5");
            Color textPrimary = isDark ? ColorTranslator.FromHtml("#F5F5F5") : ColorTranslator.FromHtml("#1E293B");
            Color textSecondary = isDark ? ColorTranslator.FromHtml("#A2B0C4") : ColorTranslator.FromHtml("#64748B");
            Color borderLine = isDark ? ColorTranslator.FromHtml("#374354") : ColorTranslator.FromHtml("#CBD5E1");
            Color inputBg = isDark ? ColorTranslator.FromHtml("#13181F") : ColorTranslator.FromHtml("#FFFFFF");
            Color itemBoxBg = isDark ? ColorTranslator.FromHtml("#13181F") : ColorTranslator.FromHtml("#F1F5F9");
            Color flowFg = isDark ? ColorTranslator.FromHtml("#38BDF8") : ColorTranslator.FromHtml("#0284C7");
            Color btnCancelBg = isDark ? ColorTranslator.FromHtml("#2F3C4D") : ColorTranslator.FromHtml("#E2E8F0");
            Color btnCancelFg = isDark ? Color.White : ColorTranslator.FromHtml("#334155");
            Color btnSubmitBg = isDark ? ColorTranslator.FromHtml("#0084FF") : ColorTranslator.FromHtml("#0070D2");

            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new Size(460, 500);
            this.BackColor = winBg;
            this.ForeColor = textPrimary;
            this.Font = new Font("Microsoft YaHei", 9F, FontStyle.Regular);
            this.ShowInTaskbar = false;
            try
            {
                this.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, this.Width, this.Height, 16, 16));
            }
            catch { }

            // 1. 顶部标题栏
            titleBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 34,
                BackColor = titleBarBg
            };
            titleBar.MouseDown += TitleBar_MouseDown;
            titleBar.MouseMove += TitleBar_MouseMove;
            titleBar.MouseUp += TitleBar_MouseUp;

            lblTitle = new Label
            {
                Text = "批注协同回复与状态流转",
                ForeColor = textPrimary,
                Font = new Font("Microsoft YaHei", 9F, FontStyle.Bold),
                Location = new Point(14, 8),
                AutoSize = true
            };
            lblTitle.MouseDown += TitleBar_MouseDown;
            lblTitle.MouseMove += TitleBar_MouseMove;
            lblTitle.MouseUp += TitleBar_MouseUp;

            btnClose = new Button
            {
                Text = "✕",
                Font = new Font("Microsoft YaHei", 10F, FontStyle.Bold),
                ForeColor = textSecondary,
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(36, 34),
                Location = new Point(this.Width - 36, 0),
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(232, 17, 35);
            btnClose.FlatAppearance.MouseDownBackColor = Color.FromArgb(196, 16, 30);
            btnClose.MouseEnter += (s, e) => btnClose.ForeColor = Color.White;
            btnClose.MouseLeave += (s, e) => btnClose.ForeColor = textSecondary;
            btnClose.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            titleBar.Controls.Add(lblTitle);
            titleBar.Controls.Add(btnClose);
            this.Controls.Add(titleBar);

            // 2. 中间卡片容器
            Panel cardPanel = new Panel
            {
                Location = new Point(16, 44),
                Size = new Size(428, 152),
                BackColor = cardBg
            };
            cardPanel.Paint += (s, e) =>
            {
                using (Pen p = new Pen(borderLine, 1))
                {
                    e.Graphics.DrawRectangle(p, 0, 0, cardPanel.Width - 1, cardPanel.Height - 1);
                }
            };

            string prefix = Config.ConfigManager.Instance.CurrentSettings.AutoNumberPrefix;
            string seqTag = note != null ? NoteService.FormatSequencePrefix(prefix, note.SequenceNumber) : "#1";
            string cleanTitle = note != null && !string.IsNullOrEmpty(note.Title) ? note.Title : "未命名批注";
            if (cleanTitle.StartsWith("【") && cleanTitle.Contains("】"))
            {
                int cIdx = cleanTitle.IndexOf('】');
                cleanTitle = cleanTitle.Substring(cIdx + 1).Trim();
            }
            string noteHeader = string.Format("{0} {1}", seqTag, cleanTitle);

            Label lblNoteHeader = new Label
            {
                Text = noteHeader,
                Font = new Font("Microsoft YaHei", 9.5F, FontStyle.Bold),
                ForeColor = textPrimary,
                Location = new Point(10, 8),
                Size = new Size(408, 20),
                AutoEllipsis = true
            };

            string metaStr = note != null 
                ? string.Format("专业: {0} | 批注者: {1}", 
                    note.Discipline ?? "通用", 
                    note.Assignee ?? note.CreatedBy ?? Environment.UserName)
                : string.Empty;
            Label lblNoteMeta = new Label
            {
                Text = metaStr,
                Font = new Font("Microsoft YaHei", 8F, FontStyle.Regular),
                ForeColor = textSecondary,
                Location = new Point(10, 30),
                Size = new Size(408, 18),
                AutoEllipsis = true
            };

            Label lblContentTitle = new Label
            {
                Text = "审查意见详情:",
                Font = new Font("Microsoft YaHei", 8F, FontStyle.Regular),
                ForeColor = textSecondary,
                Location = new Point(10, 50),
                Size = new Size(408, 16)
            };

            Label lblNoteContent = new Label
            {
                Text = note != null && !string.IsNullOrEmpty(note.Content) ? note.Content : "（无详细审查意见文本）",
                Font = new Font("Microsoft YaHei", 8.5F, FontStyle.Regular),
                ForeColor = textPrimary,
                BackColor = itemBoxBg,
                Location = new Point(10, 68),
                Size = new Size(408, 30),
                AutoEllipsis = true
            };

            Label lblFlowTitle = new Label
            {
                Text = "批注流转说明 (历史记录):",
                Font = new Font("Microsoft YaHei", 8F, FontStyle.Regular),
                ForeColor = textSecondary,
                Location = new Point(10, 102),
                Size = new Size(408, 16)
            };

            string latestFlow = null;
            if (note?.Replies != null && note.Replies.Count > 0)
            {
                var lastReply = note.Replies[note.Replies.Count - 1];
                latestFlow = string.Format("[{0}] {1} (回复人): {2} ({3:yyyy-MM-dd HH:mm})", 
                    NoteLifecycleStateExtensions.ToDisplayName(lastReply.StateTransitionTo),
                    lastReply.Author ?? "协同人",
                    lastReply.Content ?? "已流转",
                    lastReply.Timestamp);
            }
            else if (note?.AuditLogs != null && note.AuditLogs.Count > 0)
            {
                var lastAudit = note.AuditLogs[note.AuditLogs.Count - 1];
                latestFlow = string.Format("[{0}] {1}: {2} ({3:yyyy-MM-dd HH:mm})", 
                    lastAudit.Action ?? "变更",
                    lastAudit.Operator ?? "系统",
                    lastAudit.FieldChanges ?? "状态变更",
                    lastAudit.Timestamp);
            }

            Label lblLatestWorkflow = new Label
            {
                Text = !string.IsNullOrEmpty(latestFlow) ? latestFlow : "（首次流转，暂无历史说明）",
                Font = new Font("Microsoft YaHei", 8.5F, FontStyle.Regular),
                ForeColor = flowFg,
                BackColor = itemBoxBg,
                Location = new Point(10, 120),
                Size = new Size(408, 22),
                AutoEllipsis = true
            };

            cardPanel.Controls.Add(lblNoteHeader);
            cardPanel.Controls.Add(lblNoteMeta);
            cardPanel.Controls.Add(lblContentTitle);
            cardPanel.Controls.Add(lblNoteContent);
            cardPanel.Controls.Add(lblFlowTitle);
            cardPanel.Controls.Add(lblLatestWorkflow);
            this.Controls.Add(cardPanel);

            // 3. 状态下拉
            Label lblState = new Label
            {
                Text = "流转状态变更至:",
                ForeColor = textSecondary,
                Location = new Point(16, 206),
                AutoSize = true
            };
            this.Controls.Add(lblState);

            cmbTargetState = new ComboBox
            {
                Location = new Point(125, 202),
                Size = new Size(319, 26),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = inputBg,
                ForeColor = textPrimary,
                FlatStyle = FlatStyle.Flat
            };
            cmbTargetState.Items.AddRange(new object[]
            {
                "已改 (Resolved) - 问题已解决待复核",
                "已闭环 (Closed) - 复核通过归档",
                "待办 (Pending) - 重新打开或保持待处理",
                "驳回 (Rejected) - 意见存疑或设计驳回"
            });
            if (note != null)
            {
                if (note.State == NoteLifecycleState.Pending)
                {
                    cmbTargetState.SelectedIndex = 0; // 待办 -> 建议已改
                }
                else if (note.State == NoteLifecycleState.Resolved)
                {
                    cmbTargetState.SelectedIndex = 1; // 已改 -> 建议已闭环
                }
                else if (note.State == NoteLifecycleState.Rejected)
                {
                    cmbTargetState.SelectedIndex = 0; // 驳回 -> 建议已改
                }
                else if (note.State == NoteLifecycleState.Closed)
                {
                    cmbTargetState.SelectedIndex = 2; // 已闭环 -> 建议重新打开为待办
                }
                else
                {
                    cmbTargetState.SelectedIndex = 0;
                }
            }
            else
            {
                cmbTargetState.SelectedIndex = 0;
            }
            this.Controls.Add(cmbTargetState);

            // 4. 回复人 (使用者)
            Label lblAuthor = new Label
            {
                Text = "回复人 (使用者):",
                ForeColor = textSecondary,
                Location = new Point(16, 236),
                AutoSize = true
            };
            this.Controls.Add(lblAuthor);

            string rememberedAuthor = Config.ConfigManager.Instance.CurrentSettings?.LastReplyAuthor;
            if (string.IsNullOrEmpty(rememberedAuthor) || string.IsNullOrEmpty(rememberedAuthor.Trim()))
            {
                rememberedAuthor = Config.ConfigManager.Instance.CurrentSettings?.DefaultAssignee;
            }
            if (string.IsNullOrEmpty(rememberedAuthor) || string.IsNullOrEmpty(rememberedAuthor.Trim()))
            {
                rememberedAuthor = Environment.UserName;
            }

            txtReplyAuthor = new TextBox
            {
                Location = new Point(125, 232),
                Size = new Size(319, 24),
                BackColor = inputBg,
                ForeColor = textPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Text = rememberedAuthor
            };
            this.Controls.Add(txtReplyAuthor);

            // 5. 回复意见输入
            Label lblReply = new Label
            {
                Text = "本次流转说明 / 修改回复:",
                ForeColor = textSecondary,
                Location = new Point(16, 266),
                AutoSize = true
            };
            this.Controls.Add(lblReply);

            txtReplyContent = new TextBox
            {
                Location = new Point(16, 288),
                Size = new Size(428, 148),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = inputBg,
                ForeColor = textPrimary,
                BorderStyle = BorderStyle.FixedSingle
            };
            this.Controls.Add(txtReplyContent);

            // 6. 底部按钮栏
            Panel bottomBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                BackColor = titleBarBg
            };
            bottomBar.Paint += (s, e) =>
            {
                using (Pen p = new Pen(borderLine, 1))
                {
                    e.Graphics.DrawLine(p, 0, 0, bottomBar.Width, 0);
                }
            };

            btnCancel = new Button
            {
                Text = "取消",
                DialogResult = DialogResult.Cancel,
                Size = new Size(72, 28),
                Location = new Point(272, 10),
                BackColor = btnCancelBg,
                ForeColor = btnCancelFg,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            btnSubmit = new Button
            {
                Text = "提交流转",
                Size = new Size(88, 28),
                Location = new Point(354, 10),
                BackColor = btnSubmitBg,
                ForeColor = Color.White,
                Font = new Font("Microsoft YaHei", 9F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnSubmit.FlatAppearance.BorderSize = 0;
            btnSubmit.Click += (s, e) =>
            {
                if (note != null)
                {
                    string err;
                    if (!ValidationService.ValidateStateTransition(note.State, TargetState, out err))
                    {
                        CadWinFormMessageBox.ShowWarning(err, "状态流转提示", this);
                        return;
                    }
                }

                // 记忆当前回复人 (使用者)
                try
                {
                    string author = ReplyAuthor;
                    if (!string.IsNullOrEmpty(author) && Config.ConfigManager.Instance.CurrentSettings != null)
                    {
                        Config.ConfigManager.Instance.CurrentSettings.LastReplyAuthor = author;
                        Config.ConfigManager.Instance.Save();
                    }
                }
                catch { }

                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            bottomBar.Controls.Add(btnCancel);
            bottomBar.Controls.Add(btnSubmit);
            this.Controls.Add(bottomBar);

            // 边框绘制
            this.Paint += (s, e) =>
            {
                using (Pen p = new Pen(borderLine, 1))
                {
                    e.Graphics.DrawRectangle(p, 0, 0, this.Width - 1, this.Height - 1);
                }
            };
        }

        private static bool IsCurrentAutoCadDarkTheme()
        {
            try
            {
#if CAD_R17 || CAD_R18 || CAD_R19
                return false;
#elif !CAD_TEST
                if (Autodesk.AutoCAD.ApplicationServices.Application.Version.Major < 20) return false;
                object themeVar = Autodesk.AutoCAD.ApplicationServices.Application.GetSystemVariable("COLORTHEME");
                if (themeVar != null && Convert.ToInt16(themeVar) == 0) return true;
                return false;
#endif
            }
            catch { }
            return false;
        }

        private void TitleBar_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                dragging = true;
                dragCursorPoint = Cursor.Position;
                dragFormPoint = this.Location;
            }
        }

        private void TitleBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (dragging)
            {
                Point diff = Point.Subtract(Cursor.Position, new Size(dragCursorPoint));
                this.Location = Point.Add(dragFormPoint, new Size(diff));
            }
        }

        private void TitleBar_MouseUp(object sender, MouseEventArgs e)
        {
            dragging = false;
        }
    }
}
