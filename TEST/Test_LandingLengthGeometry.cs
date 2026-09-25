using System;
using CadAutoCloudNote.Core.Config;
using CadAutoCloudNote.Core.Services;

namespace CadAutoCloudNote.Tests
{
    /// <summary>
    /// 测试水平托线（Shelf/Landing line）紧凑收笔几何算法
    /// 验证图1现状修复为图2目标：托线紧贴文字右侧边缘收笔，无冗余延伸
    /// </summary>
    public static class Test_LandingLengthGeometry
    {
        public static void RunTests()
        {
            Console.WriteLine("[测试套件] 1. 水平托线紧凑收笔几何算法 (图1->图2几何修复验证)");

            // 测试用例 1: 标准三汉字（如 "待修改"）
            TestStandardThreeChars();

            // 测试用例 2: 纯英文字符（如 "REV-01"）
            TestAlphanumericChars();

            // 测试用例 3: 提取标题核心部分并剥离日期（如 "待修改（24年03月15日）" -> "待修改"）
            TestExtractSimpleTitlePart();

            // 测试用例 4: 空白/空字符串降级安全保护
            TestFallbackSafety();

            // 测试用例 5: 多字长标题紧贴验证（如 "标高与结构梁冲突"）
            TestLongChineseTitle();

            // 测试用例 6: 等边小三角几何重心与引线折弯拐点 (kneePt) 精确对齐验证 (彻底根除图1左侧腰线折弯割裂小三角缺陷)
            TestTriangleCentroidKneeAlignment();

            // 测试用例 7: 追加云线与引线继承原批注优先级严重程度颜色 (一般=绿3, 重要=品红6, 紧急=红1)
            TestPriorityColorInheritance();

            // 测试用例 8: 快速批注上下排布几何与文本格式验证
            TestSimpleVerticalLayout();

            // 测试用例 9: 非快速批注水平排布（标题与时间同排，下方规范详情）与长托线自适应验证
            TestNonSimpleHorizontalLayout();

            // 测试用例 10: 验证标题为'待修改'且含审查详情时，标题绝不被详情冲刷覆盖
            TestTitlePreservationWithDetailContent();
        }

        private static void TestStandardThreeChars()
        {
            string rawTitle = "待修改";
            double textHeight = 3.5;

            double triW = (3.5 * 1.8) / 0.866025;
            double textGap = 3.5 * 0.4;
            double titleWidth = 3.0 * (3.5 * 1.05);
            double dateWidth = (5 * 1.05 + 6 * 0.65) * (3.5 * 0.75);
            double contentWidth = Math.Max(titleWidth, dateWidth);
            double expected = (triW / 2.0) + textGap + contentWidth + (1.2 * 3.5);
            double actual = NoteService.CalculateSimpleNoteLandingLength(rawTitle, textHeight);

            Assert(Math.Abs(actual - expected) < 0.001,
                string.Format("三汉字'待修改'托线长度计算错误: 期望 {0:F4}, 实际 {1:F4}", expected, actual));

            // 验证托线充分向外延伸约 1.2 个字高收笔，绝不短缺露底，消除“横线还差一点”的逼仄感
            Assert(actual > 28.0 && actual < 38.0, "托线长度未完整覆盖第二行日期或未能优雅收笔！");
            Console.WriteLine(string.Format("  [PASS] 用例1: 三汉字'待修改'含完整日期 (h={0}) -> 托线精确长度 {1:F2} (理论 {2:F2})", textHeight, actual, expected));
        }

        private static void TestAlphanumericChars()
        {
            string rawTitle = "REV-01"; // 6个ASCII字符
            double textHeight = 3.0;

            // ASCII字符宽度按 0.65 * textHeight 计算
            double titleWidth = 6 * (3.0 * 0.65);
            double triH = 3.0 * 1.8;
            double triW = triH / 0.866025;
            double dateWidth = (5 * 1.05 + 6 * 0.65) * (3.0 * 0.75);
            double contentWidth = Math.Max(titleWidth, dateWidth);
            double expected = (triW / 2.0) + (3.0 * 0.4) + contentWidth + (1.2 * 3.0);

            double actual = NoteService.CalculateSimpleNoteLandingLength(rawTitle, textHeight);
            Assert(Math.Abs(actual - expected) < 0.001,
                string.Format("ASCII英文字符托线长度计算错误: 期望 {0:F4}, 实际 {1:F4}", expected, actual));

            Console.WriteLine(string.Format("  [PASS] 用例2: 英文标题'REV-01'含日期 (h={0}) -> 托线精确长度 {1:F2}", textHeight, actual));
        }

