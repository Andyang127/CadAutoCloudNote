using System;
using CadAutoCloudNote.Core.Models;
using CadAutoCloudNote.Core.Protocols;

namespace CadAutoCloudNote.Core.Interfaces
{
    /// <summary>
    /// UI 呈现抽象接口（解耦 WinForms / WPF / 命令行降级模式）
    /// </summary>
    public interface INoteUIProvider
    {
        bool ShowNoteEditDialog(NoteRecord note, bool isNew);
        bool ShowNoteReplyDialog(NoteRecord note, out string replyContent, out NoteLifecycleState targetState);
        bool ShowNoteReplyDialog(NoteRecord note, out string replyContent, out NoteLifecycleState targetState, out string replyAuthor);
        void ShowConfigDialog();
        void RefreshPalette();
        void ShowPalette();
        void HidePalette();
    }
}
