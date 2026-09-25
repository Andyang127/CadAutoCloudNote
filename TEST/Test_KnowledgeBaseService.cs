using System;
using System.Linq;
using CadAutoCloudNote.Core.Services;

namespace CadAutoCloudNote.Tests
{
    /// <summary>
    /// 测试国家规范库与常用审查意见条目服务
    /// 包含：国标规范库条目验证、按专业归类组织、手动与国标条文区分标记
    /// </summary>
    public static class Test_KnowledgeBaseService
    {
        public static void RunTests()
        {
            Console.WriteLine("[测试套件] 4. 国家规范库与审查意见条目服务");

            // 用例 1: 规范条目加载与总数
            TestKnowledgeBaseLoading();

            // 用例 2: 国家规范条文编码合规性（GB 50016、GB 55008 等）
            TestStandardCodeFormats();

            // 用例 3: 手动录入与国标条文逻辑区分标记
            TestCustomVsStandardDistinction();
        }

        private static void TestKnowledgeBaseLoading()
        {
            var list = KnowledgeBaseService.GetOrganizedList();
            Assert(list != null && list.Count > 0, "知识库条目列表为空");

            var disciplines = list.Select(k => k.Discipline).Distinct().ToList();
            Assert(disciplines.Contains("建筑"), "缺少建筑专业规范条文");
            Assert(disciplines.Contains("结构"), "缺少结构专业规范条文");

            Console.WriteLine(string.Format("  [PASS] 用例1: 规范库条目加载正常 (总计: {0} 条, 涉及专业: {1} 个)", list.Count, disciplines.Count));
        }

        private static void TestStandardCodeFormats()
        {
            var list = KnowledgeBaseService.GetOrganizedList();
            var gbItems = list.Where(k => !k.IsCustom).ToList();
            Assert(gbItems.Count > 0, "未找到国家标准规范条文");

            bool hasFireCode = gbItems.Any(k => k.StandardCode.Contains("GB 55037") || k.StandardCode.Contains("GB 50016"));
            Assert(hasFireCode, "未检索到国家建筑防火通用规范 (GB 55037 / GB 50016) 条目");

            bool hasGeneralCivilCode = gbItems.Any(k => k.StandardCode.Contains("GB 55031"));
            Assert(hasGeneralCivilCode, "未检索到民用建筑通用规范 (GB 55031) 条目");

            Console.WriteLine(string.Format("  [PASS] 用例2: 现行国家通用规范编码校验通过 (包含 GB 55037 防火通规、GB 55031 等强条共 {0} 项)", gbItems.Count));
        }

        private static void TestCustomVsStandardDistinction()
        {
            // 保存一条自定义项
            string uniqueTitle = "测试审查意见_" + DateTime.Now.Ticks;
            KnowledgeBaseService.SaveUserCustomItem(uniqueTitle, "暖通", "测试建议内容", "手动录入");

            var list = KnowledgeBaseService.GetOrganizedList();
            var customItem = list.FirstOrDefault(k => k.Title == uniqueTitle);
            Assert(customItem != null, "保存的自定义条目未在知识库中检索到");
            Assert(customItem.IsCustom == true, "自定义条目 IsCustom 属性应为 true");
            Assert(customItem.SourceBadge == "自定义", "自定义来源徽标应为'自定义'");
            Assert(customItem.DisplayStandardCode == "--", "自定义条目无国标编号时展示应为'--'");

            Console.WriteLine("  [PASS] 用例3: 手动录入意见与国标规范条文逻辑区分验证通过");

            // 用例 4: 针对系统内置词条保存时，严格识别为系统自带，绝不生成 USR 自定义冗余项
            var firstSystemItem = list.FirstOrDefault(k => !k.IsCustom);
            Assert(firstSystemItem != null, "系统内置条目不能为空");
            int prevUsage = firstSystemItem.UsageCount;
            var savedBuiltin = KnowledgeBaseService.SaveUserCustomItem(firstSystemItem.Title, firstSystemItem.Discipline, firstSystemItem.Suggestion, firstSystemItem.StandardCode);
            Assert(savedBuiltin != null, "保存系统内置条目返回空");
            Assert(savedBuiltin.IsCustom == false, "系统内置条目保存后 IsCustom 属性仍应为 false");
            Assert(!savedBuiltin.Id.StartsWith("USR"), "系统内置条目绝不可被篡改为 USR 开头的自定义 ID");
            Assert(savedBuiltin.UsageCount == prevUsage + 1, "系统内置条目使用频次应成功递增");

            Console.WriteLine("  [PASS] 用例4: 系统自带词条与自主录入词条查重与频次递增无冗余隔离验证通过");
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
