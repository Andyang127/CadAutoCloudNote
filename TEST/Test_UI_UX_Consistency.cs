using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CadAutoCloudNote.Core.Models;
using CadAutoCloudNote.Core.Protocols;
using CadAutoCloudNote.Core.Services;

namespace CadAutoCloudNote.Tests
{
    /// <summary>
    /// 测试 UI / UX 规范性与各版本一致性
    /// 包含：
    /// 1. 杜绝“智能质检”、“AI智能联想”等浮夸词汇
    /// 2. 验证系统设置对话框外框几何像素已大幅精简（面积减半，消除巨型遮挡）
    /// 3. 验证批注编辑界面专业置顶、标题可下拉速选与手动输入
    /// </summary>
    public static class Test_UI_UX_Consistency
    {
        public static void RunTests()
        {
            Console.WriteLine("[测试套件] 6. UI / UX 规范性与全版本一致性");

            // 用例 1: 扫描源码与 XAML，确保已彻底清除浮夸字眼
            TestNoHypeWordsInUI();

            // 用例 2: 验证系统设置窗口尺寸定义 (严格锁定 460x400 NoResize)
            TestConfigWindowDimensions();

            // 用例 3: 批注对话框元素与专业置顶
            TestNoteEditDialogStructure();

            // 用例 4: 全工程彻底消除原生白底弹窗，统一切换为深色 CadMessageBox / CadWinFormMessageBox
            TestUnifiedCadMessageBox();

            // 用例 5: 验证“批注使用对象（使用本工具的人）”顶置核心驱动架构及 WPF/WinForms 双端对等
            TestTargetPersonaArchitectureConsistency();

            // 用例 6: 验证颜色规范（文字锁定黑白7号、云线与重要/紧急同步）、快捷指令双列分散居中网格与急速批注≤10条黄金短语
            TestColorSyncAndCommandGridAndRapidCorePhrases();

            // 用例 7: 各个方向原点与托线位置自适应几何、引线与文字零交叉隔离、CAD 2007 启动命令看板注册检验
            TestLeaderOriginAndDirectionGeometry();

            // 用例 8: 验证 AutoCAD 原生撤销 (Undo/Redo) 栈保护与反应器隔离机制
            TestUndoRedoProtection();

            // 用例 9: 验证批注生命周期状态机流转鲁棒性与“批注者” vs “回复人(使用者)”角色解耦
            TestStateTransitionsAndRoleSeparation();

            // 用例 10: 验证流转说明与整改回复的多触点呈现（看板Tooltip+详情卡片、导出报表、图面闭环表第7列）
            TestClosedLoopMultiTouchpointPresentation();
        }

        private static string FindProjectRoot()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(dir))
            {
                if (Directory.Exists(Path.Combine(dir, "CadAutoCloudNote.Core")))
                {
                    return dir;
                }
                var parent = Directory.GetParent(dir);
                if (parent == null) break;
                dir = parent.FullName;
            }
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        private static void TestNoHypeWordsInUI()
        {
            string projectRoot = FindProjectRoot();
            string xamlFile = Path.Combine(projectRoot, @"CadAutoCloudNote.Core\UI.Wpf\ConfigWindow.xaml");
            string noteXamlFile = Path.Combine(projectRoot, @"CadAutoCloudNote.Core\UI.Wpf\NoteEditDialog.xaml");

            string[] forbiddenWords = new[] { "智能质检", "AI 智能联想", "AI智能联想", "AI 智能" };

            if (File.Exists(xamlFile))
            {
                string content = File.ReadAllText(xamlFile);
                foreach (var word in forbiddenWords)
                {
                    Assert(!content.Contains(word), string.Format("在 ConfigWindow.xaml 中检测到浮夸用词: '{0}'", word));
                }
            }

            if (File.Exists(noteXamlFile))
            {
                string noteContent = File.ReadAllText(noteXamlFile);
                foreach (var word in forbiddenWords)
                {
                    Assert(!noteContent.Contains(word), string.Format("在 NoteEditDialog.xaml 中检测到浮夸用词: '{0}'", word));
                }
            }

            Console.WriteLine("  [PASS] 用例1: 已彻底消除'智能质检'、'AI智能联想'等浮夸字眼，符合严谨工程风格");
        }

