using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CadAutoCloudNote.Core.Config;

namespace CadAutoCloudNote.Core.Services
{
    /// <summary>
    /// 快捷短语实体
    /// </summary>
    public class RapidPhraseItem
    {
        public string Id { get; set; }
        public string Discipline { get; set; } = "通用";
        public string Category { get; set; } = "常用";
        public string Text { get; set; } = string.Empty;
        public bool IsCustom { get; set; } = false;
        public int UsageCount { get; set; } = 0;

        public RapidPhraseItem() { }

        public RapidPhraseItem(string id, string discipline, string category, string text, bool isCustom = false)
        {
            Id = id;
            Discipline = discipline;
            Category = category;
            Text = text;
            IsCustom = isCustom;
        }

        public override string ToString() => Text;
    }

    /// <summary>
    /// 极速批注高频快捷短语服务（内置词库、增删改查、使用统计与本地持久化）
    /// </summary>
    public static class RapidPhraseService
    {
        private static readonly object _lock = new object();
        private static List<RapidPhraseItem> _cache;

        private static string StorageFilePath => Path.Combine(ConfigManager.ConfigDirectory, "RapidPhrases.json");

        public static List<RapidPhraseItem> GetAll()
        {
            EnsureLoaded();
            lock (_lock)
            {
                return new List<RapidPhraseItem>(_cache);
            }
        }

        public static List<RapidPhraseItem> GetByDiscipline(string discipline)
        {
            EnsureLoaded();
            lock (_lock)
            {
                if (string.IsNullOrEmpty(discipline) || discipline == "全部")
                {
                    return new List<RapidPhraseItem>(_cache);
                }
                return _cache.Where(p => string.Equals(p.Discipline, discipline, StringComparison.OrdinalIgnoreCase)).ToList();
            }
        }

