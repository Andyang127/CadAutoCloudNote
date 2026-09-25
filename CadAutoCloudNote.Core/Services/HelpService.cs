using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
#if CAD_R17 || CAD_R18
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
#elif !CAD_TEST
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
#endif

namespace CadAutoCloudNote.Core.Services
{
    /// <summary>
    /// 帮助与官方手册导引服务
    /// </summary>
    public static class HelpService
    {
        public const string OnlineRepoUrl = "https://github.com/Andyang127/CadAutoCloudNote";

        /// <summary>
        /// 智能定位并打开 Readme.html 官方使用手册
        /// </summary>
        public static void OpenReadme()
        {
            string readmePath = FindReadmePath();

            try
            {
#if !CAD_TEST
                try
                {
                    var doc = AcApp.DocumentManager?.MdiActiveDocument;
                    if (doc?.Editor != null)
                    {
                        doc.Editor.WriteMessage(string.Format("\n[云线批注] 正在打开用户手册: {0}\n", 
                            !string.IsNullOrEmpty(readmePath) ? readmePath : OnlineRepoUrl));
                    }
                }
                catch { }
#endif

                if (!string.IsNullOrEmpty(readmePath) && File.Exists(readmePath))
                {
                    ProcessStartInfo psi = new ProcessStartInfo(readmePath);
                    psi.UseShellExecute = true;
                    Process.Start(psi);
                }
                else
                {
                    // 本地文件未找到时降级访问在线开源项目主页
                    ProcessStartInfo psi = new ProcessStartInfo(OnlineRepoUrl);
                    psi.UseShellExecute = true;
                    Process.Start(psi);
                }
            }
            catch (Exception ex)
            {
#if !CAD_TEST
                try
                {
                    var doc = AcApp.DocumentManager?.MdiActiveDocument;
                    if (doc?.Editor != null)
                    {
                        doc.Editor.WriteMessage(string.Format("\n[云线批注] 启动使用手册失败: {0}\n", ex.Message));
                    }
                }
                catch { }
#endif
            }
        }

        /// <summary>
        /// 探测本地 Readme.html 所在物理路径
        /// </summary>
        public static string FindReadmePath()
        {
            try
            {
                string assemblyLoc = null;
                try
                {
                    assemblyLoc = typeof(HelpService).Assembly.Location;
                }
                catch { }

                if (!string.IsNullOrEmpty(assemblyLoc))
                {
                    string dir = Path.GetDirectoryName(assemblyLoc);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        // 1. 同级目录 (如直接拷贝或便携部署)
                        string path1 = Path.Combine(dir, "Readme.html");
                        if (File.Exists(path1)) return Path.GetFullPath(path1);

                        // 2. 上级目录 (如安装路径 {app}\R24\..\Readme.html 即 {app}\Readme.html)
                        string path2 = Path.Combine(dir, @"..\Readme.html");
                        if (File.Exists(path2)) return Path.GetFullPath(path2);

                        // 3. 开发环境源码路径 (build\R24\..\..\installer\Readme.html)
                        string path3 = Path.Combine(dir, @"..\..\installer\Readme.html");
                        if (File.Exists(path3)) return Path.GetFullPath(path3);

                        string path4 = Path.Combine(dir, @"..\..\..\installer\Readme.html");
                        if (File.Exists(path4)) return Path.GetFullPath(path4);
                    }
                }

                // 4. AppData 漫游目录
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string path5 = Path.Combine(appData, @"InkVerse\CadAutoCloudNote\Readme.html");
                if (File.Exists(path5)) return Path.GetFullPath(path5);

                // 5. 默认安装 Program Files 目录
                string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string path6 = Path.Combine(pf, @"InkVerse\CadAutoCloudNote\Readme.html");
                if (File.Exists(path6)) return Path.GetFullPath(path6);

                string pfX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
                if (!string.IsNullOrEmpty(pfX86))
                {
                    string path7 = Path.Combine(pfX86, @"InkVerse\CadAutoCloudNote\Readme.html");
                    if (File.Exists(path7)) return Path.GetFullPath(path7);
                }
            }
            catch { }

            return null;
        }
    }
}
