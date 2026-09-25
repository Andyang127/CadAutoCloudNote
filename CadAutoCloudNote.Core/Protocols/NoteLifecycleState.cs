using System;

namespace CadAutoCloudNote.Core.Protocols
{
    /// <summary>
    /// 批注生命周期状态
    /// </summary>
    public enum NoteLifecycleState
    {
        Draft = 0,      // 草稿
        Pending = 1,    // 待办 / 未修改
        Resolved = 2,   // 已改 / 待复核
        Rejected = 3,   // 驳回 / 存疑
        Closed = 4      // 已闭环 / 归档
    }

    /// <summary>
    /// 批注紧急程度 / 优先级 (一般、重要、紧急)
    /// </summary>
    public enum NotePriority
    {
        Normal = 0,     // 一般
        Important = 1,  // 重要
        Urgent = 2,     // 紧急

        // 向后兼容历史别名
        Low = 0,        // 兼容别名: 低 -> 一般
        High = 2,       // 兼容别名: 紧急
        Critical = 2    // 兼容别名: 致命 -> 紧急
    }

    public static class NoteStateHelper
    {
        public static string GetDisplayName(NoteLifecycleState state)
        {
            switch (state)
            {
                case NoteLifecycleState.Draft: return "草稿";
                case NoteLifecycleState.Pending: return "待办";
                case NoteLifecycleState.Resolved: return "已改";
                case NoteLifecycleState.Rejected: return "驳回";
                case NoteLifecycleState.Closed: return "已闭环";
                default: return state.ToString();
            }
        }

        public static int GetCadColorIndex(NoteLifecycleState state)
        {
            switch (state)
            {
                case NoteLifecycleState.Draft: return 8;     // 灰色
                case NoteLifecycleState.Pending: return 1;   // 红色 (待办紧迫)
                case NoteLifecycleState.Resolved: return 3;  // 绿色 (已修改待复核)
                case NoteLifecycleState.Rejected: return 6;  // 品红 (驳回)
                case NoteLifecycleState.Closed: return 8;    // 灰色 (已闭环)
                default: return 7;                          // 白色/默认
            }
        }

        public static string GetDisplayName(NotePriority priority)
        {
            switch (priority)
            {
                case NotePriority.Urgent: return "紧急";
                case NotePriority.Important:
                case NotePriority.Normal:
                default:
                    return "重要";
            }
        }

        public static bool CanTransition(NoteLifecycleState current, NoteLifecycleState target)
        {
            if (current == target) return true;
            switch (current)
            {
                case NoteLifecycleState.Draft:
                    return target == NoteLifecycleState.Pending || target == NoteLifecycleState.Closed;
                case NoteLifecycleState.Pending:
                    return target == NoteLifecycleState.Resolved || target == NoteLifecycleState.Rejected || target == NoteLifecycleState.Closed;
                case NoteLifecycleState.Resolved:
                    return target == NoteLifecycleState.Pending || target == NoteLifecycleState.Closed || target == NoteLifecycleState.Rejected;
                case NoteLifecycleState.Rejected:
                    return target == NoteLifecycleState.Pending || target == NoteLifecycleState.Resolved || target == NoteLifecycleState.Closed;
                case NoteLifecycleState.Closed:
                    return target == NoteLifecycleState.Pending || target == NoteLifecycleState.Resolved; // 允许重新打开为待办或已改待复核
                default:
                    return true;
            }
        }
    }

    public static class NoteLifecycleStateExtensions
    {
        public static string ToDisplayName(this NoteLifecycleState state) => NoteStateHelper.GetDisplayName(state);
        public static string ToDisplayName(this NotePriority priority) => NoteStateHelper.GetDisplayName(priority);
        public static short GetColorIndex(this NoteLifecycleState state) => (short)NoteStateHelper.GetCadColorIndex(state);
    }
}
