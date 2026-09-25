# CAD Auto CloudNote 核心业务库 (Core) 架构与目录结构说明

本项目采用 **C# Shared Project（共享项目）** 模式，所有核心业务逻辑集中于 `CadAutoCloudNote.Core` 目录中，直接被各宿主外壳工程（R17 ~ R27，覆盖 AutoCAD 2007 到 AutoCAD 2027）以源码级形式编译引用，杜绝跨版本程序集依赖冲突与运行时类型漂移。

---

### 1. 核心根文件

- **`AppConstants.cs`**: 定义全局静态常量（套件品牌名称 `InkVerse`、版本号 `v0.1.0`、注册表安装路径、XData RegApp 注册键名 `CAD_AUTOCLOUDNOTE_APP`、NOD 字典根节点键名）。
- **`PluginMain.cs`**: 插件生命周期总控入口（实现 `IExtensionApplication`），集成 Win32 `SetDllDirectory` 路径注入、非受控资源安全清理、`AssemblyResolve` 托管程序集精准探针以及反应器总线初始化。
- **`Commands.cs`**: 极薄的命令注册分发层（仅声明 `[CommandMethod]` 并委派至各领域服务与 UI 中枢，杜绝在命令方法中堆砌业务脏代码）。
- **`.shproj` & `.projitems`**: Visual Studio Shared Project 编译项清单。

---

### 2. 子模块架构与功能职责

#### `Config/` (配置管理模块)
- **`NoteSettings.cs`**: 全局参数模型，包含图层名、线宽、弧长凸度系数、出图非打印开关、**【极速免弹窗批注与快捷短语配置】(`EnableSimpleNoteMode`, `RapidDefaultPhrase`, `EnableRapidCmdKeySelection`)**、**【工程角色模板】(`CurrentTemplateId`)**、**【高级联动】严重等级与颜色联动映射表 (`EnablePriorityColorLink`)**、**常用审查意见快捷词典 (`QuickReviewSnippets`)**、团队审查成员名单 (`TeamMembers`) 及恢复系统默认方法 `ResetDefaults()`。
- **`ConfigManager.cs`**: 本地零依赖 JSON 配置存取引擎，负责 `%AppData%\Roaming\InkVerse\CadAutoCloudNote\settings.json` 的安全反序列化与持久化。

#### `Jigs/` (图面动态交互绘制引擎)
- **`RectCloudJig.cs`**: 交互式拉框两点绘制闭合矩形云线，动态按步长计算外凸圆弧与凸度。
- **`PolygonCloudJig.cs`**: 交互式任意顶点多边形云线生成引擎，内置 Shoelace 算法自适应顶点顺逆时针判定，确保圆弧向外隆起且拐角自然闭合。
- **`FreehandCloudJig.cs`**: 徒手连续轨迹追踪云线生成引擎。
- **`LeaderPlacementJig.cs`**: 引线与标注文字动态交互 Jig，支持实时包围盒投影、引线折角、箭头三角形方向计算；在极速模式下支持命令行关键字 **`S`**（单键切词）与 **`T`**（快速录入自定义文字）实时刷新。

#### `Models/` (领域模型)
- **`NoteRecord.cs`**: 批注主体业务元数据（XRecord 业务真源），维护 NoteId、序号、标题、内容、生命周期状态、专业、优先级、创建责任人、工程场景模板标识 (`TemplateId`) 及专有元数据字典 (`MetadataFields`)。
- **`CloudEntity.cs`**: 云线关联图元数据（句柄集合 Handle、包围盒、凸度与样式参数）。
- **`TextEntity.cs`**: 标注文字图元数据（多行文字句柄、折角引线句柄、放置坐标、字高）。
- **`NoteReply.cs`**: 多轮工程审查对话记录。
- **`AuditEntry.cs`**: 链式不可篡改 SHA256 哈希审计条目。
- **`DrawingInstance.cs`**: 图纸指纹模型。
- **`ReviewPackageManifest.cs`**: 离线审查包清单模型。
- **`BatchReviewSummary.cs`**: 批量多图合并分析模型。
- **`SemanticSearchConfig.cs`**: 规范库语义向量与模糊匹配参数配置。

#### `Protocols/` (协议与状态机)
- **`NoteLifecycleState.cs`**: 5 级生命周期状态机枚举（Draft, Pending, Resolved, Rejected, Closed）及优先级枚举（Low, Normal, High, Critical）。
- **`CapacityPolicy.cs`**: NOD 字典分卷策略（默认单卷 50 条隔离，杜绝图纸字典体积过大）。

#### `Interfaces/` (跨版本抽象接口)
- **`INoteRepository.cs`**: 数据持久化仓储接口。
- **`INoteUIProvider.cs`**: UI 宿主解耦调度接口。
- **`ICadPlatformAdapter.cs`**: CAD 平台适配接口。
- **`IXDataAccessor.cs`**: XData 唯一访问接口。
- **`ICloudDrawer.cs`**: 云线几何绘制接口。

#### `Reactors/` (反应器与数据一致性引擎)
- **`ReactorManager.cs`**: 按 Database 隔离的反应器总控。监听 `ObjectAppended`、`ObjectModified`、`ObjectErased`。集成级联安全删除逻辑（删除引线、三角形箭头、文字或云线任一部分均触发整条批注清理与看板移除）；集成 `Application.Idle` 监听，彻底消除夹点拖拽（Grip Edit）的画面残影与状态脱节。
- **`DocumentState.cs`**: 单文档上下文与内部事务深度计数器。
- **`PendingTask.cs` / `PendingTaskConflictResolver.cs`**: 延迟队列与图元增删冲突消解矩阵。
- **`CloneDetection.cs`**: 图元克隆、成套复制识别与标记剥离。

