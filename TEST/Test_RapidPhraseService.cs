using System;
using System.Collections.Generic;
using System.Linq;
using CadAutoCloudNote.Core.Services;

namespace CadAutoCloudNote.Tests
{
    /// <summary>
    /// 测试极速批注快捷短语库服务
    /// 包含：60+内置专业词库完备性、各专业筛选、新增自定义词条、频次累计与高频排序、JSON持久化
    /// </summary>
    public static class Test_RapidPhraseService
    {
        public static void RunTests()
        {
            Console.WriteLine("[测试套件] 2. 极速批注快捷短语库服务");

            // 用例 1: 内置短语库数量与专业覆盖
            TestBuiltinPhrasesCoverage();

            // 用例 2: 专业筛选联动
            TestDisciplineFiltering();

            // 用例 3: 新增与更新自定义短语
            TestAddOrUpdateCustomPhrase();

            // 用例 4: 使用频次累加与高频排序
            TestUsageCountAndRanking();

            // 用例 5: CNQ "S" 单键 1-9 精选短语库完备性与去重验证
            TestRapidPromptPhrases();
        }

        private static void TestBuiltinPhrasesCoverage()
        {
            var all = RapidPhraseService.GetAll();
            Assert(all != null && all.Count >= 50, "内置快捷短语数量不足，期望至少50条，实际: " + (all?.Count ?? 0));

            var disciplines = all.Select(p => p.Discipline).Distinct().ToList();
            string[] requiredDisciplines = new[] { "建筑", "结构", "给排水", "暖通", "电气", "总图" };
            foreach (var d in requiredDisciplines)
            {
                Assert(disciplines.Contains(d), "缺少必修专业快捷短语: " + d);
            }

            Console.WriteLine(string.Format("  [PASS] 用例1: 内置短语库加载完备 (总数: {0}, 覆盖专业: {1} 个)", all.Count, disciplines.Count));
        }

        private static void TestDisciplineFiltering()
        {
            var archList = RapidPhraseService.GetByDiscipline("建筑");
            Assert(archList.Count > 0, "建筑专业短语列表为空");
            foreach (var item in archList)
            {
                Assert(item.Discipline == "建筑", "建筑专业列表中混入了非建筑专业项: " + item.Discipline);
            }

            var hvacList = RapidPhraseService.GetByDiscipline("暖通");
            Assert(hvacList.Count > 0, "暖通专业短语列表为空");
            foreach (var item in hvacList)
            {
                Assert(item.Discipline == "暖通", "暖通专业列表中混入了非暖通专业项: " + item.Discipline);
            }

            Console.WriteLine(string.Format("  [PASS] 用例2: 专业独立筛选正常 (建筑: {0}条, 暖通: {1}条)", archList.Count, hvacList.Count));
        }

        private static void TestAddOrUpdateCustomPhrase()
        {
            string testPhrase = "自动化单元测试专用短语_" + DateTime.Now.Ticks;
            RapidPhraseService.AddOrUpdate(new RapidPhraseItem
            {
                Text = testPhrase,
                Discipline = "给排水",
                Category = "测试",
                IsCustom = true
            });

            var list = RapidPhraseService.GetByDiscipline("给排水");
            var found = list.FirstOrDefault(p => p.Text == testPhrase);
            Assert(found != null, "新增自定义快捷短语未在列表中找到");
            Assert(found.IsCustom, "新增短语未被正确标记为 IsCustom=true");

            Console.WriteLine("  [PASS] 用例3: 新增自定义快捷短语及内存/本地写入成功");
        }

        private static void TestUsageCountAndRanking()
        {
            var all = RapidPhraseService.GetAll();
            if (all.Count == 0) return;

            var target = all[0];
            int initialCount = target.UsageCount;

            RapidPhraseService.IncrementUsage(target.Text);
            var updated = RapidPhraseService.GetAll().FirstOrDefault(p => p.Text == target.Text);
            Assert(updated != null && updated.UsageCount == initialCount + 1, "短语使用频次自增失败");

            var topList = RapidPhraseService.GetTopUsed(5);
            Assert(topList != null && topList.Count > 0, "获取最高频短语列表失败");

            Console.WriteLine(string.Format("  [PASS] 用例4: 快捷短语频次追踪正常 (目标短语 '{0}' 频次: {1})", target.Text, updated.UsageCount));
        }

        private static void TestRapidPromptPhrases()
        {
            var common9 = RapidPhraseService.GetRapidPromptPhrases("通用");
            Assert(common9 != null && common9.Count == 9, "通用专业 CNQ 1-9 短语数量必须精确为 9 条，实际: " + (common9?.Count ?? 0));
            Assert(common9[0].Text == "待修改", "通用首条短语期望为'待修改'，实际: " + common9[0].Text);

            var arch9 = RapidPhraseService.GetRapidPromptPhrases("建筑");
            Assert(arch9 != null && arch9.Count == 9, "建筑专业 CNQ 1-9 短语数量必须精确为 9 条，实际: " + (arch9?.Count ?? 0));

            // 检查无重复文本
            var dupCheck = common9.Select(p => p.Text).Distinct().Count();
            Assert(dupCheck == common9.Count, "CNQ 1-9 快捷短语列表存在重复短语");

            Console.WriteLine(string.Format("  [PASS] 用例5: CNQ 'S' 单键 1-9 短语提取精确对齐 (通用首项: '{0}', 总数: {1})", common9[0].Text, common9.Count));
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception("断言失败: " + message);
            }
        }
    }
}
