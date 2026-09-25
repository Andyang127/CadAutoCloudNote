using System;
using System.Collections.Generic;

namespace CadAutoCloudNote.Core.Reactors
{
    /// <summary>
    /// 待处理事件冲突消解矩阵
    /// </summary>
    public static class PendingTaskConflictResolver
    {
        public static List<PendingTask> Resolve(List<PendingTask> incomingTasks)
        {
            if (incomingTasks == null || incomingTasks.Count <= 1)
                return incomingTasks ?? new List<PendingTask>();

            Dictionary<string, PendingTask> resolvedMap = new Dictionary<string, PendingTask>();
            HashSet<string> droppedHandles = new HashSet<string>();

            foreach (var task in incomingTasks)
            {
                string key = task.HandleString ?? task.TargetId.ToString();

                if (droppedHandles.Contains(key))
                    continue;

                if (!resolvedMap.ContainsKey(key))
                {
                    resolvedMap[key] = task;
                }
                else
                {
                    var existing = resolvedMap[key];

                    // 1. 若先前是 Appended，后续被 Erased -> 产生后即被销毁，直接从队列消除
                    if (existing.TaskType == PendingTaskType.Appended && task.TaskType == PendingTaskType.Erased)
                    {
                        resolvedMap.Remove(key);
                        droppedHandles.Add(key);
                        continue;
                    }

                    // 2. 任意状态后被 Erased -> 以 Erased 终结状态为准
                    if (task.TaskType == PendingTaskType.Erased)
                    {
                        resolvedMap[key] = task;
                        continue;
                    }

                    // 3. 连续多次 Modified -> 保留最新修改时间戳
                    if (task.TaskType == PendingTaskType.Modified)
                    {
                        resolvedMap[key] = task;
                    }
                }
            }

            return new List<PendingTask>(resolvedMap.Values);
        }
    }
}
