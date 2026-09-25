using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace CadAutoCloudNote.Core.UI.Wpf
{
    public enum CadMessageBoxButtons
    {
        OK,
        OKCancel,
        YesNo,
        YesNoCancel
    }

    public enum CadMessageBoxIcon
    {
        None,
        Information,
        Question,
        Warning,
        Error
    }

    public enum CadDialogResult
    {
        None,
        OK,
        Cancel,
        Yes,
        No
    }

    /// <summary>
    /// AutoCAD 现代深色 Fluent 统一消息框
    /// 统一替代原生 Windows 白底蓝顶 MessageBox，保持 100% 科技感与深灰一致性
    /// </summary>
    public partial class CadMessageBox : Window
    {
        public CadDialogResult Result { get; private set; } = CadDialogResult.None;

        public CadMessageBox(string message, string title, CadMessageBoxButtons buttons, CadMessageBoxIcon icon)
        {
            InitializeComponent();
            ApplyAutoCadSmartTheme();

            TxtTitle.Text = string.IsNullOrEmpty(title) ? "提示" : title;
            TxtMessage.Text = message ?? string.Empty;

            SetupIcon(icon);
            SetupButtons(buttons);
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
                if (Autodesk.AutoCAD.ApplicationServices.Application.Version.Major < 20) isDarkTheme = false;
                else
                {
                    object themeVar = Autodesk.AutoCAD.ApplicationServices.Application.GetSystemVariable("COLORTHEME");
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
                this.Resources["WindowBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC"));
                this.Resources["TitleBarBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8EEF5"));
                this.Resources["BottomBarBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9"));
                this.Resources["TextPrimary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B"));
                this.Resources["TextSecondary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));
                this.Resources["BorderLine"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1"));
                this.Resources["ButtonBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0"));
                this.Resources["ButtonHover"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1"));
                this.Resources["ButtonPressed"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1"));
                this.Resources["AccentColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0070D2"));
                this.Resources["AccentHover"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#005FB2"));
                this.Resources["BadgeBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0F2FE"));
            }
        }

        private void SetupIcon(CadMessageBoxIcon icon)
        {
            switch (icon)
            {
                case CadMessageBoxIcon.Question:
                    IconBadge.Background = new SolidColorBrush(_isDarkTheme ? Color.FromRgb(0x1E, 0x3A, 0x5F) : (Color)ColorConverter.ConvertFromString("#E0F2FE"));
                    TxtIconSymbol.Text = "?";
                    TxtIconSymbol.Foreground = new SolidColorBrush(_isDarkTheme ? Color.FromRgb(0x00, 0x84, 0xFF) : (Color)ColorConverter.ConvertFromString("#0070D2"));
                    IconBadge.Visibility = Visibility.Visible;
                    break;
                case CadMessageBoxIcon.Warning:
                    IconBadge.Background = new SolidColorBrush(_isDarkTheme ? Color.FromRgb(0x3D, 0x2E, 0x14) : (Color)ColorConverter.ConvertFromString("#FEF3C7"));
                    TxtIconSymbol.Text = "!";
                    TxtIconSymbol.Foreground = new SolidColorBrush(_isDarkTheme ? Color.FromRgb(0xF5, 0x9E, 0x0B) : (Color)ColorConverter.ConvertFromString("#D97706"));
                    IconBadge.Visibility = Visibility.Visible;
                    break;
                case CadMessageBoxIcon.Error:
                    IconBadge.Background = new SolidColorBrush(_isDarkTheme ? Color.FromRgb(0x3B, 0x1C, 0x1D) : (Color)ColorConverter.ConvertFromString("#FEE2E2"));
                    TxtIconSymbol.Text = "✕";
                    TxtIconSymbol.Foreground = new SolidColorBrush(_isDarkTheme ? Color.FromRgb(0xEF, 0x44, 0x44) : (Color)ColorConverter.ConvertFromString("#DC2626"));
                    IconBadge.Visibility = Visibility.Visible;
                    break;
                case CadMessageBoxIcon.Information:
                    IconBadge.Background = new SolidColorBrush(_isDarkTheme ? Color.FromRgb(0x1E, 0x3A, 0x5F) : (Color)ColorConverter.ConvertFromString("#E0F2FE"));
                    TxtIconSymbol.Text = "i";
                    TxtIconSymbol.Foreground = new SolidColorBrush(_isDarkTheme ? Color.FromRgb(0x00, 0x84, 0xFF) : (Color)ColorConverter.ConvertFromString("#0070D2"));
                    IconBadge.Visibility = Visibility.Visible;
                    break;
                default:
                    IconBadge.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        private void SetupButtons(CadMessageBoxButtons buttons)
        {
            BtnOk.Visibility = Visibility.Collapsed;
            BtnCancel.Visibility = Visibility.Collapsed;
            BtnYes.Visibility = Visibility.Collapsed;
            BtnNo.Visibility = Visibility.Collapsed;

            switch (buttons)
            {
                case CadMessageBoxButtons.OK:
                    BtnOk.Visibility = Visibility.Visible;
                    break;
                case CadMessageBoxButtons.OKCancel:
                    BtnOk.Visibility = Visibility.Visible;
                    BtnCancel.Visibility = Visibility.Visible;
                    break;
                case CadMessageBoxButtons.YesNo:
                    BtnYes.Visibility = Visibility.Visible;
                    BtnNo.Visibility = Visibility.Visible;
                    break;
                case CadMessageBoxButtons.YesNoCancel:
                    BtnYes.Visibility = Visibility.Visible;
                    BtnNo.Visibility = Visibility.Visible;
                    BtnCancel.Visibility = Visibility.Visible;
                    break;
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Result = BtnCancel.Visibility == Visibility.Visible ? CadDialogResult.Cancel :
                     (BtnNo.Visibility == Visibility.Visible ? CadDialogResult.No : CadDialogResult.OK);
            DialogResult = false;
            Close();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            Result = CadDialogResult.OK;
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Result = CadDialogResult.Cancel;
            DialogResult = false;
            Close();
        }

        private void BtnYes_Click(object sender, RoutedEventArgs e)
        {
            Result = CadDialogResult.Yes;
            DialogResult = true;
            Close();
        }

        private void BtnNo_Click(object sender, RoutedEventArgs e)
        {
            Result = CadDialogResult.No;
            DialogResult = false;
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (BtnYes.Visibility == Visibility.Visible)
                {
                    BtnYes_Click(BtnYes, new RoutedEventArgs());
                }
                else if (BtnOk.Visibility == Visibility.Visible)
                {
                    BtnOk_Click(BtnOk, new RoutedEventArgs());
                }
            }
            else if (e.Key == Key.Escape)
            {
                BtnClose_Click(null, new RoutedEventArgs());
            }
        }

        #region 静态工厂调用方法

        public static CadDialogResult Show(string message, string title = "提示", CadMessageBoxButtons buttons = CadMessageBoxButtons.OK, CadMessageBoxIcon icon = CadMessageBoxIcon.Information, Window owner = null)
        {
            CadMessageBox box = new CadMessageBox(message, title, buttons, icon);
            if (owner != null && owner.IsVisible)
            {
                box.Owner = owner;
                box.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                box.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            box.ShowDialog();
            return box.Result;
        }

        public static bool Confirm(string message, string title = "确认操作", Window owner = null)
        {
            CadDialogResult res = Show(message, title, CadMessageBoxButtons.YesNo, CadMessageBoxIcon.Question, owner);
            return res == CadDialogResult.Yes;
        }

        public static void ShowInfo(string message, string title = "提示", Window owner = null)
        {
            Show(message, title, CadMessageBoxButtons.OK, CadMessageBoxIcon.Information, owner);
        }

        public static void ShowWarning(string message, string title = "警告", Window owner = null)
        {
            Show(message, title, CadMessageBoxButtons.OK, CadMessageBoxIcon.Warning, owner);
        }

        public static void ShowError(string message, string title = "错误", Window owner = null)
        {
            Show(message, title, CadMessageBoxButtons.OK, CadMessageBoxIcon.Error, owner);
        }

        #endregion
    }
}