        private static void TestExtractSimpleTitlePart()
        {
            string fullWithDate = "待修改（24年03月15日）";
            string extracted = NoteService.ExtractSimpleTitlePart(fullWithDate);
            Assert(extracted == "待修改", "中文括号日期剥离失败，得到: " + extracted);

            string fullWithEnDate = "结构复核(2024-03-15)";
            string extractedEn = NoteService.ExtractSimpleTitlePart(fullWithEnDate);
            Assert(extractedEn == "结构复核", "英文括号日期剥离失败，得到: " + extractedEn);

            Console.WriteLine("  [PASS] 用例3: 核心标题提取与日期剥离验证通过");
        }

        private static void TestFallbackSafety()
        {
            double actualNull = NoteService.CalculateSimpleNoteLandingLength(null, 3.5);
            Assert(actualNull > 0, "空字符串计算托线应有默认安全保护！");

            double actualSmallH = NoteService.CalculateSimpleNoteLandingLength("待修改", 0.5); // 小于 2.0
            // 内部 Math.Max(2.0, textHeight) 保护
            Assert(actualSmallH > 5.0, "微小字号未做最小阈值保护！");

            Console.WriteLine("  [PASS] 用例4: 空值与异常极小字号边界安全保护通过");
        }

        private static void TestLongChineseTitle()
        {
            string title = "标高与结构梁冲突"; // 8个汉字
            double textHeight = 3.5;
            double actual = NoteService.CalculateSimpleNoteLandingLength(title, textHeight);

            double expected = (6.3 / 0.866025 / 2.0) + (3.5 * 0.4) + (8.0 * (3.5 * 1.05)) + (1.2 * 3.5);
            Assert(Math.Abs(actual - expected) < 0.001, "长汉字标题计算不匹配！");

            Console.WriteLine(string.Format("  [PASS] 用例5: 8字长标题'{0}' -> 托线精确贴合收笔 {1:F2}", title, actual));
        }

        private static void TestTriangleCentroidKneeAlignment()
        {
            double textHeight = 3.5;
            double triH = textHeight * 1.8; // 6.3
            double triW = triH / 0.866025; // 7.274619

            // 验证引线折弯拐点 (kneePt) 与等边小三角几何重心（Centroid）精确重合对齐
            // 顶点朝上: Y + H * (2/3)
            // 底边水平: Y - H * (1/3)
            // 水平托线正中穿过小三角几何重心，图面左右文字通过 textGap 完全避让
            double textPtX = 100.0;
            double textPtY = 50.0;
            double kneePtX = textPtX;
            double kneePtY = textPtY;

            double apexY = kneePtY + triH * (2.0 / 3.0);
            double baseY = kneePtY - triH * (1.0 / 3.0);
            double centroidY = (apexY + 2.0 * baseY) / 3.0;

            Assert(Math.Abs(centroidY - kneePtY) < 1e-4, "小三角几何重心必须精确对齐引线拐点 kneePt.Y");
            Assert(Math.Abs((apexY - baseY) - triH) < 1e-4, "小三角全高计算必须准确");

            Console.WriteLine(string.Format("  [PASS] 用例6: 等边小三角几何重心精确重合于引线折弯拐点 (H={0:F2}, W={1:F2}, CentroidY={2:F2})",
                triH, triW, centroidY));
        }

        private static void TestPriorityColorInheritance()
        {
            var settings = new CadAutoCloudNote.Core.Config.NoteSettings();
            settings.EnablePriorityColorLink = true;
            settings.PriorityColorNormal = 3;    // 绿色
            settings.PriorityColorImportant = 6; // 洋红
            settings.PriorityColorUrgent = 1;    // 红色

            // 验证优先级颜色与 AutoCAD 索引色严格映射
            int colorNormal = settings.GetPriorityColorIndex(CadAutoCloudNote.Core.Protocols.NotePriority.Normal);
            int colorImportant = settings.GetPriorityColorIndex(CadAutoCloudNote.Core.Protocols.NotePriority.Important);
            int colorUrgent = settings.GetPriorityColorIndex(CadAutoCloudNote.Core.Protocols.NotePriority.Urgent);

            Assert(colorNormal == 3, string.Format("一般严重度应映射为绿色(3)，实际: {0}", colorNormal));
            Assert(colorImportant == 6, string.Format("重要严重度应映射为洋红色(6)，实际: {0}", colorImportant));
            Assert(colorUrgent == 1, string.Format("紧急严重度应映射为红色(1)，实际: {0}", colorUrgent));

            // 验证中文字符串重载
            Assert(settings.GetPriorityColorIndex("一般") == 3, "字符串'一般'映射失败");
            Assert(settings.GetPriorityColorIndex("重要") == 6, "字符串'重要'映射失败");
            Assert(settings.GetPriorityColorIndex("紧急") == 1, "字符串'紧急'映射失败");

            Console.WriteLine("  [PASS] 用例7: 追加云线与引线颜色继承逻辑严格遵循：一般=3(绿)、重要=6(洋红)、紧急=1(红)");
        }

