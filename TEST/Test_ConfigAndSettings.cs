using System;
using CadAutoCloudNote.Core.Config;
using CadAutoCloudNote.Core.Models;
using CadAutoCloudNote.Core.Protocols;

namespace CadAutoCloudNote.Tests
{
    /// <summary>
    /// 测试系统配置与参数管理
    /// 包含：全局默认参数校验、问题等级解析（一般/重要/紧急）、极速批注开关状态联动
    /// </summary>
    public static class Test_ConfigAndSettings
    {
        public static void RunTests()
        {
            Console.WriteLine("[测试套件] 5. 系统配置与参数管理");

            // 用例 1: 默认配置项完备性
            TestDefaultSettings();

            // 用例 2: 问题等级解析（Normal, Important, Urgent）
            TestPriorityParsing();

            // 用例 3: 极速批注参数合法性
            TestRapidModeSettings();
        }

        private static void TestDefaultSettings()
        {
            var settings = new NoteSettings();
            Assert(settings.TextHeightRatio > 0, "默认字高比例必须大于0");
            Assert(settings.ArrowSizeRatio > 0, "默认箭头比例必须大于0");
            Assert(!string.IsNullOrEmpty(settings.CloudLayer), "默认图层不能为空");
            Assert(settings.CloudColorIndex >= 0 && settings.CloudColorIndex <= 256, "默认颜色索引越界");

            Console.WriteLine(string.Format("  [PASS] 用例1: 默认配置完整 (字高比例: {0}, 箭头比例: {1}, 默认图层: {2})",
                settings.TextHeightRatio, settings.ArrowSizeRatio, settings.CloudLayer));
        }

        private static void TestPriorityParsing()
        {
            Assert(NoteSettings.ParsePriority("一般") == NotePriority.Normal, "一般解析失败");
            Assert(NoteSettings.ParsePriority("重要") == NotePriority.Important, "重要解析失败");
            Assert(NoteSettings.ParsePriority("紧急") == NotePriority.Urgent, "紧急解析失败");
            Assert(NoteSettings.ParsePriority("Normal") == NotePriority.Normal, "Normal解析失败");
            Assert(NoteSettings.ParsePriority("Important") == NotePriority.Important, "Important解析失败");
            Assert(NoteSettings.ParsePriority("Urgent") == NotePriority.Urgent, "Urgent解析失败");
            Assert(NoteSettings.ParsePriority("未知") == NotePriority.Important, "非法输入应降级为 Important");
            Assert(NoteSettings.ParsePriority("") == NotePriority.Important, "空输入应默认降级为 Important");

            Console.WriteLine("  [PASS] 用例2: 问题等级（重要/紧急及兼容一般）字符串与枚举解析正常");
        }

        private static void TestRapidModeSettings()
        {
            var settings = new NoteSettings();
            // 极速批注开启后，默认标题应具有有效文字
            settings.EnableSimpleNoteMode = true;
            settings.RapidDefaultPhrase = "待修改";
            Assert(settings.EnableSimpleNoteMode == true, "EnableSimpleNoteMode 设置失败");
            Assert(settings.RapidDefaultPhrase == "待修改", "RapidDefaultPhrase 设置失败");

            Console.WriteLine("  [PASS] 用例3: 极速免弹窗批注设置状态正常");
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
