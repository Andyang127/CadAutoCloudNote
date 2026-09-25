using System;
using CadAutoCloudNote.Core.Interfaces;

namespace CadAutoCloudNote.Core.Services
{
    /// <summary>
    /// 插件更新检查服务
    /// </summary>
    public class UpdateChecker : IUpdateChecker
    {
        private static readonly UpdateChecker _instance = new UpdateChecker();
        public static UpdateChecker Instance => _instance;

        public bool CheckForUpdate(out string newVersion, out string downloadUrl)
        {
            newVersion = AppConstants.Version;
            downloadUrl = string.Empty;
            // 离线安全或当前已为最新版本
            return false;
        }
    }
}
