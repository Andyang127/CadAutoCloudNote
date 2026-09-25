using System;

namespace CadAutoCloudNote.Core.Interfaces
{
    /// <summary>
    /// 插件更新检查接口
    /// </summary>
    public interface IUpdateChecker
    {
        bool CheckForUpdate(out string newVersion, out string downloadUrl);
    }
}