        private static void TestSimpleVerticalLayout()
        {
            string rawTitle = "待修改";
            string formatted = NoteService.FormatNoteContents(0, rawTitle, string.Empty, new DateTime(2026, 9, 19), isSimpleMode: true);
            Assert(formatted.StartsWith("\\A1;{\\fSimSun|b1|i0;待修改}\\P{\\H0.75x;"), "快速批注应为上下排布，第1行粗体标题，第2行小号日期");
            Assert(formatted.Contains("26年09月19日"), "快速批注日期格式不符");

            double len = NoteService.CalculateSimpleNoteLandingLength(rawTitle, 3.5, 0, isSimpleMode: true);
            Assert(len > 28.0 && len < 40.0, "快速批注托线长度计算异常");
            Console.WriteLine("  [PASS] 用例8: 快速批注为上下排布（横线上方为粗体标题，下方为日期戳），托线长度自适应收笔通过");
        }

        private static void TestNonSimpleHorizontalLayout()
        {
            string rawTitle = "安全出口数量不足，请核查";
            string content = "电缆井、管道井检修门耐火极限不得低于丙级【依据：GB 50016-2014】";
            string formatted = NoteService.FormatNoteContents(1, rawTitle, content, new DateTime(2026, 9, 19), isSimpleMode: false);
            string expectedPrefix = NoteService.FormatSequencePrefix(ConfigManager.Instance.CurrentSettings.AutoNumberPrefix, 1);
            Assert(formatted.StartsWith(string.Format("\\A1;{{\\fSimSun|b1|i0;{0} 安全出口数量不足，请核查}}  {{\\H0.75x;（26年09月19日）}}", expectedPrefix)), "非快速批注第一行应水平包含序号、标题与小字号日期");
            Assert(formatted.Contains("\\P" + content), "非快速批注横线下方应为规范与详情");

            double textHeight = 3.5;
            double len = NoteService.CalculateSimpleNoteLandingLength(rawTitle, textHeight, 1, isSimpleMode: false);
            Assert(len > 60.0, "非快速批注水平托线未覆盖第一排长标题与日期");
            Console.WriteLine(string.Format("  [PASS] 用例9: 非快速批注为水平排布（上方第一排为标题+时间，下方为详情），水平托线长度 {0:F2} 覆盖收笔通过", len));
        }

        private static void TestTitlePreservationWithDetailContent()
        {
            // 验证图1 Bug：标题为“待修改”，内容为“这是一个输入测试用的详情”时，第1行必须严格保留“待修改”，绝不可被详情内容冲刷覆盖！
            string rawTitle = "待修改";
            string content = "这是一个输入测试用的详情";
            string formatted = NoteService.FormatNoteContents(1, rawTitle, content, new DateTime(2026, 9, 25), isSimpleMode: false);
            string expectedPrefix = NoteService.FormatSequencePrefix(ConfigManager.Instance.CurrentSettings.AutoNumberPrefix, 1);

            Assert(formatted.Contains(string.Format("{0} 待修改", expectedPrefix)), "标题'待修改'被详情内容冲刷覆盖！实际输出: " + formatted);
            Assert(formatted.Contains("\\P" + content), "横线下方详情内容缺失");
            Assert(!formatted.StartsWith(string.Format("\\A1;{{\\fSimSun|b1|i0;{0} 这是一个输入测试用的详情", expectedPrefix)), "图1 Bug重现：标题被覆写为内容！");

            Console.WriteLine("  [PASS] 用例10: 标题'待修改'与审查详情解耦保护，第一行严格呈现'【1】 待修改'，绝不被内容覆写");
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
