# CAD Auto CloudNote 开发者贡献指南 (Contributing Guide)

感谢您对 **CAD Auto CloudNote** 开源项目的关注与支持！本项目是一个由工程一线技术爱好者在业余时间独立开发、免费分享的开源小工具，我们非常欢迎来自社区的设计师、审图工程师与二次开发同行共同参与改进。

---

## 1. 提交 Issue 指南

在提交 Issue 前，建议您：
1. **搜索现有 Issue**：检查该问题或需求是否已被提出或解决；
2. **区分类型**：
   - 发现图元脱节、文字乱码、异常报错等问题，请使用 **Bug 反馈** 模板；
   - 提出新增工程角色、快捷短语补充或交互改进，请使用 **功能建议** 模板；
3. **提供关键运行环境信息**：
   - AutoCAD 版本（如 2018 / 2021 / 2025）；
   - 宿主操作系统（Windows 10 / 11 64位）；
   - 当前使用的工程角色模板与操作步骤。

---

## 2. 代码贡献核心原则

为了保证插件在 AutoCAD 2007~2027 跨度长达 20 年的 11 代宿主中稳定无歧义运行，请遵守以下设计铁律：

### 2.1 铁律与架构约定
1. **纯本地与零臃肿依赖**：
   - 严禁引入需要联网鉴权的第三方 SDK；
   - Excel 导出采用原生的 XML Spreadsheet 2003 与 NPOI 基础库，禁止调用笨重的 Office Interop COM 组件。
2. **COLORTHEME 双色主题对齐**：
   - 所有新建或修改的 WPF 与 WinForms 界面，必须接入 `COLORTHEME` 变量感知机制；
   - 必须在深色工程模式与浅色高对比模式下分别测试通过，文字与边框严禁出现白底白字或黑底黑字。
3. **原生撤销 (Undo/Redo) 事务保护**：
   - 任何涉及图元增删改的操作，必须封装在带有 `Transaction.StartUndoMark()` 的单一事务中；
   - 反应器回调中严禁开启新事务，改用 `Application.Idle` 或延迟任务队列。
4. **单元测试必须全绿**：
   - 提交 PR 前必须在控制台运行 `dotnet run --project TEST\CadAutoCloudNote.Tests.csproj`，确保所有测试用例 100% PASS。

---

## 3. Pull Request (PR) 流程

1. **Fork 本仓库** 并拉取到您的本地；
2. 创建专属特性分支：
   ```bash
   git checkout -b feature/your-feature-name
   # 或
   git checkout -b fix/issue-description
   ```
3. 遵循 C# 官方代码规范编写代码，保持注释务实清晰；
4. 编译测试全部 11 个宿主工程：
   ```powershell
   dotnet build CadAutoCloudNote.slnx -c Release
   ```
5. 运行独立自动化测试：
   ```powershell
   dotnet run --project TEST\CadAutoCloudNote.Tests.csproj
   ```
6. 提交 commit（推荐语义化提交，如 `fix(jig): 修复引线零交叉对齐算法`）；
7. 向主仓库的 `main` 分支发起 Pull Request，并在描述中详述修改内容与测试结果。

---

再次感谢您的付出，愿我们一起把这个 CAD 云线审图小工具打磨得更顺手、更称心！