#### `Services/` (核心领域服务)
- **`NoteService.cs`**: 批注增删改查主服务；提供 `CompactDrawingDatabase(Database db)` 方法，一键扫描清理孤儿 XRecord 与无效扩展字典；支持绑定当前场景模板并自动生成前缀。
- **`RapidPhraseService.cs`**: 高频快捷短语库引擎，完整收录建筑、结构、给水排水、电气、暖通空调、通用及销项回复等 7 大类 60+ 条行业高频短语；支持专业分类筛选、使用计数排序、自定义词条 CRUD 与 JSON 本地化存储。
- **`ProjectTemplateService.cs`**: 9 套批注使用对象与工程角色模板引擎（设计自校/内部校对、专业负责人/专业审核、项目审图/专审、施工图会审、设计变更、监理通知、现场交底、竣工验收/竣工图、BIM 管综协同），提供一键应用图层、颜色、前缀、线宽与打印控制。
- **`CurveToCloudService.cs`**: 曲线/几何图元转云线算法引擎，支持 Polyline、Circle、Arc、Ellipse、Spline 自适应离散化与外凸圆弧重构。
- **`ReplyService.cs`**: 批注多轮审查回复与生命周期流转。
- **`ExportService.cs`**: 原生 Excel（XML Spreadsheet 2003）与标准 CSV 格式导出；提供 `ExportTemplateReport` 支持 9 套工程角色模板专有差异清单、带定制表头和列宽的标准 Excel 电子表格生成。
- **`SummaryTableService.cs`**: 图纸模型空间 CAD Table 汇总表绘制与图面表格生成。
- **`KnowledgeBaseService.cs`**: 现行国家建筑与工程强制性条文规范库 CRUD 与快速检索。
- **`AuditService.cs`**: SHA-256 链式防篡改哈希审计与完整性追溯。
- **`SemanticSearchService.cs`**: Bigram Dice-Sørensen 语义相似度模糊匹配引擎。
- **`CompareLinkService.cs`**: AutoCAD 2019+ 原生 COMPARE 修订云线关联分析。
- **`TianzhengScaleHelper.cs`**: 天正建筑图纸比例自适应读取与适配。
- **`ReviewPackageService.cs`**: `.cloudnotepkg` 离线审查包打包与坐标校准。
- **`BatchReviewService.cs`**: 多图纸后台只读扫描与合并审查。
- **`Logger.cs`**: 本地滚动异常审计日志。

#### `Helpers/` (底层工具网关)
- **`XDataHelper.cs`**: XData 读写统一网关（内置自动注册 RegApp 保护）。
- **`XRecordHelper.cs`**: XRecord 4+2 分片存储与 50 条自动分卷拓扑实现，全面支持角色模板标识 `TemplateId` 持久化。
- **`ScaleHelper.cs`**: 坐标系、比例因子与图框自适应换算。
- **`IconHelper.cs`**: 全链路图标资源加载网关（同时服务于 WPF ImageSource 与 WinForms Icon）。

#### `UI.Wpf/`, `UI.WinForm/` & `UI/` (界面呈现与调度)
- **`NoteUIProvider.cs`**: 统一 UI 调度中枢，根据 AutoCAD 版本动态派发 WPF 或 WinForms 界面。
- **`ConfigWindow.xaml` / `.xaml.cs`**: 现代化双色智能自适应 WPF 全局设置视窗（针对 AutoCAD 2013~2027，自动跟随宿主深色工程/浅色明亮主题），包含五大板块（对象与规则、绘图与样式、极速与短语、规范知识库、快捷与系统）、严重等级颜色联动下拉、高频快捷短语库管理、9 套工程角色模板切换与专属差异报表导出、以及一键健康体检。
- **`ConfigForm.cs`**: 针对 AutoCAD 2007~2012（.NET 3.5）的双色智能自适应 WinForms 全局设置窗体。
- **`PaletteManager.cs` / `NotePaletteControl`**: PaletteSet 非模态协同管理看板，支持双击缩放对焦、多维组合过滤与批量导出。
- **`NoteEditDialog.xaml` / `NoteEditForm.cs`**: 批注新建与编辑对话框。

---

### 3. 多版本宿主适配层 (`src/`)

包含从 `CadAutoCloudNote.R17` 到 `CadAutoCloudNote.R27` 共 11 个独立外壳工程：
- **R17**：AutoCAD 2007 ~ 2009 (.NET 3.5 / x86 / AnyCPU)
- **R18**：AutoCAD 2010 ~ 2012 (.NET 3.5 / x86 / AnyCPU)
- **R19**：AutoCAD 2013 ~ 2014 (.NET 4.0 / x64)
- **R20**：AutoCAD 2015 ~ 2016 (.NET 4.5 / x64)
- **R21**：AutoCAD 2017 (.NET 4.6 / x64)
- **R22**：AutoCAD 2018 (.NET 4.6 / x64)
- **R23**：AutoCAD 2019 ~ 2020 (.NET 4.7 / x64)
- **R24**：AutoCAD 2021 ~ 2024 (.NET 4.8 / x64)
- **R25**：AutoCAD 2025 (.NET 8.0-windows / x64)
- **R26**：AutoCAD 2026 (.NET 8.0-windows / x64)
- **R27**：AutoCAD 2027 (.NET 10.0-windows / x64)
