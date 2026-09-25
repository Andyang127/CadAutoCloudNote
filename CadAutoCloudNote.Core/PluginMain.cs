using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: ExtensionApplication(typeof(CadAutoCloudNote.Core.PluginMain))]

namespace CadAutoCloudNote.Core
{
    public class PluginMain : IExtensionApplication
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool SetDllDirectory(string lpPathName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr LoadLibrary(string lpFileName);

        public static bool IsShuttingDown { get; private set; } = false;

        public void Initialize()
        {
            try
            {
                string pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
                string depsDir = Path.Combine(pluginDir, "deps");
                string x64Dir = Path.Combine(pluginDir, "x64");

                // 1. 优先注入 Win32 原生库搜索路径（onnxruntime.dll 等）
                if (Directory.Exists(x64Dir))
                {
                    SetDllDirectory(x64Dir);
                }
                else if (Directory.Exists(depsDir))
                {
                    SetDllDirectory(depsDir);
                }

                // 2. 挂载托管程序集解析探针
                AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

                // 3. 启动反应器管理器
                CadAutoCloudNote.Core.Reactors.ReactorManager.Instance.Initialize();

                // 4. 打印启动问候信息
                Document doc = Application.DocumentManager.MdiActiveDocument;
                doc?.Editor.WriteMessage($"\n[{AppConstants.PluginFullName}] v{AppConstants.Version} 载入成功！输入 CLOUDNOTE 开始批注，NOTEPANEL 打开面板。\n");
            }
            catch (System.Exception ex)
            {
                try
                {
                    Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage($"\n[{AppConstants.PluginShortName}] 初始化异常: {ex.Message}\n");
                }
                catch { }
            }
        }

        public void Terminate()
        {
            IsShuttingDown = true;
            try
            {
                CadAutoCloudNote.Core.Reactors.ReactorManager.Instance.Terminate();
            }
            catch { }
            AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;
        }

        private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                string assemblyName = new AssemblyName(args.Name).Name;
                string pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";

                var candidates = new System.Collections.Generic.List<string>
                {
                    CombinePaths(pluginDir, assemblyName + ".dll"),
                    CombinePaths(pluginDir, "deps", assemblyName + ".dll"),
                    CombinePaths(pluginDir, "..", "Shared", assemblyName + ".dll")
                };

                foreach (var path in candidates)
                {
                    if (File.Exists(path))
                    {
                        return Assembly.LoadFrom(path);
                    }
                }
            }
            catch { }
            return null;
        }

        private static string CombinePaths(params string[] paths)
        {
            if (paths == null || paths.Length == 0) return string.Empty;
            string current = paths[0];
            for (int i = 1; i < paths.Length; i++)
            {
                current = Path.Combine(current, paths[i]);
            }
            return current;
        }
    }
}
