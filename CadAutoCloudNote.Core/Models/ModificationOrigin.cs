using System;

namespace CadAutoCloudNote.Core.Models
{
    /// <summary>
    /// 批注修改来源
    /// </summary>
    public enum ModificationOrigin
    {
        User = 0,           // 用户直接操作
        Reactor = 1,        // 反应器自动感知图元变化
        BatchMerge = 2,     // 批量多图合并
        PackageImport = 3,  // 审查包导入
        CompareSync = 4,    // 版本对比联动对齐
        Migration = 5       // 升级迁移
    }
}
