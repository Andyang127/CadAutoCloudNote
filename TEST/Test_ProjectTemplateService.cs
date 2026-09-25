using System;
using System.Linq;
using CadAutoCloudNote.Core.Config;
using CadAutoCloudNote.Core.Services;

namespace CadAutoCloudNote.Tests
{
    /// <summary>
    /// 测试工程场景模板引擎服务
    /// 包含：8大场景模板（审图、内部校对、会审、变更、竣工、交底、监理、BIM管综）映射规则与设定应用
    /// </summary>
    public static class Test_ProjectTemplateService
    {
        public static void RunTests()
        {
            Console.WriteLine("[测试套件] 3. 批注使用对象(使用本工具的人)规则驱动引擎");

            // 用例 1: 9大使用对象角色模板加载
            TestTemplatesLoading();

            // 用例 2: 获取指定使用对象模板属性及强制锁定
            TestGetSpecificTemplate();

            // 用例 3: 选定使用对象后全量规则与专属审查意见应用至系统参数
            TestApplyTemplateToSettings();

            // 用例 4: 适用专业白名单与不相关专业自动置灰过滤测试
            TestDisciplineApplicabilityRules();

            // 用例 5: 当前活动对象属性
            TestCurrentTemplateProperty();
        }

        private static void TestTemplatesLoading()
        {
            var templates = ProjectTemplateService.GetAllTemplates();
            Assert(templates != null && templates.Count >= 9, "使用对象模板加载数量不足9个，实际: " + (templates?.Count ?? 0));

            var ids = templates.Select(t => t.Id).ToList();
            Assert(ids.Contains("tpl_general"), "缺少通用设计人模板 tpl_general");
            Assert(ids.Contains("tpl_audit"), "缺少审图专家模板 tpl_audit");
            Assert(ids.Contains("tpl_check"), "缺少内部校审模板 tpl_check");
            Assert(ids.Contains("tpl_meeting"), "缺少多方会审模板 tpl_meeting");
            Assert(ids.Contains("tpl_change"), "缺少设计变更模板 tpl_change");
            Assert(ids.Contains("tpl_asbuilt"), "缺少竣工图归档模板 tpl_asbuilt");
            Assert(ids.Contains("tpl_construct"), "缺少施工现场技术员模板 tpl_construct");
            Assert(ids.Contains("tpl_supervise"), "缺少监理工程师模板 tpl_supervise");
            Assert(ids.Contains("tpl_clash"), "缺少BIM管综工程师模板 tpl_clash");

            // 验证每个对象均配置了明确的角色身份与职责
            foreach (var t in templates)
            {
                Assert(!string.IsNullOrEmpty(t.RoleTitle), string.Format("模板 {0} 缺少 RoleTitle 角色身份定位", t.Id));
                Assert(t.ApplicableDisciplines != null && t.ApplicableDisciplines.Count > 0, string.Format("模板 {0} 缺少适用专业白名单", t.Id));
                Assert(t.QuickReviewSnippets != null && t.QuickReviewSnippets.Count > 0, string.Format("模板 {0} 缺少专属审查意见库", t.Id));
            }

            Console.WriteLine(string.Format("  [PASS] 用例1: 9大批注使用对象模板与专属规则加载完整 (总数: {0})", templates.Count));
        }

        private static void TestGetSpecificTemplate()
        {
            var auditTpl = ProjectTemplateService.GetTemplate("tpl_audit");
            Assert(auditTpl != null, "获取审图模板失败");
            Assert(auditTpl.Prefix == "AUD-", "审图模板前缀应为 AUD-, 实际: " + auditTpl.Prefix);
            Assert(auditTpl.LayerName == "AUDIT-NOTE", "审图模板图层错误: " + auditTpl.LayerName);
            Assert(auditTpl.ColorIndex == 6, "审图模板颜色索引错误: " + auditTpl.ColorIndex);

            var checkTpl = ProjectTemplateService.GetTemplate("tpl_check");
            Assert(checkTpl.ForceNonPlotting == true, "内部校审对象应强制不打印");
            Assert(checkTpl.IsNonPlottingLocked == true, "内部校审对象必须锁定强制不打印开关");
            Assert(checkTpl.Prefix == "CHK-", "校审模板前缀应为 CHK-");

            Console.WriteLine("  [PASS] 用例2: 场景模板专属图层、颜色、前缀及强制不打印锁定校验通过");
        }

