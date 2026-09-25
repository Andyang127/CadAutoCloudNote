## 关联的 Issue
Closes #

## 修改内容说明
简述本次修改的目的、设计方案及影响模块：
- 

## 遵循的规范与自检清单
请勾选确认以下事项：
- [ ] 代码遵循 C# 编码规范，无多余调试打印与脏代码；
- [ ] 新建或修改的 UI 界面适配 **AutoCAD COLORTHEME 智能感知双色主题（深色/浅色测试正常）**；
- [ ] 撤销操作已封装进事务，未破坏原生 Undo/Redo 栈；
- [ ] 运行自动化测试全绿（`dotnet run --project TEST\CadAutoCloudNote.Tests.csproj`）；
- [ ] 编译 11 代宿主外壳工程 0 警告 0 错误（`dotnet build CadAutoCloudNote.slnx -c Release`）；
- [ ] 相关文档（README.md / Readme.html 等）已同步更新。
