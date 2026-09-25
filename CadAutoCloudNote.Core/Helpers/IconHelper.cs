using System;
using System.IO;
using System.Reflection;
using System.Drawing;

namespace CadAutoCloudNote.Core.Helpers
{
    /// <summary>
    /// 插件全链路图标加载助手：为 AutoCAD PaletteSet 看板、WinForms 窗体与 WPF 对话框提供统一高保真图标挂载
    /// </summary>
    public static class IconHelper
    {
        private static string _lastLoadedPath = null;
        private static DateTime _lastLoadedTime = DateTime.MinValue;
        private static Icon _cachedIcon = null;

        /// <summary>
        /// 动态读取并加载最新应用图标（优先加载磁盘上最新修改的 logo，实时响应换标）
        /// </summary>
        public static Icon GetAppIcon()
        {
            try
            {
                // 1. 探测当前运行 DLL 所在物理目录及相关目录 (支持安装路径、工程开发路径与 Bundle 规范)
                string dllPath = string.Empty;
                try { dllPath = Assembly.GetExecutingAssembly().Location; } catch { }
                string dir = !string.IsNullOrEmpty(dllPath) ? Path.GetDirectoryName(dllPath) : AppDomain.CurrentDomain.BaseDirectory;

                var candidates = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrEmpty(dir))
                {
                    candidates.Add(Path.Combine(dir, "icon.ico"));
                    candidates.Add(CombinePath(dir, "Resources", "icon.ico"));
                    candidates.Add(CombinePath(dir, "..", "icon.ico"));
                    candidates.Add(CombinePath(dir, "..", "Contents", "icon.ico"));
                    candidates.Add(CombinePath(dir, "..", "installer", "icon.ico"));
                    candidates.Add(CombinePath(dir, "..", "CADAutoCloudNote.bundle", "icon.ico"));
                    candidates.Add(CombinePath(dir, "..", "CADAutoCloudNote.bundle", "Contents", "icon.ico"));
                    candidates.Add(CombinePath(dir, "..", "CadAutoCloudNote.Core", "Resources", "icon.ico"));
                    candidates.Add(CombinePath(dir, "..", "..", "icon.ico"));
                    candidates.Add(CombinePath(dir, "..", "..", "installer", "icon.ico"));
                    candidates.Add(CombinePath(dir, "..", "..", "CADAutoCloudNote.bundle", "icon.ico"));
                    candidates.Add(CombinePath(dir, "..", "..", "CADAutoCloudNote.bundle", "Contents", "icon.ico"));
                    candidates.Add(CombinePath(dir, "..", "..", "CadAutoCloudNote.Core", "Resources", "icon.ico"));
                }

                // 挑选存在的路径中最新修改的图标
                string bestPath = null;
                DateTime bestTime = DateTime.MinValue;
                foreach (string p in candidates)
                {
                    try
                    {
                        string fullPath = Path.GetFullPath(p);
                        if (File.Exists(fullPath))
                        {
                            DateTime wt = File.GetLastWriteTimeUtc(fullPath);
                            if (wt > bestTime)
                            {
                                bestTime = wt;
                                bestPath = fullPath;
                            }
                        }
                    }
                    catch { }
                }

                if (!string.IsNullOrEmpty(bestPath))
                {
                    // 若检测到磁盘文件有更新或初次加载，重新读取最新
                    if (_cachedIcon == null || bestPath != _lastLoadedPath || bestTime > _lastLoadedTime)
                    {
                        try
                        {
                            _cachedIcon = new Icon(bestPath);
                            _lastLoadedPath = bestPath;
                            _lastLoadedTime = bestTime;
                        }
                        catch { }
                    }
                    if (_cachedIcon != null) return _cachedIcon;
                }

                if (_cachedIcon != null) return _cachedIcon;

                // 2. 备用：从当前程序集嵌入资源中提取
                Assembly asm = Assembly.GetExecutingAssembly();
                string[] resNames = asm.GetManifestResourceNames();
                if (resNames != null)
                {
                    foreach (string name in resNames)
                    {
                        if (name.EndsWith("icon.ico", StringComparison.OrdinalIgnoreCase))
                        {
                            using (Stream stream = asm.GetManifestResourceStream(name))
                            {
                                if (stream != null)
                                {
                                    _cachedIcon = new Icon(stream);
                                    return _cachedIcon;
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return _cachedIcon;
        }

#if USE_WPF
        public static System.Windows.Media.ImageSource GetAppImageSource()
        {
            try
            {
                Icon icon = GetAppIcon();
                if (icon != null)
                {
                    return System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle,
                        System.Windows.Int32Rect.Empty,
                        System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                }
            }
            catch { }
            return null;
        }
#endif

        private static string CombinePath(params string[] parts)
        {
            if (parts == null || parts.Length == 0) return string.Empty;
            string result = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                result = Path.Combine(result, parts[i]);
            }
            return result;
        }
    }
}
