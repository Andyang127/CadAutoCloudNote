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

namespace CadAutoCloudNote.Core.UI.Wpf
{
    public partial class NoteReplyDialog : Window
    {
        private readonly NoteRecord _note;

        public string ReplyContent => TxtReplyContent.Text.Trim();
        public string ReplyAuthor => (TxtReplyAuthor == null || string.IsNullOrEmpty(TxtReplyAuthor.Text) || string.IsNullOrEmpty(TxtReplyAuthor.Text.Trim())) ? Environment.UserName : TxtReplyAuthor.Text.Trim();
        public NoteLifecycleState TargetState
        {
            get
            {
                switch (CmbTargetState.SelectedIndex)
                {
                    case 0: return NoteLifecycleState.Resolved;
                    case 1: return NoteLifecycleState.Closed;
                    case 2: return NoteLifecycleState.Pending;
                    case 3: return NoteLifecycleState.Rejected;
                    default: return NoteLifecycleState.Resolved;
                }
            }
        }

        public NoteReplyDialog(NoteRecord note)
        {
            InitializeComponent();
            ApplyAutoCadSmartTheme();
            _note = note;
            string rememberedAuthor = Config.ConfigManager.Instance.CurrentSettings?.LastReplyAuthor;
            if (string.IsNullOrEmpty(rememberedAuthor) || string.IsNullOrEmpty(rememberedAuthor.Trim()))
            {
                rememberedAuthor = Config.ConfigManager.Instance.CurrentSettings?.DefaultAssignee;
            }
            if (string.IsNullOrEmpty(rememberedAuthor) || string.IsNullOrEmpty(rememberedAuthor.Trim()))
            {
                rememberedAuthor = Environment.UserName;
            }
            TxtReplyAuthor.Text = rememberedAuthor;

            try
            {
                var iconSource = IconHelper.GetAppImageSource();
                if (iconSource != null) this.Icon = iconSource;
            }
            catch { }

            if (note != null)
            {
                string pfx = Config.ConfigManager.Instance.CurrentSettings.AutoNumberPrefix;
                string formattedSeq = NoteService.FormatSequencePrefix(pfx, note.SequenceNumber);
                string cleanTitle = !string.IsNullOrEmpty(note.Title) ? note.Title : "未命名批注";
                if (cleanTitle.StartsWith("【") && cleanTitle.Contains("】"))
                {
                    int cIdx = cleanTitle.IndexOf('】');
                    cleanTitle = cleanTitle.Substring(cIdx + 1).Trim();
                }
                TxtNoteTitle.Text = string.Format("{0} {1}", formattedSeq, cleanTitle);
                TxtNoteMeta.Text = string.Format("专业: {0} | 批注者: {1}", 
                    note.Discipline ?? "通用", 
                    note.Assignee ?? note.CreatedBy ?? Environment.UserName);

                TxtNoteContent.Text = !string.IsNullOrEmpty(note.Content) ? note.Content : "（无详细审查意见文本）";

                string latestFlow = null;
                if (note.Replies != null && note.Replies.Count > 0)
                {
                    var lastReply = note.Replies[note.Replies.Count - 1];
                    latestFlow = string.Format("[{0}] {1} (回复人): {2} ({3:yyyy-MM-dd HH:mm})", 
                        NoteLifecycleStateExtensions.ToDisplayName(lastReply.StateTransitionTo),
                        lastReply.Author ?? "协同人",
                        lastReply.Content ?? "已流转",
                        lastReply.Timestamp);
                }
                else if (note.AuditLogs != null && note.AuditLogs.Count > 0)
                {
                    var lastAudit = note.AuditLogs[note.AuditLogs.Count - 1];
                    latestFlow = string.Format("[{0}] {1}: {2} ({3:yyyy-MM-dd HH:mm})", 
                        lastAudit.Action ?? "变更",
                        lastAudit.Operator ?? "系统",
                        lastAudit.FieldChanges ?? "状态变更",
                        lastAudit.Timestamp);
                }
                TxtLatestWorkflow.Text = !string.IsNullOrEmpty(latestFlow) ? latestFlow : "（首次流转，暂无历史说明）";

                UpdateCurrentStateBadge(note.State);

                // 默认智能建议流转目标状态
                if (note.State == NoteLifecycleState.Pending)
                {
                    CmbTargetState.SelectedIndex = 0; // 待办 -> 已改
                }
                else if (note.State == NoteLifecycleState.Resolved)
                {
                    CmbTargetState.SelectedIndex = 1; // 已改 -> 已闭环
                }
                else if (note.State == NoteLifecycleState.Rejected)
                {
                    CmbTargetState.SelectedIndex = 0; // 驳回 -> 已改
                }
                else
                {
                    CmbTargetState.SelectedIndex = 2; // 已闭环 -> 重新打开为待办
                }
            }
        }

        private void UpdateCurrentStateBadge(NoteLifecycleState state)
        {
            TxtCurrentState.Text = NoteLifecycleStateExtensions.ToDisplayName(state);
            switch (state)
            {
                case NoteLifecycleState.Pending:
                    BadgeCurrentState.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626")); // 红
                    TxtCurrentState.Foreground = Brushes.White;
                    break;
                case NoteLifecycleState.Resolved:
                    BadgeCurrentState.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16A34A")); // 绿
                    TxtCurrentState.Foreground = Brushes.White;
                    break;
                case NoteLifecycleState.Rejected:
                    BadgeCurrentState.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9333EA")); // 品红
                    TxtCurrentState.Foreground = Brushes.White;
                    break;
                case NoteLifecycleState.Closed:
                default:
                    BadgeCurrentState.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#475569")); // 灰
                    TxtCurrentState.Foreground = Brushes.White;
                    break;
            }
        }

        private void CmbTargetState_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

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
                this.Resources["FlowText"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0284C7"));
                this.Resources["BadgeBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0F2FE"));
            }
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void BtnSubmit_Click(object sender, RoutedEventArgs e)
        {
            if (_note != null)
            {
                string err;
                if (!ValidationService.ValidateStateTransition(_note.State, TargetState, out err))
                {
                    CadMessageBox.ShowWarning(err, "状态流转提示", this);
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