        public static void AddOrUpdate(RapidPhraseItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.Text)) return;
            EnsureLoaded();
            lock (_lock)
            {
                var existing = _cache.FirstOrDefault(p => p.Id == item.Id || (p.Text == item.Text && (p.Discipline == item.Discipline || p.Discipline == "通用" || item.Discipline == "通用")));
                if (existing != null)
                {
                    existing.Discipline = item.Discipline;
                    existing.Category = item.Category;
                    existing.Text = item.Text;
                    existing.UsageCount++;
                }
                else
                {
                    if (string.IsNullOrEmpty(item.Id))
                    {
                        item.Id = "RP-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
                    }
                    item.IsCustom = true;
                    item.UsageCount = Math.Max(1, item.UsageCount);
                    _cache.Add(item);
                }
                SaveInternal();
            }
        }

        public static bool Delete(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            EnsureLoaded();
            lock (_lock)
            {
                int removed = _cache.RemoveAll(p => p.Id == id);
                if (removed > 0)
                {
                    SaveInternal();
                    return true;
                }
                return false;
            }
        }

        public static void IncrementUsage(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            EnsureLoaded();
            lock (_lock)
            {
                var item = _cache.FirstOrDefault(p => p.Text == text || text.StartsWith(p.Text));
                if (item != null)
                {
                    item.UsageCount++;
                    SaveInternal();
                }
            }
        }

        public static List<RapidPhraseItem> GetTopUsed(int count = 10)
        {
            EnsureLoaded();
            lock (_lock)
            {
                return _cache.OrderByDescending(p => p.UsageCount).Take(count).ToList();
            }
        }

        /// <summary>
        /// 获取专为急速免弹窗批注精选的 8 大全专业黄金预置短语（严格控制在 10 条以内，符合认知心理学 7±2 瞬时决策原则）
        /// 包含：待修改、请核实、请补充、请复核、待确认、核对有误、信息不全、做法不明
        /// </summary>
        public static List<string> GetCoreRapidPresetPhrases()
        {
            return new List<string>
            {
                "待修改",
                "请核实",
                "请补充",
                "请复核",
                "待确认",
                "核对有误",
                "信息不全",
                "做法不明"
            };
        }

        /// <summary>
        /// 获取专为 CNQ 极速批注命令行/动态输入 "S" 键量身定制的 1-8/9 条精炼短语
        /// 严格与设置 UI 中的【急速批注默认成图短语】下拉列表保持 100% 对齐一致
        /// 包含：待修改、请核实、请补充、请复核、待确认、核对有误、信息不全、做法不明
        /// </summary>
        public static List<RapidPhraseItem> GetRapidPromptPhrases(string discipline = null)
        {
            EnsureLoaded();
            lock (_lock)
            {
                var result = new List<RapidPhraseItem>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // 1. 若当前配置了自定义急速默认短语，确保该短语优先处于列表第 1 项
                string currentDefault = null;
                try
                {
                    currentDefault = Config.ConfigManager.Instance?.CurrentSettings?.RapidDefaultPhrase;
                }
                catch { }

                if (!string.IsNullOrEmpty(currentDefault))
                {
                    var match = _cache.FirstOrDefault(p => string.Equals(p.Text, currentDefault, StringComparison.OrdinalIgnoreCase))
                                ?? new RapidPhraseItem("RP-DEF", "通用", "简短快速", currentDefault);
                    result.Add(match);
                    seen.Add(currentDefault);
                }

                // 2. 依次加载 8 大核心预置短语（与 UI 下拉框顺序完全一致）
                var coreTexts = GetCoreRapidPresetPhrases();
                foreach (var text in coreTexts)
                {
                    if (result.Count >= 9) break;
                    if (seen.Add(text))
                    {
                        var match = _cache.FirstOrDefault(p => string.Equals(p.Text, text, StringComparison.OrdinalIgnoreCase))
                                    ?? new RapidPhraseItem("RP-CORE", "通用", "简短快速", text);
                        result.Add(match);
                    }
                }

                // 3. 若未满 9 条，由用户高频使用或自定义的“简短快速”精炼短语补充
                var extraShorts = _cache.Where(p => p.Category == "简短快速" && p.Text != null && p.Text.Length <= 6)
                                        .OrderByDescending(p => p.UsageCount)
                                        .ToList();
                foreach (var item in extraShorts)
                {
                    if (result.Count >= 9) break;
                    if (seen.Add(item.Text))
                    {
                        result.Add(item);
                    }
                }

                return result;
            }
        }

        public static void ResetToDefaults()
        {
            lock (_lock)
            {
                _cache = GetBuiltInDefaults();
                SaveInternal();
            }
        }

        private static void EnsureLoaded()
        {
            if (_cache != null) return;
            lock (_lock)
            {
                if (_cache != null) return;
                _cache = LoadFromDisk();
                if (_cache == null || _cache.Count == 0)
                {
                    _cache = GetBuiltInDefaults();
                    SaveInternal();
                }
                else
                {
                    var defaults = GetBuiltInDefaults();
                    bool added = false;
                    foreach (var d in defaults)
                    {
                        if (!_cache.Any(c => c.Text == d.Text && c.Discipline == d.Discipline))
                        {
                            _cache.Add(d);
                            added = true;
                        }
                    }
                    if (added) SaveInternal();
                }
            }
        }

        private static List<RapidPhraseItem> LoadFromDisk()
        {
            try
            {
                string path = StorageFilePath;
                if (!File.Exists(path)) return null;

                string json = File.ReadAllText(path, Encoding.UTF8);
                var items = new List<RapidPhraseItem>();
                var blocks = SplitJsonObjects(json);

                foreach (var b in blocks)
                {
                    var dict = ParseSimpleJsonBlock(b);
                    if (dict.ContainsKey("Text") && !string.IsNullOrEmpty(dict["Text"]))
                    {
                        var item = new RapidPhraseItem
                        {
                            Id = dict.ContainsKey("Id") ? dict["Id"] : ("RP-" + Guid.NewGuid().ToString("N").Substring(0, 8)),
                            Discipline = dict.ContainsKey("Discipline") ? dict["Discipline"] : "通用",
                            Category = dict.ContainsKey("Category") ? dict["Category"] : "常用",
                            Text = dict["Text"],
                            IsCustom = dict.ContainsKey("IsCustom") && dict["IsCustom"].Equals("true", StringComparison.OrdinalIgnoreCase),
                            UsageCount = dict.ContainsKey("UsageCount") && int.TryParse(dict["UsageCount"], out int uc) ? uc : 0
                        };
                        items.Add(item);
                    }
                }
                return items;
            }
            catch
            {
                return null;
            }
        }

        private static void SaveInternal()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("[");
                for (int i = 0; i < _cache.Count; i++)
                {
                    var p = _cache[i];
                    sb.AppendLine("  {");
                    sb.AppendLine(string.Format("    \"Id\": \"{0}\",", Escape(p.Id)));
                    sb.AppendLine(string.Format("    \"Discipline\": \"{0}\",", Escape(p.Discipline)));
                    sb.AppendLine(string.Format("    \"Category\": \"{0}\",", Escape(p.Category)));
                    sb.AppendLine(string.Format("    \"Text\": \"{0}\",", Escape(p.Text)));
                    sb.AppendLine(string.Format("    \"IsCustom\": {0},", p.IsCustom ? "true" : "false"));
                    sb.AppendLine(string.Format("    \"UsageCount\": {0}", p.UsageCount));
                    sb.Append("  }");
                    if (i < _cache.Count - 1) sb.AppendLine(",");
                    else sb.AppendLine();
                }
                sb.AppendLine("]");
                File.WriteAllText(StorageFilePath, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        private static string Escape(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

        private static List<string> SplitJsonObjects(string json)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(json)) return list;

            int braceCount = 0;
            int startIndex = -1;

            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '{')
                {
                    if (braceCount == 0) startIndex = i;
                    braceCount++;
                }
                else if (c == '}')
                {
                    braceCount--;
                    if (braceCount == 0 && startIndex != -1)
                    {
                        list.Add(json.Substring(startIndex, i - startIndex + 1));
                        startIndex = -1;
                    }
                }
            }
            return list;
        }

        private static Dictionary<string, string> ParseSimpleJsonBlock(string block)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var lines = block.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                string t = line.Trim().TrimEnd(',');
                int colon = t.IndexOf(':');
                if (colon > 0)
                {
                    string key = t.Substring(0, colon).Trim().Trim('"');
                    string val = t.Substring(colon + 1).Trim().Trim('"');
                    dict[key] = val;
                }
            }
            return dict;
        }

        /// <summary>
        /// 基于《批注-快捷短语.md》内置的 6 大专业与销项回复完整专业词库
        /// </summary>
        public static List<RapidPhraseItem> GetBuiltInDefaults()
        {
            var list = new List<RapidPhraseItem>();
            int idSeq = 1;

            void Add(string disc, string cat, string txt)
            {
                list.Add(new RapidPhraseItem(string.Format("RP-{0:D3}", idSeq++), disc, cat, txt, false));
            }

            // 一、通用类 - 简短快速批注
            Add("通用", "简短快速", "待修改");
            Add("通用", "简短快速", "请核实");
            Add("通用", "简短快速", "请补充");
            Add("通用", "简短快速", "请修改");
            Add("通用", "简短快速", "请复核");
            Add("通用", "简短快速", "待确认");
            Add("通用", "简短快速", "核对有误");
            Add("通用", "简短快速", "信息不全");
            Add("通用", "简短快速", "做法不明");

            // 一、通用类 - 图纸表达/校对类
            Add("通用", "表达校对", "此处表达不清，请补充节点详图");
            Add("通用", "表达校对", "尺寸标注缺失，请核对完善");
            Add("通用", "表达校对", "标高存在冲突，请复核");
            Add("通用", "表达校对", "图例与图纸不一致，请统一");
            Add("通用", "表达校对", "图面线型混乱，请调整");
            Add("通用", "表达校对", "剖面索引缺失，请补画剖切索引");
            Add("通用", "表达校对", "图纸比例有误，请核对");
            Add("通用", "表达校对", "图名、图号与目录不匹配，请修正");
            Add("通用", "表达校对", "索引详图编号找不到，请补全");
            Add("通用", "表达校对", "引出标注指向不明确，请调整");
            Add("通用", "表达校对", "重复标注，清理冗余尺寸");
            Add("通用", "表达校对", "缺少定位轴线，请补充");
            Add("通用", "表达校对", "图面文字重叠，请调整位置");

            // 一、通用类 - 专业对图类
            Add("通用", "专业协同", "与建筑图纸冲突，请核对协调");
            Add("通用", "专业协同", "与结构图纸冲突，请复核调整");
            Add("通用", "专业协同", "与给排水图纸冲突，请核对");
            Add("通用", "专业协同", "与电气图纸冲突，请协调处理");
            Add("通用", "专业协同", "与暖通图纸冲突，请复核");
            Add("通用", "专业协同", "平面与大样图不一致，请统一");
            Add("通用", "专业协同", "建筑底图已更新，请同步更新本图");

            // 销项回复类
            Add("销项回复", "整改销项", "已按要求修改");
            Add("销项回复", "整改销项", "已补充节点详图");
            Add("销项回复", "整改销项", "已核对，无误");
            Add("销项回复", "整改销项", "已调整，满足规范");
            Add("销项回复", "整改销项", "图纸已统一，消除冲突");
            Add("销项回复", "整改销项", "本问题已整改完成，请复核");
            Add("销项回复", "整改销项", "经复核，此项无需调整，详见说明");

            // 二、建筑专业 - 规范合规类
            Add("建筑", "规范合规", "疏散宽度不满足规范要求，请复核");
            Add("建筑", "规范合规", "疏散距离超出规范限值，请调整");
            Add("建筑", "规范合规", "防火分区面积超限，请重新划分");
            Add("建筑", "规范合规", "防火门等级/开启方向不符合规范");
            Add("建筑", "规范合规", "防火间距不足，请复核调整");
            Add("建筑", "规范合规", "安全出口数量不足，请核查");
            Add("建筑", "规范合规", "楼梯间形式不满足规范要求");
            Add("建筑", "规范合规", "避难走道构造不符合规范");
            Add("建筑", "规范合规", "房间疏散门开启影响疏散，请调整");
            Add("建筑", "规范合规", "外墙保温耐火性能不满足规范，请复核");

            // 二、建筑专业 - 构造详图类
            Add("建筑", "构造详图", "屋面防水构造层次缺失，请补充");
            Add("建筑", "构造详图", "外墙节点做法不明确，请补详图");
            Add("建筑", "构造详图", "窗台压顶做法未注明，请完善");
            Add("建筑", "构造详图", "变形缝构造做法不详，请补充");
            Add("建筑", "构造详图", "室内外高差标注缺失，请核对");
            Add("建筑", "构造详图", "栏杆高度、竖杆净距不满足规范");
            Add("建筑", "构造详图", "台阶踏步尺寸不符合规范，请调整");
            Add("建筑", "构造详图", "门窗洞口定位缺失，请补充尺寸");
            Add("建筑", "构造详图", "地下室防水做法未明确，请完善");

            // 三、结构专业 - 荷载与计算类
            Add("结构", "荷载计算", "楼面荷载取值，请复核确认");
            Add("结构", "荷载计算", "活荷载取值不符合规范，请调整");
            Add("结构", "荷载计算", "抗震设防参数取值，请复核");
            Add("结构", "荷载计算", "基础持力层描述不详，请核对地质报告");
            Add("结构", "荷载计算", "地基承载力取值，请复核");
            Add("结构", "荷载计算", "构件计算模型假设，请核实");

            // 三、结构专业 - 构件与表达类
            Add("结构", "构件表达", "梁标高冲突，请复核核对");
            Add("结构", "构件表达", "梁、柱定位尺寸缺失，请补充");
            Add("结构", "构件表达", "板配筋标注不全，请完善");
            Add("结构", "构件表达", "主次梁附加吊筋/箍筋未设置，请补充");
            Add("结构", "构件表达", "节点构造做法不详，请补大样");
            Add("结构", "构件表达", "后浇带构造及配筋未注明，请完善");
            Add("结构", "构件表达", "沉降观测点布置，请补充");
            Add("结构", "构件表达", "基础平面缺少定位轴线，请完善");
            Add("结构", "构件表达", "剪力墙边缘构件配筋不完整，请核对");
            Add("结构", "构件表达", "构件编号混乱，请统一编号规则");
            Add("结构", "构件表达", "预埋件定位、规格未注明，请补充");

            // 四、给排水专业
            Add("给排水", "专业设计", "管道标高与土建构件冲突，请复核");
            Add("给排水", "专业设计", "消防水池有效容积不足，请核算");
            Add("给排水", "专业设计", "消火栓布置不能全覆盖，请调整");
            Add("给排水", "专业设计", "喷淋喷头间距超出规范限值，请复核");
            Add("给排水", "专业设计", "排水横管坡度不满足要求，请调整");
            Add("给排水", "专业设计", "管道支吊架做法未注明，请补充");
            Add("给排水", "专业设计", "消防水泵参数选型，请复核");
            Add("给排水", "专业设计", "污水检查井定位缺失，请完善");
            Add("给排水", "专业设计", "管材、阀门压力等级未注明，请补充");
            Add("给排水", "专业设计", "防水套管设置位置、规格不详");

            // 五、电气专业
            Add("电气", "专业设计", "配电回路负荷计算，请复核");
            Add("电气", "专业设计", "疏散照明布置不满足规范，请调整");
            Add("电气", "专业设计", "火灾探测器布置间距超限，请核对");
            Add("电气", "专业设计", "电缆桥架与管线标高冲突，请协调");
            Add("电气", "专业设计", "接地平面缺少接地极布置详图");
            Add("电气", "专业设计", "配电箱系统图与平面图不一致");
            Add("电气", "专业设计", "管线敷设方式未注明，请完善");
            Add("电气", "专业设计", "防雷类别、接闪器布置，请复核");
            Add("电气", "专业设计", "应急电源容量选型，请核算");
            Add("电气", "专业设计", "弱电点位定位缺失，请补充");

            // 六、暖通专业
            Add("暖通", "专业设计", "风管标高与其他专业管线冲突，请协调");
            Add("暖通", "专业设计", "排烟口布置距离不满足规范，请调整");
            Add("暖通", "专业设计", "通风量、排烟量计算，请复核");
            Add("暖通", "专业设计", "空调水管坡度未标注，请补充");
            Add("暖通", "专业设计", "防火阀设置位置不符合规范");
            Add("暖通", "专业设计", "设备基础定位尺寸缺失，请完善");
            Add("暖通", "专业设计", "风管支吊架做法未注明，请补充");
            Add("暖通", "专业设计", "送风口、排风口位置不合理，请调整");

            // 七、总图专业
            Add("总图", "专业设计", "建筑物定位坐标与规划红线冲突，请复核");
            Add("总图", "专业设计", "道路转弯半径不满足消防车道要求，请调整");
            Add("总图", "专业设计", "竖向设计标高与市政接驳标高不符，请复核");
            Add("总图", "专业设计", "消防车登高操作场地尺寸或坡度不符合规范");
            Add("总图", "专业设计", "绿化率/建筑密度指标计算有误，请核实");
            Add("总图", "专业设计", "室外雨污水管线标高倒坡，请调整");
            Add("总图", "专业设计", "出入口视线通视要求不满足规范，请优化");
            Add("总图", "专业设计", "无障碍坡道及盲道与市政人行道未衔接");

            return list;
        }
    }
}