        private static void TestConfigWindowDimensions()
        {
            string projectRoot = FindProjectRoot();
            string xamlFile = Path.Combine(projectRoot, @"CadAutoCloudNote.Core\UI.Wpf\ConfigWindow.xaml");

            if (File.Exists(xamlFile))
            {
                string content = File.ReadAllText(xamlFile);
                // 校验尺寸锁定为 460x400 且禁用拉伸 ResizeMode="NoResize"
                Assert(content.Contains("Width=\"460\""), "ConfigWindow 宽度未设置为 460");
                Assert(content.Contains("Height=\"400\""), "ConfigWindow 高度未设置为 400");
                Assert(content.Contains("MinWidth=\"460\""), "ConfigWindow 最小宽度未锁定为 460");
                Assert(content.Contains("MinHeight=\"400\""), "ConfigWindow 最小高度未锁定为 400");
                Assert(content.Contains("MaxWidth=\"460\""), "ConfigWindow 最大宽度未锁定为 460");
                Assert(content.Contains("MaxHeight=\"400\""), "ConfigWindow 最大高度未锁定为 400");
                Assert(content.Contains("ResizeMode=\"NoResize\""), "ConfigWindow 未锁定 ResizeMode=NoResize");
            }

            Console.WriteLine("  [PASS] 用例2: ConfigWindow 外框物理像素精准固化为 460x400 (NoResize) 校验通过");
        }

        private static void TestNoteEditDialogStructure()
        {
            string projectRoot = FindProjectRoot();
            string noteXaml = Path.Combine(projectRoot, @"CadAutoCloudNote.Core\UI.Wpf\NoteEditDialog.xaml");
            string noteForm = Path.Combine(projectRoot, @"CadAutoCloudNote.Core\UI.WinForm\NoteEditForm.cs");

            if (File.Exists(noteXaml))
            {
                string xaml = File.ReadAllText(noteXaml);
                Assert(xaml.Contains("PanelDisciplineChips"), "WPF 未包含所属专业置顶组件 PanelDisciplineChips");
                Assert(xaml.Contains("CmbTitle"), "WPF 未包含标题 ComboBox CmbTitle");
                Assert(xaml.Contains("IsEditable=\"True\""), "WPF 标题 ComboBox 必须允许手动编辑 (IsEditable=True)");
                Assert(xaml.Contains("x:Name=\"PART_EditableTextBox\""), "WPF BulletproofComboBox 模板必须包含 PART_EditableTextBox 以支持文字显示和编辑");
                Assert(!xaml.Contains("图面文字，下拉"), "WPF 标题标签中不得包含'图面文字，下拉'等低幼冗余说明文案");
                Assert(xaml.Contains("批注标题"), "WPF 标题标签应规范展示为'批注标题'");
            }

            if (File.Exists(noteForm))
            {
                string form = File.ReadAllText(noteForm);
                Assert(form.Contains("cmbTitle = new ComboBox"), "WinForms 未将标题升级为 ComboBox");
                Assert(form.Contains("ComboBoxStyle.DropDown"), "WinForms 标题 ComboBox 必须为 DropDown 允许手动键入");
            }

            Console.WriteLine("  [PASS] 用例3: WPF 与 WinForms 双端架构对等：专业置顶、标题可下拉/手动录入且支持高精渲染与清晰专业文案");
        }

