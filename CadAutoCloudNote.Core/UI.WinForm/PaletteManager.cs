using System;
using Autodesk.AutoCAD.Windows;
using CadAutoCloudNote.Core.Helpers;

namespace CadAutoCloudNote.Core.UI.WinForm
{
    /// <summary>
    /// 停靠看板单例管理器
    /// </summary>
    public static class PaletteManager
    {
        private static PaletteSet _paletteSet;
        private static NotePaletteControl _control;

        public static void ShowPalette()
        {
            if (_paletteSet == null)
            {
                _paletteSet = new PaletteSet("NOTEPANEL", new Guid("98F1D2E3-A4B5-4C6D-8E9F-0A1B2C3D4E5F"));
                try { _paletteSet.Name = "云线批注看板"; } catch { }
                _paletteSet.MinimumSize = new System.Drawing.Size(320, 400);
                _paletteSet.Size = new System.Drawing.Size(360, 520);
                try
                {
                    System.Drawing.Icon appIcon = IconHelper.GetAppIcon();
                    if (appIcon != null)
                    {
                        _paletteSet.Icon = appIcon;
                    }
                }
                catch { }

                _control = new NotePaletteControl();
                _paletteSet.Add("批注列表", _control);
            }
            _paletteSet.Visible = true;
            _control.RefreshList();
        }

        public static void Refresh()
        {
            if (_paletteSet != null && _paletteSet.Visible)
            {
                _control?.RefreshList();
            }
        }
    }
}
