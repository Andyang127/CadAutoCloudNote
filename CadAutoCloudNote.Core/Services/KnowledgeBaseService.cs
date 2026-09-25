using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CadAutoCloudNote.Core.Config;

namespace CadAutoCloudNote.Core.Services
{
    /// <summary>
    /// 知识库与规范条目实体
    /// </summary>
    public class KnowledgeItem
    {
        public string Id { get; set; }
        public string Discipline { get; set; }
        public string Title { get; set; }
        public string Suggestion { get; set; }
        public string StandardCode { get; set; }
        public bool IsCustom { get; set; }
        public int UsageCount { get; set; }

        public KnowledgeItem()
        {
            IsCustom = false;
            UsageCount = 0;
        }

        public KnowledgeItem(string id, string discipline, string title, string suggestion, string standardCode, bool isCustom = false, int usageCount = 0)
        {
            Id = id;
            Discipline = discipline;
            Title = title;
            Suggestion = suggestion;
            StandardCode = standardCode;
            IsCustom = isCustom;
            UsageCount = usageCount;
        }

        public string SourceBadge => IsCustom ? "自定义" : "国标";
        public string SourceBadgeBg => IsCustom ? "#2E2415" : "#1B3552";
        public string SourceBadgeFg => IsCustom ? "#F59E0B" : "#38BDF8";
        public string SourceBadgeBorder => IsCustom ? "#78350F" : "#1D4ED8";
        public string DisplayStandardCode => (string.IsNullOrEmpty(StandardCode) || StandardCode == "手动录入") ? "--" : StandardCode;
    }

    /// <summary>
    /// 工程规范与常用审查意见知识库服务 (支持现行国家规范、CRUD 与本地持久化)
    /// </summary>
    public static class KnowledgeBaseService
    {
        private static readonly List<KnowledgeItem> _items = new List<KnowledgeItem>();
        private static readonly object _lock = new object();

        public static string KnowledgeFilePath => Path.Combine(ConfigManager.ConfigDirectory, "KnowledgeBase.json");

        static KnowledgeBaseService()
        {
            LoadFromFile();
            if (_items.Count < 300)
            {
                ResetToDefaults();
            }
        }

        /// <summary>
        /// 获取所有规范条目副本
        /// </summary>
        public static List<KnowledgeItem> GetAll()
        {
            lock (_lock)
            {
                return new List<KnowledgeItem>(_items);
            }
        }

