# AutoCAD SDK 本地引用依赖说明 (Reference Assemblies)

本项目遵循 **100% 极纯源代码** 开源原则，为尊重 Autodesk® 专有商业软件知识产权，不随源码仓库分发任何私有二进制库（`.dll`）。

各版本工程编译所需的 AutoCAD .NET API 引用库目录结构如下，开发者克隆仓库后，可从本地对应版本的 AutoCAD 安装目录或 Autodesk 官方 SDK (ObjectARX) 中复制对应的引用程序集：

---

### 目录与对应版本配置

| 目录路径 | 目标 CAD 版本 | 运行时架构 | 核心引用依赖 |
| :--- | :--- | :--- | :--- |
| `Libs/AutoCAD/Net2.0_2007-2009/` | AutoCAD 2007 ~ 2009 | .NET 2.0 / 3.5 | `AcDbMgd.dll`, `AcMgd.dll` |
| `Libs/AutoCAD/Net3.5_2010-2012/` | AutoCAD 2010 ~ 2012 | .NET 3.5 | `AcDbMgd.dll`, `AcMgd.dll` |
| `Libs/AutoCAD/Net4.0_2013-2014/` | AutoCAD 2013 ~ 2014 | .NET 4.0 / 4.5 | `acdbmgd.dll`, `acmgd.dll`, `accoremgd.dll` |
| `Libs/AutoCAD/Net4.5_2015-2016/` | AutoCAD 2015 ~ 2016 | .NET 4.5 | `acdbmgd.dll`, `acmgd.dll`, `accoremgd.dll` |
| `Libs/AutoCAD/Net4.6_2017/` | AutoCAD 2017 | .NET 4.6 | `AcDbMgd.dll`, `AcMgd.dll`, `AcCoreMgd.dll` |
| `Libs/AutoCAD/Net4.6_2018/` | AutoCAD 2018 | .NET 4.6 | `AcDbMgd.dll`, `AcMgd.dll`, `AcCoreMgd.dll` |
| `Libs/AutoCAD/Net4.7_2019-2020/` | AutoCAD 2019 ~ 2020 | .NET 4.7 | `AcDbMgd.dll`, `AcMgd.dll`, `AcCoreMgd.dll`, `AdWindows.dll` |
| `Libs/AutoCAD/Net4.8_2021-2024/` | AutoCAD 2021 ~ 2024 | .NET 4.8 | `acdbmgd.dll`, `acmgd.dll`, `accoremgd.dll`, `AdWindows.dll` |
| `Libs/AutoCAD/Net8.0_2025-2026/` | AutoCAD 2025 ~ 2026 | .NET 8.0 | `AcDbMgd.dll`, `AcMgd.dll`, `AcCoreMgd.dll`, `AdWindows.dll` |
| `Libs/AutoCAD/Net10.0_2027/` | AutoCAD 2027 | .NET 10.0 | `AcDbMgd.dll`, `AcMgd.dll`, `AcCoreMgd.dll`, `AdWindows.dll` |

> [!TIP]
> 开发者亦可直接通过 NuGet 引入 Autodesk 官方发布的 `AutoCAD.NET` 系列包进行依赖还原。