        private static void TestApplyTemplateToSettings()
        {
            var settings = new NoteSettings();
            bool applied = ProjectTemplateService.ApplyTemplateToSettings("tpl_supervise", settings);
            Assert(applied, "应用监理巡查模板失败");
            Assert(settings.CurrentTemplateId == "tpl_supervise", "设置中的模板ID未正确更新");
            Assert(settings.CloudLayer == "SUPERVISOR-NOTE", "图层未更新为监理图层: " + settings.CloudLayer);
            Assert(settings.TextLayer == "SUPERVISOR-NOTE_TEXT", "文字图层未更新为监理文字图层: " + settings.TextLayer);
            var genTpl = ProjectTemplateService.GetTemplate("tpl_general");
            Assert(genTpl.TextLayerName == "CAD_NOTE_TEXT", "通用模板文字图层错误: " + genTpl.TextLayerName);
            Assert(settings.AutoNumberPrefix == "SUP-", "前缀未更新为 SUP-");
            Assert(settings.ForceNonPlotting == false, "监理图层应允许打印");
            Assert(settings.QuickReviewSnippets != null && settings.QuickReviewSnippets.Count > 0, "专属审查意见未全量同步到系统设置");
            Assert(settings.QuickReviewSnippets.Any(s => s.Contains("监理") || s.Contains("规范") || s.Contains("旁站")), "专属审查意见库未成功加载监理特色词条");

            Console.WriteLine("  [PASS] 用例3: 使用对象全量规则及专属审查意见同步 NoteSettings 成功");
        }

        private static void TestDisciplineApplicabilityRules()
        {
            // 1. 施工现场技术员 (tpl_construct): 仅建筑、结构、通用适用，水/暖/电/总图自动置灰
            var constructTpl = ProjectTemplateService.GetTemplate("tpl_construct");
            Assert(constructTpl.IsDisciplineApplicable("建筑"), "施工现场技术员应适用建筑专业");
            Assert(constructTpl.IsDisciplineApplicable("结构"), "施工现场技术员应适用结构专业");
            Assert(constructTpl.IsDisciplineApplicable("通用"), "施工现场技术员应适用通用专业");
            Assert(!constructTpl.IsDisciplineApplicable("给排水"), "施工现场技术员不应涉及给排水专业(应置灰)");
            Assert(!constructTpl.IsDisciplineApplicable("暖通"), "施工现场技术员不应涉及暖通专业(应置灰)");
            Assert(!constructTpl.IsDisciplineApplicable("电气"), "施工现场技术员不应涉及电气专业(应置灰)");
            Assert(!constructTpl.IsDisciplineApplicable("总图"), "施工现场技术员不应涉及总图专业(应置灰)");

            // 2. BIM管综工程师 (tpl_clash): 结构、总图置灰，机电专业高亮
            var clashTpl = ProjectTemplateService.GetTemplate("tpl_clash");
            Assert(clashTpl.IsDisciplineApplicable("给排水"), "BIM管综应适用给排水");
            Assert(clashTpl.IsDisciplineApplicable("暖通"), "BIM管综应适用暖通");
            Assert(clashTpl.IsDisciplineApplicable("电气"), "BIM管综应适用电气");
            Assert(!clashTpl.IsDisciplineApplicable("结构"), "BIM管综不应涉及结构(应置灰)");
            Assert(!clashTpl.IsDisciplineApplicable("总图"), "BIM管综不应涉及总图(应置灰)");

            Console.WriteLine("  [PASS] 用例4: 选定对象后适用专业白名单与不相关专业自动置灰过滤判定完全正确");
        }

        private static void TestCurrentTemplateProperty()
        {
            var current = ProjectTemplateService.CurrentTemplate;
            Assert(current != null, "CurrentTemplate 为空");
            Assert(!string.IsNullOrEmpty(current.Name), "CurrentTemplate.Name 为空");

            Console.WriteLine(string.Format("  [PASS] 用例5: CurrentTemplate 动态获取成功 -> '{0}' ({1})", current.Name, current.RoleTitle));
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
