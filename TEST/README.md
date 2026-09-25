# CAD Auto CloudNote - 自动化测试体系 (TEST Suite)

本目录 `TEST/` 为 CAD Auto CloudNote 项目的独立测试工程与验证体系。涵盖托线收笔几何计算、极速短语库、9大批注使用对象与工程角色模板引擎、国家规范库、系统配置以及 WPF / WinForms 双端 UI/UX 规范性检测。

---

## 目录结构

```
TEST/
├── CadAutoCloudNote.Tests.csproj  # 测试控制台项目文件 (基于 .NET Framework 4.8)
├── Program.cs                     # 自动化测试主入口与彩色汇总统计器
├── Test_LandingLengthGeometry.cs  # 水平托线紧凑收笔几何算法测试 (图1->图2几何修复验证)
├── Test_RapidPhraseService.cs     # 极速批注快捷短语库服务测试 (60+词条、专业筛选、频次与持久化)
├── Test_ProjectTemplateService.cs # 工程场景模板引擎服务测试 (9套角色场景与参数映射)
├── Test_KnowledgeBaseService.cs   # 国家规范库与审查意见条目服务测试 (GB 50016等强条、手动区分)
├── Test_ConfigAndSettings.cs      # 系统配置与参数管理测试 (问题等级、极速模式状态)
├── Test_UI_UX_Consistency.cs      # UI/UX 规范性测试 (彻底清除浮夸字眼、外框尺寸锁定460x400、专业置顶)
├── RunAllTests.ps1                # 一键运行测试的 PowerShell 脚本
└── README.md                      # 测试说明文档
```

---

## 运行方式

### 方式 1：PowerShell 一键执行（推荐）
在终端中进入项目根目录或 `TEST` 目录执行：
```powershell
pwsh TEST/RunAllTests.ps1
```
或者在当前控制台直接执行：
```powershell
.\TEST\RunAllTests.ps1
```

### 方式 2：通过 dotnet CLI 编译与运行
```powershell
# 编译测试项目
dotnet build -c Release TEST/CadAutoCloudNote.Tests.csproj

# 运行测试
.\build\Tests\CadAutoCloudNote.Tests.exe
```

---

## 测试套件覆盖范围

1. **水平托线紧凑收笔几何算法 (`Test_LandingLengthGeometry.cs`)**：
   - 验证模式 2 下，水平托线精确基于 `triW / 2 + textGap + titleWidth + 0.2 * textHeight` 计算收笔，紧贴文字右侧边缘；
   - 杜绝早期版本中托线超长延伸（图 1 现状）；
   - 测试中文、英文、数字、混合文本以及异常微小字高的边界保护。

2. **极速批注快捷短语库服务 (`Test_RapidPhraseService.cs`)**：
   - 校验内置 60+ 条高频快捷短语的加载完整度；
   - 验证建筑、结构、给排水、暖通、电气、总图各专业的筛选隔离；
   - 验证新增自定义短语及自动持久化；
   - 验证使用频次自增与 TOP 高频推荐排序。

3. **工程场景模板引擎服务 (`Test_ProjectTemplateService.cs`)**：
   - 验证 9 套批注使用对象与工程角色模板（设计自校/内部校对、专业负责人/专业审核、项目审图/专审、施工图会审、设计变更、监理通知、现场交底、竣工验收/竣工图、BIM 管综协同）；
   - 验证专属图层、前缀（如 `AUD-`、`CHK-`）、专用颜色及打印控制（如内部校对强制不打印）；
   - 验证 `CurrentTemplate` 动态联动。

4. **国家规范库与审查意见服务 (`Test_KnowledgeBaseService.cs`)**：
   - 验证建筑防火《GB 50016-2014》、结构通用《GB 55008-2021》等国家规范条文；
   - 验证国标条文与用户手动录入意见的逻辑属性区分。

5. **系统配置与参数管理 (`Test_ConfigAndSettings.cs`)**：
   - 验证问题等级（一般/重要/紧急）字符串与枚举的双向解析；
   - 验证极速免弹窗模式参数联动。

6. **UI / UX 规范性与全版本一致性 (`Test_UI_UX_Consistency.cs`)**：
   - 静态分析 XAML 与源码，确保“智能质检”、“AI智能联想”等浮夸字眼 100% 彻底清除；
   - 验证设置视窗外框几何物理像素紧凑（460x400 NoResize，消除巨型遮挡）；
   - 验证 WPF 与 WinForms 双端架构一致：专业胶囊组置顶、标题可下拉速选与手动键入。
