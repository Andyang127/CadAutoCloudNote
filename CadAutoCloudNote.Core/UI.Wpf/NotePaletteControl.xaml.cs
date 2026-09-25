#if USE_WPF
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Autodesk.AutoCAD.ApplicationServices;
#if CAD_R17 || CAD_R18
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
#else
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
#endif
using Autodesk.AutoCAD.DatabaseServices;
using CadAutoCloudNote.Core.Helpers;
using CadAutoCloudNote.Core.Models;
using CadAutoCloudNote.Core.Protocols;
using CadAutoCloudNote.Core.Services;

namespace CadAutoCloudNote.Core.UI.Wpf
{
    public class NoteViewModel
    {
        public NoteRecord Model { get; set; }
        public int SequenceNumber => Model.SequenceNumber;
        public string Title => Model.Title;
        public string DisplayTitle
        {
            get
            {
                if (SequenceNumber <= 0) return Model.Title;
                string prefix = Config.ConfigManager.Instance.CurrentSettings.AutoNumberPrefix;
                string formattedSeq = NoteService.FormatSequencePrefix(prefix, SequenceNumber);
                string cleanTitle = Model.Title ?? string.Empty;
                if (cleanTitle.StartsWith("【") && cleanTitle.Contains("】"))
                {
                    int cIdx = cleanTitle.IndexOf('】');
                    cleanTitle = cleanTitle.Substring(cIdx + 1).Trim();
                }
                return string.Format("{0} {1}", formattedSeq, cleanTitle);
            }
        }
        public string Discipline => Model.Discipline;
        public string StateName => NoteLifecycleStateExtensions.ToDisplayName(Model.State);
        public string Assignee => Model.Assignee ?? Model.CreatedBy;