        /// <summary>
        /// 按 ID 获取条目
        /// </summary>
        public static KnowledgeItem GetById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            lock (_lock)
            {
                foreach (var item in _items)
                {
                    if (string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase))
                        return item;
                }
            }
            return null;
        }

        /// <summary>
        /// 检索知识库条目 (支持关键词、专业与来源类型过滤)
        /// </summary>
        public static List<KnowledgeItem> Search(string keyword, string discipline = null, string source = "全部")
        {
            List<KnowledgeItem> results = new List<KnowledgeItem>();
            string kw = keyword != null ? keyword.Trim() : string.Empty;

            lock (_lock)
            {
                foreach (var item in _items)
                {
                    // 1. 来源过滤
                    if (!string.IsNullOrEmpty(source) && !source.StartsWith("全部", StringComparison.OrdinalIgnoreCase))
                    {
                        if (source.Contains("常用") || source.Contains("自定义"))
                        {
                            if (!item.IsCustom) continue;
                        }
                        else if (source.Contains("规范") || source.Contains("系统") || source.Contains("国标"))
                        {
                            if (item.IsCustom) continue;
                        }
                    }

                    // 2. 专业过滤
                    if (!string.IsNullOrEmpty(discipline) && !discipline.Equals("全部", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.Equals(item.Discipline, discipline, StringComparison.OrdinalIgnoreCase))
                            continue;
                    }

                    // 3. 关键词过滤
                    if (string.IsNullOrEmpty(kw) ||
                        (item.Title != null && item.Title.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (item.Suggestion != null && item.Suggestion.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (item.StandardCode != null && item.StandardCode.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        results.Add(item);
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// 获取组织好的条目列表（用户常用高频置顶、系统规范随后）
        /// </summary>
        public static List<KnowledgeItem> GetOrganizedList()
        {
            lock (_lock)
            {
                var userItems = new List<KnowledgeItem>();
                var systemItems = new List<KnowledgeItem>();

                foreach (var item in _items)
                {
                    if (item.IsCustom) userItems.Add(item);
                    else systemItems.Add(item);
                }

                // 用户常用条目按使用频次降序排列
                userItems.Sort(delegate (KnowledgeItem a, KnowledgeItem b)
                {
                    return b.UsageCount.CompareTo(a.UsageCount);
                });

                var result = new List<KnowledgeItem>();
                result.AddRange(userItems);
                result.AddRange(systemItems);
                return result;
            }
        }

        /// <summary>
        /// 保存或更新用户手动录入的常用审查意见条目
        /// </summary>
        public static KnowledgeItem SaveUserCustomItem(string title, string discipline, string suggestion, string standardCode = "")
        {
            if (string.IsNullOrEmpty(title)) return null;
            string disc = string.IsNullOrEmpty(discipline) ? "通用" : discipline.Trim();
            string std = standardCode != null ? standardCode.Trim() : string.Empty;
            if (std == "手动录入") std = string.Empty;
            string sugg = suggestion != null ? suggestion.Trim() : string.Empty;
            string trimTitle = title.Trim();

            // 若无规范编号，智能尝试从意见正文提取国家现行标准代号
            if (string.IsNullOrEmpty(std) && !string.IsNullOrEmpty(sugg))
            {
                try
                {
                    var match = System.Text.RegularExpressions.Regex.Match(sugg, @"(?:GB|JGJ|GB/T|JG|CJJ|CECS)\s*[\d\.\-]+(?:\s*第?[\d\.]+条?)?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        std = match.Value.Trim();
                    }
                }
                catch { }
            }

            lock (_lock)
            {
                // 查重：优先全库匹配（含系统内置与自定义条目，严格区分系统自带与手动录入）
                KnowledgeItem existing = null;
                foreach (var it in _items)
                {
                    if (string.Equals(it.Title, trimTitle, StringComparison.OrdinalIgnoreCase) &&
                        (string.Equals(it.Discipline, disc, StringComparison.OrdinalIgnoreCase) || disc == "通用" || it.Discipline == "通用"))
                    {
                        existing = it;
                        break;
                    }
                }

                if (existing != null)
                {
                    // 若命中系统内置条目，绝不创建 USR 自定义冗余项，仅自增频次并保留系统原生属性；自定义条目则同步更新内容
                    if (existing.IsCustom)
                    {
                        existing.Suggestion = sugg;
                        if (!string.IsNullOrEmpty(std)) existing.StandardCode = std;
                    }
                    existing.UsageCount++;
                    SaveToFile();
                    return existing;
                }

                // 全新自主录入条目：计算新 ID USR01, USR02... 并标记 IsCustom = true
                int maxUserNum = 0;
                foreach (var it in _items)
                {
                    if (it.Id != null && it.Id.StartsWith("USR", StringComparison.OrdinalIgnoreCase))
                    {
                        int num;
                        if (int.TryParse(it.Id.Substring(3), out num))
                        {
                            if (num > maxUserNum) maxUserNum = num;
                        }
                    }
                }

                string newId = "USR" + (maxUserNum + 1).ToString("D2");
                var newItem = new KnowledgeItem(newId, disc, trimTitle, sugg, std, true, 1);
                _items.Insert(0, newItem);
                SaveToFile();
                return newItem;
            }
        }

        /// <summary>
        /// 递增某条目的调用频次并保存
        /// </summary>
        public static void IncrementUsage(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            lock (_lock)
            {
                foreach (var item in _items)
                {
                    if (string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase))
                    {
                        item.UsageCount++;
                        SaveToFile();
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 新增或更新条目
        /// </summary>
        public static void AddOrUpdate(KnowledgeItem item)
        {
            if (item == null) return;
            lock (_lock)
            {
                if (string.IsNullOrEmpty(item.Id))
                {
                    item.Id = "KB" + (_items.Count + 1).ToString("D2");
                }

                int foundIdx = -1;
                for (int i = 0; i < _items.Count; i++)
                {
                    if (string.Equals(_items[i].Id, item.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        foundIdx = i;
                        break;
                    }
                }

                if (foundIdx >= 0)
                {
                    _items[foundIdx] = item;
                }
                else
                {
                    _items.Add(item);
                }
                SaveToFile();
            }
        }

        /// <summary>
        /// 删除条目
        /// </summary>
        public static bool Delete(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            lock (_lock)
            {
                int removed = _items.RemoveAll(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
                if (removed > 0)
                {
                    SaveToFile();
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 重置恢复内置的现行国家工程规范库条目 (保留用户自定义常用意见)
        /// </summary>
        public static void ResetToDefaults()
        {
            lock (_lock)
            {
                // 1. 保留用户已保存的自定义常用条目
                var userCustomItems = new List<KnowledgeItem>();
                foreach (var it in _items)
                {
                    if (it.IsCustom) userCustomItems.Add(it);
                }

                _items.Clear();
                _items.AddRange(userCustomItems);

                // 2. 重新载入内置 300+ 条现行国家工程规范条目 (6大专业各 52 条)
                _items.AddRange(DefaultKnowledgeData.GetDefaultItems());

                SaveToFile();
            }
        }

        public static void SaveToFile(string path = null)
        {
            string targetPath = path ?? KnowledgeFilePath;
            try
            {
                string dir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var sb = new StringBuilder();
                sb.AppendLine("[");
                for (int i = 0; i < _items.Count; i++)
                {
                    var item = _items[i];
                    sb.AppendLine("  {");
                    sb.AppendLine(string.Format("    \"Id\": \"{0}\",", Escape(item.Id)));
                    sb.AppendLine(string.Format("    \"Discipline\": \"{0}\",", Escape(item.Discipline)));
                    sb.AppendLine(string.Format("    \"Title\": \"{0}\",", Escape(item.Title)));
                    sb.AppendLine(string.Format("    \"Suggestion\": \"{0}\",", Escape(item.Suggestion)));
                    sb.AppendLine(string.Format("    \"StandardCode\": \"{0}\",", Escape(item.StandardCode)));
                    sb.AppendLine(string.Format("    \"IsCustom\": {0},", item.IsCustom ? "true" : "false"));
                    sb.AppendLine(string.Format("    \"UsageCount\": {0}", item.UsageCount));
                    sb.Append("  }");
                    if (i < _items.Count - 1) sb.Append(",");
                    sb.AppendLine();
                }
                sb.AppendLine("]");

                File.WriteAllText(targetPath, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        public static void LoadFromFile(string path = null)
        {
            string targetPath = path ?? KnowledgeFilePath;
            if (!File.Exists(targetPath)) return;

            try
            {
                string json = File.ReadAllText(targetPath, Encoding.UTF8);
                var items = ParseKnowledgeJson(json);
                if (items != null && items.Count > 0)
                {
                    foreach (var it in items)
                    {
                        if (it.StandardCode == "手动录入")
                        {
                            it.StandardCode = string.Empty;
                            it.IsCustom = true;
                        }
                    }
                    lock (_lock)
                    {
                        _items.Clear();
                        _items.AddRange(items);
                    }
                }
            }
            catch { }
        }

        private static string Escape(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");
        private static string Unescape(string s) => (s ?? "").Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\");

        private static List<KnowledgeItem> ParseKnowledgeJson(string json)
        {
            var list = new List<KnowledgeItem>();
            if (string.IsNullOrEmpty(json)) return list;

            int cur = 0;
            int len = json.Length;

            while (cur < len)
            {
                int openBrace = json.IndexOf('{', cur);
                if (openBrace < 0) break;
                int closeBrace = json.IndexOf('}', openBrace);
                if (closeBrace < 0) break;

                string objStr = json.Substring(openBrace + 1, closeBrace - openBrace - 1);
                var item = ParseSingleItem(objStr);
                if (item != null && !string.IsNullOrEmpty(item.Title))
                {
                    list.Add(item);
                }
                cur = closeBrace + 1;
            }

            return list;
        }

        private static KnowledgeItem ParseSingleItem(string content)
        {
            var item = new KnowledgeItem();
            string[] lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.EndsWith(",")) trimmed = trimmed.Substring(0, trimmed.Length - 1).Trim();
                int colon = trimmed.IndexOf(':');
                if (colon > 0)
                {
                    string key = trimmed.Substring(0, colon).Trim().Trim('"');
                    string val = trimmed.Substring(colon + 1).Trim();
                    if (val.StartsWith("\"") && val.EndsWith("\"") && val.Length >= 2)
                    {
                        val = val.Substring(1, val.Length - 2);
                    }
                    val = Unescape(val);

                    if (key.Equals("Id", StringComparison.OrdinalIgnoreCase)) item.Id = val;
                    else if (key.Equals("Discipline", StringComparison.OrdinalIgnoreCase)) item.Discipline = val;
                    else if (key.Equals("Title", StringComparison.OrdinalIgnoreCase)) item.Title = val;
                    else if (key.Equals("Suggestion", StringComparison.OrdinalIgnoreCase)) item.Suggestion = val;
                    else if (key.Equals("StandardCode", StringComparison.OrdinalIgnoreCase)) item.StandardCode = val;
                    else if (key.Equals("IsCustom", StringComparison.OrdinalIgnoreCase))
                    {
                        item.IsCustom = (val.Equals("true", StringComparison.OrdinalIgnoreCase) || val == "1");
                    }
                    else if (key.Equals("UsageCount", StringComparison.OrdinalIgnoreCase))
                    {
                        int count;
                        if (int.TryParse(val, out count)) item.UsageCount = count;
                    }
                }
            }
            return item;
        }
    }
}
