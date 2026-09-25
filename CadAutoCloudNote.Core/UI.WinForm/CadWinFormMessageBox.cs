using System;
using System.Drawing;
using System.Windows.Forms;

namespace CadAutoCloudNote.Core.UI.WinForm
{
    /// <summary>
    /// AutoCAD WinForms 统一深色 Fluent 消息提示对话框 (专供 CAD 2007~2012 及独立测试)
    /// 彻底杜绝刺眼的 Windows 原生白底蓝框 MessageBox
    /// </summary>
    public class CadWinFormMessageBox : Form
    {
        private readonly Panel titleBar;
        private readonly Label lblTitle;
        private readonly Button btnClose;
        private readonly Label lblIcon;
        private readonly Label lblMessage;
        private readonly Panel bottomPanel;
        private readonly Button btnYes;
        private readonly Button btnNo;
        private readonly Button btnOk;
        private readonly Button btnCancel;

        private bool dragging = false;
        private Point dragCursorPoint;
        private Point dragFormPoint;

        [System.Runtime.InteropServices.DllImport("gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(
            int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        public CadWinFormMessageBox(string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            bool isDark = IsCurrentAutoCadDarkTheme();

            Color winBg = isDark ? ColorTranslator.FromHtml("#1E2530") : ColorTranslator.FromHtml("#F8FAFC");
            Color foreText = isDark ? ColorTranslator.FromHtml("#F5F5F5") : ColorTranslator.FromHtml("#1E293B");
            Color titleBarBg = isDark ? ColorTranslator.FromHtml("#171D25") : ColorTranslator.FromHtml("#E8EEF5");
            Color closeBtnFg = isDark ? ColorTranslator.FromHtml("#A2B0C4") : ColorTranslator.FromHtml("#64748B");
            Color bottomBg = isDark ? ColorTranslator.FromHtml("#181F2A") : ColorTranslator.FromHtml("#F1F5F9");
            Color lineBorder = isDark ? ColorTranslator.FromHtml("#374354") : ColorTranslator.FromHtml("#CBD5E1");

            Color btnPrimaryBg = isDark ? ColorTranslator.FromHtml("#0084FF") : ColorTranslator.FromHtml("#0070D2");
            Color btnPrimaryFg = Color.White;
            Color btnSecondaryBg = isDark ? ColorTranslator.FromHtml("#2F3C4D") : ColorTranslator.FromHtml("#E2E8F0");
            Color btnSecondaryFg = isDark ? Color.White : ColorTranslator.FromHtml("#334155");

            string msg = message ?? string.Empty;
            int lineCount = msg.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
            int targetHeight = Math.Max(165, Math.Min(320, 115 + Math.Max(1, lineCount) * 18));

            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new Size(410, targetHeight);
            this.BackColor = winBg;
            this.ForeColor = foreText;
            this.Font = new Font("Microsoft YaHei", 9F, FontStyle.Regular);
            this.ShowInTaskbar = false;
            try
            {
                this.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, this.Width, this.Height, 16, 16));
            }
            catch { }

            // 标题栏
            titleBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 32,
                BackColor = titleBarBg
            };
            titleBar.MouseDown += TitleBar_MouseDown;
            titleBar.MouseMove += TitleBar_MouseMove;
            titleBar.MouseUp += TitleBar_MouseUp;

            lblTitle = new Label
            {
                Text = string.IsNullOrEmpty(title) ? "提示" : title,
                ForeColor = foreText,
                Font = new Font("Microsoft YaHei", 9F, FontStyle.Bold),
                Location = new Point(12, 7),
                AutoSize = true
            };
            lblTitle.MouseDown += TitleBar_MouseDown;
            lblTitle.MouseMove += TitleBar_MouseMove;
            lblTitle.MouseUp += TitleBar_MouseUp;

            btnClose = new Button
            {
                Text = "✕",
                Dock = DockStyle.Right,
                Width = 32,
                FlatStyle = FlatStyle.Flat,
                ForeColor = closeBtnFg,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = ColorTranslator.FromHtml("#E81123");
            btnClose.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            titleBar.Controls.Add(lblTitle);
            titleBar.Controls.Add(btnClose);

            // 底部按钮栏
            bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 46,
                BackColor = bottomBg
            };
            bottomPanel.Paint += (s, e) =>
            {
                using (Pen p = new Pen(lineBorder, 1))
                {
                    e.Graphics.DrawLine(p, 0, 0, bottomPanel.Width, 0);
                }
            };

            btnCancel = CreateButton("取消", btnSecondaryBg, btnSecondaryFg, DialogResult.Cancel);
            btnNo = CreateButton("否(N)", btnSecondaryBg, btnSecondaryFg, DialogResult.No);
            btnYes = CreateButton("是(Y)", btnPrimaryBg, btnPrimaryFg, DialogResult.Yes);
            btnOk = CreateButton("确定", btnPrimaryBg, btnPrimaryFg, DialogResult.OK);

            int right = this.Width - 14;
            if (buttons == MessageBoxButtons.OK)
            {
                PlaceButton(btnOk, ref right);
            }
            else if (buttons == MessageBoxButtons.OKCancel)
            {
                PlaceButton(btnCancel, ref right);
                PlaceButton(btnOk, ref right);
            }
            else if (buttons == MessageBoxButtons.YesNo)
            {
                PlaceButton(btnNo, ref right);
                PlaceButton(btnYes, ref right);
            }
            else if (buttons == MessageBoxButtons.YesNoCancel)
            {
                PlaceButton(btnCancel, ref right);
                PlaceButton(btnNo, ref right);
                PlaceButton(btnYes, ref right);
            }