        public string TooltipText
        {
            get
            {
                string latestReplyInfo = "暂无流转答复";
                if (Model?.Replies != null && Model.Replies.Count > 0)
                {
                    var r = Model.Replies[Model.Replies.Count - 1];
                    latestReplyInfo = string.Format("[{0}] {1}: {2} ({3:MM-dd HH:mm})",
                        NoteLifecycleStateExtensions.ToDisplayName(r.StateTransitionTo),
                        !string.IsNullOrEmpty(r.Author) ? r.Author : "协同人",
                        !string.IsNullOrEmpty(r.Content) ? r.Content : "(无说明)",
                        r.Timestamp);
                }
                else if (Model?.AuditLogs != null && Model.AuditLogs.Count > 0)
                {
                    for (int k = Model.AuditLogs.Count - 1; k >= 0; k--)
                    {
                        var log = Model.AuditLogs[k];
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

                return string.Format(
                    "【批注】{0}\n" +
                    "【专业】{1}  【批注者】{2}  【状态】{3}\n" +
                    "【审查意见】{4}\n" +
                    "─────────────────────────────────\n" +
                    "【最新答复】{5}",
                    DisplayTitle,
                    Discipline ?? "通用",
                    Assignee ?? "未指定",
                    StateName,
                    !string.IsNullOrEmpty(Model?.Content) ? Model.Content : "(无详细意见)",
                    latestReplyInfo);
            }
        }

        public NoteViewModel(NoteRecord model)
        {
            Model = model;
        }
    }

    /// <summary>
    /// 现代化 WPF 侧边栏看板控件（支持 AutoCAD COLORTHEME 双色自适应、多字段检索与状态胶囊交互）
    /// </summary>
    public partial class NotePaletteControl : UserControl
    {
        private bool _isDarkTheme = false;

        public NotePaletteControl()
        {
            InitializeComponent();

            CmbStatus.Items.Add("全部状态");
            CmbStatus.Items.Add("待办");
            CmbStatus.Items.Add("已改");
            CmbStatus.Items.Add("驳回");
            CmbStatus.Items.Add("已闭环");
            CmbStatus.SelectedIndex = 0;

            var allTemplates = ProjectTemplateService.GetAllTemplates();
            CmbTargetPersona.ItemsSource = allTemplates;
            var curTpl = allTemplates.Find(t => t.Id == Config.ConfigManager.Instance.CurrentSettings.CurrentTemplateId)
                         ?? ProjectTemplateService.CurrentTemplate
                         ?? allTemplates[0];
            CmbTargetPersona.SelectedItem = curTpl;

            ApplyAutoCadSmartTheme();
            this.Loaded += (s, e) => ApplyAutoCadSmartTheme();
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
                this.Resources["WindowBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1F22"));
                this.Resources["HeaderBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2B2D30"));
                this.Resources["CardBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2B2D30"));
                this.Resources["BorderLine"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3E4147"));
                this.Resources["TextPrimary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0"));
                this.Resources["TextSecondary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));
                this.Resources["TextMuted"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));
                this.Resources["InputBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A1B1E"));
                this.Resources["ButtonBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#33363D"));
                this.Resources["ButtonHover"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#424650"));
                this.Resources["AccentColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#38BDF8"));
                this.Resources["ReplyBubbleBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#132A38"));
                this.Resources["ReplyBubbleBorder"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E465D"));
                this.Resources["FlowText"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#38BDF8"));
            }
            else
            {
                this.Resources["WindowBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC"));
                this.Resources["HeaderBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
                this.Resources["CardBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
                this.Resources["BorderLine"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0"));
                this.Resources["TextPrimary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B"));
                this.Resources["TextSecondary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));
                this.Resources["TextMuted"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));
                this.Resources["InputBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
                this.Resources["ButtonBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9"));
                this.Resources["ButtonHover"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0"));
                this.Resources["AccentColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0284C7"));
                this.Resources["ReplyBubbleBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0FDFA"));
                this.Resources["ReplyBubbleBorder"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCFBF1"));
                this.Resources["FlowText"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F766E"));
            }
        }

        private void CmbTargetPersona_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbTargetPersona.SelectedItem is AnnotationTemplate tpl)
            {
                var settings = Config.ConfigManager.Instance.CurrentSettings;
                ProjectTemplateService.ApplyTemplateToSettings(tpl.Id, settings);
            }
        }

        public void RefreshList()
        {
            try
            {
                ApplyAutoCadSmartTheme();

                Document doc = AcApp.DocumentManager.MdiActiveDocument;
                if (doc?.Database == null) return;

                List<NoteRecord> notes = NoteService.Instance.GetAllNotes(doc.Database);
                notes.Sort((a, b) => a.SequenceNumber.CompareTo(b.SequenceNumber));

                // 统计各状态数量
                int countPending = 0;
                int countResolved = 0;
                int countRejected = 0;
                int countClosed = 0;
                int countAliveTotal = 0;

                string kw = TxtSearch.Text != null ? TxtSearch.Text.Trim() : string.Empty;
                int filterStateIdx = CmbStatus.SelectedIndex;

                List<NoteViewModel> list = new List<NoteViewModel>();
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

                    countAliveTotal++;
                    switch (n.State)
                    {
                        case NoteLifecycleState.Pending: countPending++; break;
                        case NoteLifecycleState.Resolved: countResolved++; break;
                        case NoteLifecycleState.Rejected: countRejected++; break;
                        case NoteLifecycleState.Closed: countClosed++; break;
                    }

                    // 状态过滤筛选
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

                        // 关联答复与审计全文字段检索
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

                    list.Add(new NoteViewModel(n));
                }

                LvNotes.ItemsSource = list;

                // 更新胶囊统计数值
                if (TxtCountAll != null) TxtCountAll.Text = "全部 " + countAliveTotal;
                if (TxtCountPending != null) TxtCountPending.Text = "待办 " + countPending;
                if (TxtCountResolved != null) TxtCountResolved.Text = "已改 " + countResolved;
                if (TxtCountRejected != null) TxtCountRejected.Text = "驳回 " + countRejected;
                if (TxtCountClosed != null) TxtCountClosed.Text = "闭环 " + countClosed;

                // 更新胶囊选中高亮状态
                UpdatePillActiveVisual(filterStateIdx);

                // 更新匹配与总数提示
                if (TxtMatchCount != null)
                {
                    if (string.IsNullOrEmpty(kw) && filterStateIdx == 0)
                    {
                        TxtMatchCount.Text = string.Format("共 {0} 条批注", countAliveTotal);
                    }
                    else
                    {
                        TxtMatchCount.Text = string.Format("匹配 {0} / 共 {1} 条", list.Count, countAliveTotal);
                    }
                }

                LvNotes_SelectionChanged(null, null);
            }
            catch { }
        }

        private void UpdatePillActiveVisual(int filterStateIdx)
        {
            Border[] pills = { PillAll, PillPending, PillResolved, PillRejected, PillClosed };
            for (int i = 0; i < pills.Length; i++)
            {
                if (pills[i] == null) continue;
                bool isActive = (i == filterStateIdx);
                pills[i].BorderThickness = isActive ? new Thickness(2) : new Thickness(1);
                pills[i].Opacity = (filterStateIdx == 0 || isActive) ? 1.0 : 0.65;
            }
        }

        private void PillFilter_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag != null)
            {
                if (int.TryParse(border.Tag.ToString(), out int tagVal))
                {
                    int targetIdx = tagVal + 1; // -1 -> 0, 0 -> 1, 1 -> 2, 2 -> 3, 3 -> 4
                    if (CmbStatus.SelectedIndex == targetIdx && targetIdx != 0)
                    {
                        // 再次点击同一胶囊切回全部
                        CmbStatus.SelectedIndex = 0;
                    }
                    else
                    {
                        CmbStatus.SelectedIndex = targetIdx;
                    }
                }
            }
        }

        private void LvNotes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TxtDetailTitle == null) return;
            if (LvNotes.SelectedItem is NoteViewModel vm && vm.Model != null)
            {
                TxtDetailTitle.Text = string.Format("{0} [{1}] · {2} · 批注者: {3}",
                    vm.DisplayTitle,
                    vm.StateName,
                    vm.Discipline ?? "通用",
                    vm.Assignee ?? "未指定");
                TxtDetailContent.Text = "审查意见: " + (!string.IsNullOrEmpty(vm.Model.Content) ? vm.Model.Content : "(无详细意见)");

                string replyStr = "最新答复: -";
                if (vm.Model.Replies != null && vm.Model.Replies.Count > 0)
                {
                    var r = vm.Model.Replies[vm.Model.Replies.Count - 1];
                    replyStr = string.Format("最新答复: [{0}] {1}: {2} ({3:MM-dd HH:mm})",
                        NoteLifecycleStateExtensions.ToDisplayName(r.StateTransitionTo),
                        !string.IsNullOrEmpty(r.Author) ? r.Author : "协同人",
                        !string.IsNullOrEmpty(r.Content) ? r.Content : "(无说明)",
                        r.Timestamp);
                }
                else if (vm.Model.AuditLogs != null && vm.Model.AuditLogs.Count > 0)
                {
                    for (int k = vm.Model.AuditLogs.Count - 1; k >= 0; k--)
                    {
                        var log = vm.Model.AuditLogs[k];
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
                TxtDetailReply.Text = replyStr;

                if (BtnQuickZoom != null) BtnQuickZoom.IsEnabled = true;
                if (BtnQuickReply != null) BtnQuickReply.IsEnabled = true;
            }
            else
            {
                TxtDetailTitle.Text = "批注详情与流转答复预览";
                TxtDetailContent.Text = "在列表中选中批注可实时查看完整审查意见与最新流转答复。\n支持双击列表项自动平移定位图纸。";
                TxtDetailReply.Text = "最新答复: -";

                if (BtnQuickZoom != null) BtnQuickZoom.IsEnabled = false;
                if (BtnQuickReply != null) BtnQuickReply.IsEnabled = false;
            }
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            bool hasText = !string.IsNullOrEmpty(TxtSearch.Text);
            if (TxtSearchWatermark != null)
            {
                TxtSearchWatermark.Visibility = hasText ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
            }
            if (BtnClearSearch != null)
            {
                BtnClearSearch.Visibility = hasText ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            }
            RefreshList();
        }

        private void TxtSearch_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                TxtSearch.Text = string.Empty;
                e.Handled = true;
            }
        }

        private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Text = string.Empty;
            TxtSearch.Focus();
        }

        private void CmbStatus_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshList();
        private void BtnRefresh_Click(object sender, RoutedEventArgs e) => RefreshList();
        private void BtnAdd_Click(object sender, RoutedEventArgs e) => SendCmd("CLOUDNOTE");
        private void BtnConvert_Click(object sender, RoutedEventArgs e) => SendCmd("CNCONVERT");
        private void BtnTable_Click(object sender, RoutedEventArgs e) => SendCmd("NOTETABLE");
        private void BtnExport_Click(object sender, RoutedEventArgs e) => SendCmd("NOTEEXPORT");
        private void BtnSettings_Click(object sender, RoutedEventArgs e) => NoteUIProvider.Instance.ShowConfigDialog();
        private void BtnHelp_Click(object sender, RoutedEventArgs e) => HelpService.OpenReadme();

        private void BtnQuickZoom_Click(object sender, RoutedEventArgs e) => LvNotes_MouseDoubleClick(sender, null);

        private void BtnReply_Click(object sender, RoutedEventArgs e)
        {
            var vm = LvNotes.SelectedItem as NoteViewModel;
            if (vm == null)
            {
                CadMessageBox.ShowInfo("请先在列表中选中一条批注！", "提示");
                return;
            }

            SendCmd("NOTEREPLY");
        }

        private void LvNotes_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var vm = LvNotes.SelectedItem as NoteViewModel;
            if (vm?.Model?.Cloud == null) return;

            var c = vm.Model.Cloud;
            if (c.MinPoint.X == 0 && c.MaxPoint.X == 0) return;

            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            double margin = Math.Max(c.ArcLength * 3.0, 50.0);
            string cmd = string.Format("._ZOOM _W {0:0.###},{1:0.###} {2:0.###},{3:0.###}\n",
                c.MinPoint.X - margin,
                c.MinPoint.Y - margin,
                c.MaxPoint.X + margin,
                c.MaxPoint.Y + margin);
            doc.SendStringToExecute(cmd, true, false, false);
        }

        private void LvNotes_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            try
            {
                if (ColTitle != null && LvNotes.ActualWidth > 0)
                {
                    double fixedW = 50 + 55 + 80 + 25; // other columns (专业50+状态55+批注者80) + scrollbar margin
                    double avail = LvNotes.ActualWidth - fixedW;
                    ColTitle.Width = Math.Max(60, avail);
                }
            }
            catch { }
        }

        private void SendCmd(string cmd)
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            doc?.SendStringToExecute(cmd + "\n", true, false, false);
        }
    }
}
#endif
