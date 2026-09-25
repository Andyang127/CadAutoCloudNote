using System;

namespace CadAutoCloudNote.Core.Protocols
{
    /// <summary>
    /// XRecord 存储容量与自动分卷策略
    /// </summary>
    public static class CapacityPolicy
    {
        // 根字典名称
        public const string RootDictionaryName = "CAD_NOTE_ROOT_DICT";

        // 单卷默认容纳的批注条数（到达此阈值自动创建新卷字典）
        public const int MaxNotesPerVolume = 50;
        public const int MAX_NOTES_PER_VOLUME = MaxNotesPerVolume;

        // 默认分卷名称前缀，如 CAD_NOTE_DICT_V001, CAD_NOTE_DICT_V002
        public const string VolumePrefix = "CAD_NOTE_DICT_V";

        // 单个 XRecord 字节容量安全上限 (CAD XRecord 理论上限约 2MB，设定 128KB 保障数据安全与平稳反序列化性能)
        public const int SafeXRecordByteLimit = 128 * 1024;

        // 基础分片名称后缀
        public const string SliceMain = "_MAIN";
        public const string SliceClouds = "_CLOUDS";
        public const string SliceText = "_TEXT";
        public const string SliceReplies = "_REPLIES";

        // 扩展分片名称后缀
        public const string SliceAudit = "_AUDIT";
        public const string SliceAi = "_AI";

        public static string GetVolumeKey(int volumeIndex)
        {
            return VolumePrefix + volumeIndex.ToString("D3");
        }
    }
}
