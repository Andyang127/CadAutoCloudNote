using System;
using CadAutoCloudNote.Core.Config;
using CadAutoCloudNote.Core.Interfaces;
using CadAutoCloudNote.Core.Models;
using CadAutoCloudNote.Core.Protocols;
using CadAutoCloudNote.Core.UI.WinForm;
#if USE_WPF
using CadAutoCloudNote.Core.UI.Wpf;
#endif

namespace CadAutoCloudNote.Core.UI
{
    /// <summary>
    /// 全版本 UI 调度器实现（无缝桥接 WPF 与 WinForms）
    /// </summary>
    public class NoteUIProvider : INoteUIProvider
    {
        private static readonly NoteUIProvider _instance = new NoteUIProvider();
        public static NoteUIProvider Instance => _instance;

        public void ShowConfigDialog()
        {
            try
            {
#if USE_WPF
                var window = new ConfigWindow();
                try
                {
                    Autodesk.AutoCAD.ApplicationServices.Application.ShowModalWindow(window);
                }
                catch
                {
                    window.ShowDialog();
                }
#else
                using (var form = new ConfigForm())
                {
                    try
                    {
                        Autodesk.AutoCAD.ApplicationServices.Application.ShowModalDialog(form);
                    }
                    catch
                    {
                        form.ShowDialog();
                    }
                }
#endif
            }
            catch (Exception ex)
            {
#if USE_WPF
                CadAutoCloudNote.Core.UI.Wpf.CadMessageBox.ShowError("呼出设置窗口失败: " + ex.Message, "错误");
#else
                CadAutoCloudNote.Core.UI.WinForm.CadWinFormMessageBox.ShowError("呼出设置窗口失败: " + ex.Message, "错误");
#endif
            }
        }

        public bool ShowNoteEditDialog(NoteRecord note, bool isNew)
        {
            if (note == null) return false;
            var s = ConfigManager.Instance.CurrentSettings;

#if USE_WPF
            var dlg = new NoteEditDialog(note.Title, note.Content, note.Discipline ?? s.DefaultDiscipline, note.Assignee ?? s.DefaultAssignee, note.Priority, note.SequenceNumber > 0 ? note.SequenceNumber : 1, s.AutoNumberPrefix);
            bool? res;
            try
            {
                res = Autodesk.AutoCAD.ApplicationServices.Application.ShowModalWindow(dlg);
            }
            catch
            {
                res = dlg.ShowDialog();
            }

            if (res == true)
            {
                note.Title = dlg.NoteTitle;
                note.Content = dlg.NoteContent;
                note.Discipline = dlg.Discipline;
                note.Priority = dlg.Priority;
                note.Assignee = dlg.Assignee;
                note.SequenceNumber = dlg.SequenceNumber;
                return true;
            }
            return false;
#else
            using (var form = new NoteEditForm(note.Title, note.Content, note.Discipline ?? s.DefaultDiscipline, note.Assignee ?? s.DefaultAssignee, note.Priority, note.SequenceNumber > 0 ? note.SequenceNumber : 1, s.AutoNumberPrefix))
            {
                System.Windows.Forms.DialogResult res;
                try
                {
                    res = Autodesk.AutoCAD.ApplicationServices.Application.ShowModalDialog(form);
                }
                catch
                {
                    res = form.ShowDialog();
                }

                if (res == System.Windows.Forms.DialogResult.OK)
                {
                    note.Title = form.NoteTitle;
                    note.Content = form.NoteContent;
                    note.Discipline = form.Discipline;
                    note.Priority = form.Priority;
                    note.Assignee = form.Assignee;
                    note.SequenceNumber = form.SequenceNumber;
                    return true;
                }
                return false;
            }
#endif
        }

        public bool ShowNoteReplyDialog(NoteRecord note, out string replyContent, out NoteLifecycleState targetState)
        {
            string replyAuthor;
            return ShowNoteReplyDialog(note, out replyContent, out targetState, out replyAuthor);
        }

        public bool ShowNoteReplyDialog(NoteRecord note, out string replyContent, out NoteLifecycleState targetState, out string replyAuthor)
        {
            replyContent = string.Empty;
            targetState = NoteLifecycleState.Resolved;
            replyAuthor = Environment.UserName;
            if (note == null) return false;

#if USE_WPF
            var dlg = new NoteReplyDialog(note);
            bool? res;
            try
            {
                res = Autodesk.AutoCAD.ApplicationServices.Application.ShowModalWindow(dlg);
            }
            catch
            {
                res = dlg.ShowDialog();
            }

            if (res == true)
            {
                replyContent = dlg.ReplyContent;
                targetState = dlg.TargetState;
                replyAuthor = dlg.ReplyAuthor;
                return true;
            }
            return false;
#else
            using (var form = new NoteReplyForm(note))
            {
                System.Windows.Forms.DialogResult res;
                try
                {
                    res = Autodesk.AutoCAD.ApplicationServices.Application.ShowModalDialog(form);
                }
                catch
                {
                    res = form.ShowDialog();
                }

                if (res == System.Windows.Forms.DialogResult.OK)
                {
                    replyContent = form.ReplyContent;
                    targetState = form.TargetState;
                    replyAuthor = form.ReplyAuthor;
                    return true;
                }
                return false;
            }
#endif
        }

        public void ShowPalette() => PaletteManager.ShowPalette();
        public void RefreshPalette() => PaletteManager.Refresh();
        public void HidePalette() { }
    }
}