            // 主体内容：现代圆角徽章与抗锯齿绘制
            Color badgeBg = isDark ? ColorTranslator.FromHtml("#1E3A5F") : ColorTranslator.FromHtml("#E0F2FE");
            Color badgeFg = isDark ? ColorTranslator.FromHtml("#0084FF") : ColorTranslator.FromHtml("#0284C7");
            string iconGlyph = "i";

            switch (icon)
            {
                case MessageBoxIcon.Question:
                    iconGlyph = "?";
                    badgeBg = isDark ? ColorTranslator.FromHtml("#1E3A5F") : ColorTranslator.FromHtml("#E0F2FE");
                    badgeFg = isDark ? ColorTranslator.FromHtml("#0084FF") : ColorTranslator.FromHtml("#0284C7");
                    break;
                case MessageBoxIcon.Warning:
                    iconGlyph = "!";
                    badgeBg = isDark ? ColorTranslator.FromHtml("#3D2E14") : ColorTranslator.FromHtml("#FEF3C7");
                    badgeFg = isDark ? ColorTranslator.FromHtml("#F59E0B") : ColorTranslator.FromHtml("#D97706");
                    break;
                case MessageBoxIcon.Error:
                    iconGlyph = "✕";
                    badgeBg = isDark ? ColorTranslator.FromHtml("#3B1C1D") : ColorTranslator.FromHtml("#FEE2E2");
                    badgeFg = isDark ? ColorTranslator.FromHtml("#EF4444") : ColorTranslator.FromHtml("#DC2626");
                    break;
                default:
                    iconGlyph = "i";
                    badgeBg = isDark ? ColorTranslator.FromHtml("#1E3A5F") : ColorTranslator.FromHtml("#E0F2FE");
                    badgeFg = isDark ? ColorTranslator.FromHtml("#0084FF") : ColorTranslator.FromHtml("#0284C7");
                    break;
            }

            lblIcon = new Label
            {
                Location = new Point(16, 44),
                Size = new Size(34, 34),
                BackColor = Color.Transparent
            };
            lblIcon.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                using (SolidBrush b = new SolidBrush(badgeBg))
                {
                    e.Graphics.FillEllipse(b, 2, 2, 29, 29);
                }
                using (Pen p = new Pen(badgeFg, 1.2f))
                {
                    e.Graphics.DrawEllipse(p, 2, 2, 29, 29);
                }
                using (SolidBrush tb = new SolidBrush(badgeFg))
                using (Font f = new Font("Microsoft YaHei", 12F, FontStyle.Bold))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    e.Graphics.DrawString(iconGlyph, f, tb, new RectangleF(2, 2, 29, 29), sf);
                }
            };

            lblMessage = new Label
            {
                Text = msg,
                Location = new Point(62, 42),
                Size = new Size(this.Width - 76, this.Height - 32 - 46 - 14),
                TextAlign = ContentAlignment.TopLeft,
                ForeColor = foreText,
                AutoEllipsis = true
            };

            this.Controls.Add(lblMessage);
            this.Controls.Add(lblIcon);
            this.Controls.Add(bottomPanel);
            this.Controls.Add(titleBar);

            // 绘制抗锯齿圆角外边框
            this.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (Pen p = new Pen(lineBorder, 1.5f))
                {
                    using (var path = GetRoundRectPath(new Rectangle(0, 0, this.Width - 1, this.Height - 1), 16))
                    {
                        e.Graphics.DrawPath(p, path);
                    }
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

        private static System.Drawing.Drawing2D.GraphicsPath GetRoundRectPath(Rectangle rect, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            int diameter = radius * 2;
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private Button CreateButton(string text, Color bg, Color fg, DialogResult res)
        {
            Button btn = new Button
            {
                Text = text,
                Size = new Size(72, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = bg,
                ForeColor = fg,
                Cursor = Cursors.Hand,
                DialogResult = res,
                Font = new Font("Microsoft YaHei", 8.5F)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += (s, e) => { this.DialogResult = res; this.Close(); };
            return btn;
        }

        private void PlaceButton(Button btn, ref int right)
        {
            right -= btn.Width;
            btn.Location = new Point(right, 9);
            bottomPanel.Controls.Add(btn);
            right -= 8;
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

        #region 静态工厂方法

        public static DialogResult Show(string message, string title = "提示", MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.Information, IWin32Window owner = null)
        {
            using (var dlg = new CadWinFormMessageBox(message, title, buttons, icon))
            {
                if (owner != null)
                {
                    dlg.StartPosition = FormStartPosition.CenterParent;
                    return dlg.ShowDialog(owner);
                }
                else
                {
                    dlg.StartPosition = FormStartPosition.CenterScreen;
                    return dlg.ShowDialog();
                }
            }
        }

        public static bool Confirm(string message, string title = "确认", IWin32Window owner = null)
        {
            return Show(message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question, owner) == DialogResult.Yes;
        }

        public static void ShowInfo(string message, string title = "提示", IWin32Window owner = null)
        {
            Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information, owner);
        }

        public static void ShowWarning(string message, string title = "警告", IWin32Window owner = null)
        {
            Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning, owner);
        }

        public static void ShowError(string message, string title = "错误", IWin32Window owner = null)
        {
            Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error, owner);
        }

        #endregion
    }
}