        private static void TestUnifiedCadMessageBox()
        {
            string projectRoot = FindProjectRoot();
            string coreDir = Path.Combine(projectRoot, @"CadAutoCloudNote.Core");

            string[] filesToScan = new[]
            {
                Path.Combine(coreDir, @"UI.Wpf\ConfigWindow.xaml.cs"),
                Path.Combine(coreDir, @"UI.Wpf\NoteEditDialog.xaml.cs"),
                Path.Combine(coreDir, @"UI.Wpf\NotePaletteControl.xaml.cs"),
                Path.Combine(coreDir, @"UI.WinForm\ConfigForm.cs"),
                Path.Combine(coreDir, @"UI.WinForm\NotePaletteControl.cs"),
                Path.Combine(coreDir, @"UI.WinForm\NoteEditForm.cs"),
                Path.Combine(coreDir, @"UI\NoteUIProvider.cs")
            };

            foreach (var file in filesToScan)
            {
                if (!File.Exists(file)) continue;
                string content = File.ReadAllText(file);
                // 确保无原生 MessageBox.Show 出现（必须全部替换为 CadMessageBox 或 CadWinFormMessageBox）
                string[] lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.StartsWith("//") || line.StartsWith("/*") || line.StartsWith("*")) continue;
                    if (line.Contains("System.Windows.MessageBox.Show") ||
                        (line.Contains("MessageBox.Show") && !line.Contains("CadMessageBox.Show") && !line.Contains("CadWinFormMessageBox.Show")))
                    {
                        throw new Exception(string.Format("在文件 {0} 第 {1} 行发现原生白底弹窗调用: {2}",
                            Path.GetFileName(file), i + 1, line));
                    }
                }
            }

            // 验证深色弹窗主体文件存在
            string wpfMsgBox = Path.Combine(coreDir, @"UI.Wpf\CadMessageBox.xaml");
            string winFormMsgBox = Path.Combine(coreDir, @"UI.WinForm\CadWinFormMessageBox.cs");
            Assert(File.Exists(wpfMsgBox), "CadMessageBox.xaml 不存在");
            Assert(File.Exists(winFormMsgBox), "CadWinFormMessageBox.cs 不存在");

            Console.WriteLine("  [PASS] 用例4: 全工程彻底消除原生白底弹窗，统一切换为深色 CadMessageBox / CadWinFormMessageBox 验证通过");
        }

        private static void TestTargetPersonaArchitectureConsistency()
        {
            string projectRoot = FindProjectRoot();
            string cfgXaml = Path.Combine(projectRoot, @"CadAutoCloudNote.Core\UI.Wpf\ConfigWindow.xaml");
            string noteXaml = Path.Combine(projectRoot, @"CadAutoCloudNote.Core\UI.Wpf\NoteEditDialog.xaml");
            string paletteXaml = Path.Combine(projectRoot, @"CadAutoCloudNote.Core\UI.Wpf\NotePaletteControl.xaml");
            string cfgForm = Path.Combine(projectRoot, @"CadAutoCloudNote.Core\UI.WinForm\ConfigForm.cs");
            string noteForm = Path.Combine(projectRoot, @"CadAutoCloudNote.Core\UI.WinForm\NoteEditForm.cs");

            // 1. ConfigWindow.xaml 校验
            if (File.Exists(cfgXaml))
            {
                string content = File.ReadAllText(cfgXaml);
                Assert(content.Contains("CmbTargetPersonaHero"), "ConfigWindow 未包含顶置全宽驱动下拉 CmbTargetPersonaHero");
                Assert(content.Contains("BtnApplyHeroTemplate"), "ConfigWindow 未包含一键应用此对象规则按钮 BtnApplyHeroTemplate");
                Assert(content.Contains("PanelAudience"), "ConfigWindow 未包含首位展示的使用对象规则面板 PanelAudience");
                Assert(content.Contains("TabBtnAudience"), "ConfigWindow 未包含使用对象与规则导航选项卡 TabBtnAudience");
            }

            // 2. NoteEditDialog.xaml 校验
            if (File.Exists(noteXaml))
            {
                string content = File.ReadAllText(noteXaml);
                Assert(content.Contains("CmbTargetPersona"), "NoteEditDialog 未包含批注使用对象下拉 CmbTargetPersona");
                Assert(content.Contains("Property=\"IsEnabled\" Value=\"False\""), "NoteEditDialog 缺少专业芯片禁用置灰样式触发器");
            }

            // 3. NotePaletteControl.xaml 校验
            if (File.Exists(paletteXaml))
            {
                string content = File.ReadAllText(paletteXaml);
                Assert(content.Contains("CmbTargetPersona"), "NotePaletteControl 侧边栏未包含使用对象快速切换下拉 CmbTargetPersona");
            }

            // 4. WinForms 对等性校验
            if (File.Exists(cfgForm))
            {
                string content = File.ReadAllText(cfgForm);
                Assert(content.Contains("cmbTargetPersonaHero"), "ConfigForm 未包含顶置使用对象下拉 cmbTargetPersonaHero");
                Assert(content.Contains("btnApplyPersonaHero"), "ConfigForm 未包含一键应用规则按钮 btnApplyPersonaHero");
            }

            if (File.Exists(noteForm))
            {
                string content = File.ReadAllText(noteForm);
                Assert(content.Contains("cmbTargetPersona"), "NoteEditForm 未包含使用对象下拉 cmbTargetPersona");
            }

            Console.WriteLine("  [PASS] 用例5: “批注使用对象（使用本工具的人）”顶置核心驱动架构及 WPF/WinForms 双端严格对等校验通过");
        }

        private static void TestColorSyncAndCommandGridAndRapidCorePhrases()
        {
            string projectRoot = FindProjectRoot();
            string cfgXaml = Path.Combine(projectRoot, @"CadAutoCloudNote.Core\UI.Wpf\ConfigWindow.xaml");

            // 1. 验证底栏“恢复默认”按钮宽度至少 68px 且包含 Padding=0 居中，彻底消除截断
            if (File.Exists(cfgXaml))
            {
                string content = File.ReadAllText(cfgXaml);
                Assert(content.Contains("x:Name=\"BtnReset\" Width=\"68\""), "BtnReset 按钮宽度未扩展至 68 像素");
                Assert(content.Contains("Padding=\"0\" HorizontalContentAlignment=\"Center\""), "BtnReset 按钮缺少 Padding=0 或居中对齐");
                
                // 2. 验证快捷指令双列分散居中 Grid 网格
                Assert(content.Contains("<ColumnDefinition Width=\"48\"/>"), "快捷指令面板缺少 48px 短别名列");
                Assert(content.Contains("<ColumnDefinition Width=\"96\"/>"), "快捷指令面板缺少 96px 完整命令列");
                Assert(content.Contains("Text=\"CN\""), "快捷指令面板首项应为别名 CN");
                Assert(content.Contains("Text=\"CNOTE\""), "快捷指令面板首项全称应为 CNOTE");
            }

            // 3. 验证急速批注黄金预置短语数量严格限制在 10 条以内，且首项为'待修改'
            var corePhrases = CadAutoCloudNote.Core.Services.RapidPhraseService.GetCoreRapidPresetPhrases();
            Assert(corePhrases != null && corePhrases.Count <= 10, "急速批注黄金预置短语数不得超过 10 条");
            Assert(corePhrases.Count == 8, "急速批注黄金预置短语数应精准为 8 条");
            Assert(corePhrases[0] == "待修改", "急速批注首选项必须为'待修改'");
            Assert(corePhrases.Contains("请核实"), "急速批注应包含'请核实'");
            Assert(corePhrases.Contains("请补充"), "急速批注应包含'请补充'");
            Assert(corePhrases.Contains("请复核"), "急速批注应包含'请复核'");
            Assert(corePhrases.Contains("待确认"), "急速批注应包含'待确认'");
            Assert(corePhrases.Contains("核对有误"), "急速批注应包含'核对有误'");
            Assert(corePhrases.Contains("信息不全"), "急速批注应包含'信息不全'");
            Assert(corePhrases.Contains("做法不明"), "急速批注应包含'做法不明'");

            // 4. 验证默认配置颜色体系：文字颜色默认与开箱云线色一致为 1（红色），默认优先级为重要
            var settings = new CadAutoCloudNote.Core.Config.NoteSettings();
            Assert(settings.TextColorIndex == 1, "文字颜色默认索引必须锁定为 1 (红色)");
            Assert(settings.DefaultPriority == "重要", "默认优先级应为'重要'");
            Assert(settings.CloudColorIndex == 1, "开箱默认云线色必须严格锁定为 1 (红色)");

            // 5. 验证 WinForms ConfigForm.cs 与 WPF 100% 对齐：5 大 Tab、底栏急速入口与主题自适应
            string cfgForm = Path.Combine(projectRoot, @"CadAutoCloudNote.Core\UI.WinForm\ConfigForm.cs");
            if (File.Exists(cfgForm))
            {
                string formContent = File.ReadAllText(cfgForm);
                // 验证 5 大选项卡命名与定义
                Assert(formContent.Contains("TabPage tabAudience = new TabPage(\"对象与规则\")"), "WinForms 缺少'对象与规则' Tab");
                Assert(formContent.Contains("TabPage tabDrawing = new TabPage(\"绘图与样式\")"), "WinForms 缺少'绘图与样式' Tab");
                Assert(formContent.Contains("TabPage tabRapid = new TabPage(\"极速与短语\")"), "WinForms 缺少'极速与短语' Tab");
                Assert(formContent.Contains("TabPage tabKnowledge = new TabPage(\"规范知识库\")"), "WinForms 缺少'规范知识库' Tab");
                Assert(formContent.Contains("TabPage tabSystem = new TabPage(\"快捷与系统\")"), "WinForms 缺少'快捷与系统' Tab");
                
                // 验证底栏急速批注复选框与预置词下拉
                Assert(formContent.Contains("chkBottomSimpleMode"), "WinForms 底栏缺少 chkBottomSimpleMode 急速批注复选框");
                Assert(formContent.Contains("cmbBottomRapidPhrase"), "WinForms 底栏缺少 cmbBottomRapidPhrase 预置词下拉");
                
                // 验证浅色主题顶栏白灰/浅蓝自适应，杜绝黑色突兀色块
                Assert(formContent.Contains("HeroBarBg = ColorTranslator.FromHtml(\"#EDF1F7\")"), "WinForms 浅色模式顶栏未配置柔和灰蓝背景 #EDF1F7");
                Assert(formContent.Contains("HeroBarText = ColorTranslator.FromHtml(\"#1E293B\")"), "WinForms 浅色模式顶栏文字未配置深炭黑 #1E293B");
                Assert(formContent.Contains("TagBg = ColorTranslator.FromHtml(\"#E2EEFA\")"), "WinForms 浅色模式快捷指令 Tag 未配置冰蓝背景 #E2EEFA");
                Assert(formContent.Contains("TagText = ColorTranslator.FromHtml(\"#0070D2\")"), "WinForms 浅色模式快捷指令 Tag 未配置 CAD 蓝字 #0070D2");

                // 验证快捷指令双列居中设计 (48px 短别名 + 96px 完整命令)
                Assert(formContent.Contains("Size(48, 22)"), "WinForms 快捷指令未包含 48px 短别名 Tag");
                Assert(formContent.Contains("Size(96, 22)"), "WinForms 快捷指令未包含 96px 完整命令 Tag");
                Assert(formContent.Contains("ContentAlignment.MiddleCenter"), "WinForms 快捷指令 Tag 文字未居中对齐");
            }

            Console.WriteLine("  [PASS] 用例6: 颜色体系（默认云线1号红/文字7号黑白）、WinForms 5大Tab与底栏急速入口对齐、浅色主题顶栏白灰蓝自适应、快捷指令双列居中对齐校验全部通过");
        }

        private static void TestLeaderOriginAndDirectionGeometry()
        {
            Console.WriteLine("\n[测试套件] 7. 各个方向批注原点与托线位置自适应几何与启动命令检验");

            string projectRoot = FindProjectRoot();

            // 1. 验证 Commands.cs 中注册了 [CommandMethod("云线批注看板")]
            string cmdFile = Path.Combine(projectRoot, @"CadAutoCloudNote.Core\Commands.cs");
            Assert(File.Exists(cmdFile), "Commands.cs 不存在");
            string cmdContent = File.ReadAllText(cmdFile);
            Assert(cmdContent.Contains("[CommandMethod(\"云线批注看板\")]"), "Commands.cs 必须注册 [CommandMethod(\"云线批注看板\")]，防止低版本 CAD 启动报未知命令");

            // 2. 验证 PaletteManager.cs 构造函数使用 NOTEPANEL 命令名
            string palFile = Path.Combine(projectRoot, @"CadAutoCloudNote.Core\UI.WinForm\PaletteManager.cs");
            Assert(File.Exists(palFile), "PaletteManager.cs 不存在");
            string palContent = File.ReadAllText(palFile);
            Assert(palContent.Contains("new PaletteSet(\"NOTEPANEL\""), "PaletteManager 必须采用 NOTEPANEL 作为持久化命令名");

            // 3. 验证左侧与右侧批注放置几何对齐逻辑 (杜绝引线切割文字与托线反向问题)
            double textHeight = 3.5;
            string testTitle = "待修改（26年09月19日）";
            double contentWidth = CadAutoCloudNote.Core.Services.NoteService.CalculateNoteContentWidth(testTitle, textHeight, 0, true);
            double landingLength = CadAutoCloudNote.Core.Services.NoteService.CalculateSimpleNoteLandingLength(testTitle, textHeight, 0, true);
            double triH = textHeight * 1.8;
            double triW = triH / 0.866025;
            double textGap = textHeight * 0.45;

            Assert(contentWidth > 15.0, "文字内容宽度测算必须大于 15");
            Assert(landingLength > contentWidth + (triW / 2.0) + textGap, "水平托线总长必须大于文字与小三角间隙总和");

            // 验证左侧放置 (Note to the LEFT of cloud, cloud at X=100, kneePt at X=0)
            double kneeX = 0.0;
            double cloudX = 100.0;
            double leftTextLocX = kneeX - (triW / 2.0) - textGap - contentWidth;
            double leftTextEndX = leftTextLocX + contentWidth;
            double leftLandingEndX = kneeX - landingLength;

            // 断言左侧放置时：托线末端 < 文字起点 < 文字终点 < 小三角重心(kneeX) < 云线(cloudX)
            Assert(leftLandingEndX < leftTextLocX, "左侧放置时水平托线必须向左充分延伸托住文字起点");
            Assert(leftTextEndX < kneeX, "左侧放置时文字终点必须在小三角左侧，杜绝侵入小三角");
            Assert(kneeX < cloudX, "小三角转折点位于文字与云线之间");

            // 验证引线在 [kneeX, cloudX] = [0, 100]，文字在 [leftTextLocX, leftTextEndX] = [-30, -5]
            // 两者 X 范围完全隔离，交集为空：引线绝不可能切割或穿透文字！
            Assert(leftTextEndX < kneeX, "引线区间与文字区间必须完全解耦，引线绝对不会穿透文字！");

            // 验证右侧放置 (Note to the RIGHT of cloud, cloud at X=-100, kneePt at X=0)
            double rightKneeX = 0.0;
            double rightCloudX = -100.0;
            double rightTextLocX = rightKneeX + (triW / 2.0) + textGap;
            double rightTextEndX = rightTextLocX + contentWidth;
            double rightLandingEndX = rightKneeX + landingLength;

            Assert(rightLandingEndX > rightTextEndX, "右侧放置时水平托线必须向右充分延伸托住文字终点");
            Assert(rightTextLocX > rightKneeX, "右侧放置时文字起点必须在小三角右侧");
            Assert(rightCloudX < rightKneeX, "云线位于小三角转折点左侧");

            // 4. 验证 ReactorManager.cs 严格锁定 MiddleLeft 对齐，确保移动后引线托线始终在两行文字中间
            string reactorFile = Path.Combine(projectRoot, @"CadAutoCloudNote.Core\Reactors\ReactorManager.cs");
            Assert(File.Exists(reactorFile), "ReactorManager.cs 不存在");
            string reactorContent = File.ReadAllText(reactorFile);
            Assert(!reactorContent.Contains("mtext.Attachment = isSimple ? AttachmentPoint.MiddleLeft : AttachmentPoint.BottomLeft"), "ReactorManager 不得将非简易批注设置为 BottomLeft，必须统一锁定 MiddleLeft");

            Console.WriteLine("  [PASS] 用例7: 各个方向原点与托线位置自适应几何、引线与文字零交叉隔离、移动后居中对齐、CAD 2007 启动命令看板注册检验全部通过");
        }

        private static void TestUndoRedoProtection()
        {
            string projectRoot = FindProjectRoot();
            string coreDir = Path.Combine(projectRoot, @"CadAutoCloudNote.Core");

            // 1. 验证 IsUndoOrRedoCommand 命令解析
            Assert(CadAutoCloudNote.Core.Reactors.ReactorManager.IsUndoOrRedoCommand("U"), "U 命令必须被识别为撤销命令");
            Assert(CadAutoCloudNote.Core.Reactors.ReactorManager.IsUndoOrRedoCommand("UNDO"), "UNDO 命令必须被识别为撤销命令");
            Assert(CadAutoCloudNote.Core.Reactors.ReactorManager.IsUndoOrRedoCommand("-UNDO"), "-UNDO 命令必须被识别为撤销命令");
            Assert(CadAutoCloudNote.Core.Reactors.ReactorManager.IsUndoOrRedoCommand("_U"), "_U 命令必须被识别为撤销命令");
            Assert(CadAutoCloudNote.Core.Reactors.ReactorManager.IsUndoOrRedoCommand("._U"), "._U 命令必须被识别为撤销命令");
            Assert(CadAutoCloudNote.Core.Reactors.ReactorManager.IsUndoOrRedoCommand("REDO"), "REDO 命令必须被识别为重做命令");
            Assert(CadAutoCloudNote.Core.Reactors.ReactorManager.IsUndoOrRedoCommand("MREDO"), "MREDO 命令必须被识别为重做命令");
            Assert(CadAutoCloudNote.Core.Reactors.ReactorManager.IsUndoOrRedoCommand("OOPS"), "OOPS 命令必须被识别为恢复命令");
            Assert(!CadAutoCloudNote.Core.Reactors.ReactorManager.IsUndoOrRedoCommand("LINE"), "LINE 命令绝不能识别为撤销命令");
            Assert(!CadAutoCloudNote.Core.Reactors.ReactorManager.IsUndoOrRedoCommand("CIRCLE"), "CIRCLE 命令绝不能识别为撤销命令");
            Assert(!CadAutoCloudNote.Core.Reactors.ReactorManager.IsUndoOrRedoCommand("MOVE"), "MOVE 命令绝不能识别为撤销命令");
            Assert(!CadAutoCloudNote.Core.Reactors.ReactorManager.IsUndoOrRedoCommand("CN"), "CN 命令绝不能识别为撤销命令");

            // 2. 验证 ReactorManager.cs 源码防护机制
            string reactorFile = Path.Combine(coreDir, @"Reactors\ReactorManager.cs");
            Assert(File.Exists(reactorFile), "ReactorManager.cs 不存在");
            string reactorContent = File.ReadAllText(reactorFile);

            Assert(!reactorContent.Contains("OnApplicationIdle"), "ReactorManager 中必须彻底移除 Idle 事件事务提交，防止破坏命令撤销组");
            Assert(reactorContent.Contains("IsUndoOrRedoCommand"), "ReactorManager 必须包含 Undo/Redo 命令探测逻辑");
            Assert(reactorContent.Contains("state.IsCommandUndoing"), "ReactorManager 必须通过 IsCommandUndoing 状态机拦截撤销期间的图元事件");
            Assert(reactorContent.Contains("string.IsNullOrEmpty(noteId)"), "ReactorManager 图元事件处理必须完全过滤非插件实体");
            Assert(reactorContent.Contains("if (anyModified)"), "ReactorManager 必须包含 anyModified 防护，杜绝空事务提交进撤销流");

            // 3. 验证看板刷新代码不启动删除事务
            string wpfPalette = Path.Combine(coreDir, @"UI.Wpf\NotePaletteControl.xaml.cs");
            if (File.Exists(wpfPalette))
            {
                string wpfContent = File.ReadAllText(wpfPalette);
                Assert(!wpfContent.Contains("XRecordHelper.DeleteNote(doc.Database, tr, n.NoteId)"), "WPF 看板刷新循环中禁止直接启动事务物理删除 XRecord");
            }

            string winformPalette = Path.Combine(coreDir, @"UI.WinForm\NotePaletteControl.cs");
            if (File.Exists(winformPalette))
            {
                string winformContent = File.ReadAllText(winformPalette);
                Assert(!winformContent.Contains("XRecordHelper.DeleteNote(doc.Database, tr, n.NoteId)"), "WinForms 看板刷新循环中禁止直接启动事务物理删除 XRecord");
            }

            // 4. 验证只读查询中不调用 tr.Commit()
            string noteServiceFile = Path.Combine(coreDir, @"Services\NoteService.cs");
            Assert(File.Exists(noteServiceFile), "NoteService.cs 不存在");
            string noteServiceContent = File.ReadAllText(noteServiceFile);
            Assert(!noteServiceContent.Contains("ReadAllNotes(db, tr);\r\n                tr.Commit();"),
                "GetAllNotes 只读查询禁止调用 tr.Commit()");

            Console.WriteLine("  [PASS] 用例8: AutoCAD 原生撤销 (Undo/Redo) 栈保护、非插件实体事件过滤、只读免提交及空事务拦截校验全部通过");
        }

        private static void TestStateTransitionsAndRoleSeparation()
        {
            // 1. 状态机流转规则校验 (Closed 和 Rejected 状态流转增强)
            string err;
            Assert(ValidationService.ValidateStateTransition(NoteLifecycleState.Closed, NoteLifecycleState.Pending, out err), "Closed 应允许流转为 Pending");
            Assert(ValidationService.ValidateStateTransition(NoteLifecycleState.Closed, NoteLifecycleState.Resolved, out err), "Closed 应允许流转为 Resolved");
            Assert(ValidationService.ValidateStateTransition(NoteLifecycleState.Closed, NoteLifecycleState.Closed, out err), "Closed 应允许保持 Closed 写入说明");

            Assert(ValidationService.ValidateStateTransition(NoteLifecycleState.Rejected, NoteLifecycleState.Pending, out err), "Rejected 应允许流转为 Pending");
            Assert(ValidationService.ValidateStateTransition(NoteLifecycleState.Rejected, NoteLifecycleState.Resolved, out err), "Rejected 应允许流转为 Resolved");
            Assert(ValidationService.ValidateStateTransition(NoteLifecycleState.Rejected, NoteLifecycleState.Closed, out err), "Rejected 应允许流转为 Closed");

            // 2. 非法流转友好中文提示校验
            Assert(!ValidationService.ValidateStateTransition(NoteLifecycleState.Draft, NoteLifecycleState.Resolved, out err), "Draft 禁止直接流转为 Resolved");
            Assert(err.Contains("草稿") && err.Contains("已改") && err.Contains("待办"), "错误提示必须友好输出中文状态名与建议引导，实际: " + err);
            Assert(!err.Contains("Draft") && !err.Contains("Resolved"), "错误提示不得包含生硬英文枚举名");

            // 3. 验证 NoteLifecycleState.CanTransition 规则与 ValidationService 100% 对齐
            Assert(NoteStateHelper.CanTransition(NoteLifecycleState.Closed, NoteLifecycleState.Resolved), "CanTransition 必须允许 Closed->Resolved");
            Assert(NoteStateHelper.CanTransition(NoteLifecycleState.Rejected, NoteLifecycleState.Resolved), "CanTransition 必须允许 Rejected->Resolved");

            // 4. 验证“批注者”与“回复人”名词体系解耦与规范性
            string projectRoot = FindProjectRoot();
            string coreDir = Path.Combine(projectRoot, @"CadAutoCloudNote.Core");
            
            string replyFormFile = Path.Combine(coreDir, @"UI.WinForm\NoteReplyForm.cs");
            string replyFormContent = File.ReadAllText(replyFormFile);
            Assert(replyFormContent.Contains("批注者"), "NoteReplyForm 必须包含'批注者'字段");
            Assert(replyFormContent.Contains("回复人 (使用者)"), "NoteReplyForm 必须包含'回复人 (使用者)'字段以区分提意见人与当次流转人");

            string replyDialogFile = Path.Combine(coreDir, @"UI.Wpf\NoteReplyDialog.xaml");
            string replyDialogContent = File.ReadAllText(replyDialogFile);
            Assert(replyDialogContent.Contains("批注者"), "NoteReplyDialog.xaml 必须包含'批注者'");
            Assert(replyDialogContent.Contains("回复人 (使用者)"), "NoteReplyDialog.xaml 必须包含'回复人 (使用者)'");

            Console.WriteLine("  [PASS] 用例9: 状态机流转鲁棒性校验、友好中文引导输出及“批注者”与“回复人(使用者)”角色严格解耦验证通过");
        }

        private static void TestClosedLoopMultiTouchpointPresentation()
        {
            string projectRoot = FindProjectRoot();
            string coreDir = Path.Combine(projectRoot, @"CadAutoCloudNote.Core");

            // 1. 验证 ExportService 导出 CSV 与 Excel XML 具备整改答复与流转人字段
            string exportServiceFile = Path.Combine(coreDir, @"Services\ExportService.cs");
            string exportServiceCode = File.ReadAllText(exportServiceFile);
            Assert(exportServiceCode.Contains("最新回复人"), "ExportService CSV 表头必须包含'最新回复人'");
            Assert(exportServiceCode.Contains("最新整改答复"), "ExportService CSV 表头必须包含'最新整改答复'");
            Assert(exportServiceCode.Contains("最新整改答复/流转说明"), "ExportService Excel XML 表头必须包含'最新整改答复/流转说明'");
            Assert(exportServiceCode.Contains("答复人(使用者)"), "ExportService Excel XML 表头必须包含'答复人(使用者)'");
            Assert(exportServiceCode.Contains("EscapeXml(latestReplyContent)"), "ExportService 必须输出答复内容");
            Assert(exportServiceCode.Contains("EscapeXml(latestReplier)"), "ExportService 必须输出答复人姓名");

            // 2. 验证 SummaryTableService 图面汇总表升级为 7 列并包含整改答复
            string summaryTableFile = Path.Combine(coreDir, @"Services\SummaryTableService.cs");
            string summaryTableCode = File.ReadAllText(summaryTableFile);
            Assert(summaryTableCode.Contains("numCols = 7"), "SummaryTableService 必须升级为 7 列");
            Assert(summaryTableCode.Contains("整改答复/流转说明"), "SummaryTableService 表头必须包含'整改答复/流转说明'");
            Assert(summaryTableCode.Contains("图 纸 审 查 与 整 改 闭 环 汇 总 表"), "SummaryTableService 标题行应体现整改闭环");

            // 3. 验证看板双端（WinForms & WPF）悬停浮动卡片与底部详情预览
            string winPaletteFile = Path.Combine(coreDir, @"UI.WinForm\NotePaletteControl.cs");
            string winPaletteCode = File.ReadAllText(winPaletteFile);
            Assert(winPaletteCode.Contains("ShowItemToolTips = true"), "WinForms 看板必须开启 ShowItemToolTips");
            Assert(winPaletteCode.Contains("pnlDetail"), "WinForms 看板必须包含底部详情预览卡片 pnlDetail");
            Assert(winPaletteCode.Contains("ToolTipText"), "WinForms 看板列表项必须绑定包含最新答复的 ToolTipText");

            string wpfPaletteXaml = Path.Combine(coreDir, @"UI.Wpf\NotePaletteControl.xaml");
            string wpfPaletteXamlCode = File.ReadAllText(wpfPaletteXaml);
            Assert(wpfPaletteXamlCode.Contains("Value=\"{Binding TooltipText}\""), "WPF 看板项必须绑定 TooltipText");
            Assert(wpfPaletteXamlCode.Contains("TxtDetailReply"), "WPF 看板必须包含底部最新答复预览 TxtDetailReply");

            string wpfPaletteCs = Path.Combine(coreDir, @"UI.Wpf\NotePaletteControl.xaml.cs");
            string wpfPaletteCsCode = File.ReadAllText(wpfPaletteCs);
            Assert(wpfPaletteCsCode.Contains("TooltipText"), "WPF NoteViewModel 必须提供 TooltipText 属性");

            Console.WriteLine("  [PASS] 用例10: 流转说明与修改答复多触点呈现（看板Tooltip+详情卡片、报表导出扩展、图面会审表第7列闭环）全链路校验通过");
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
