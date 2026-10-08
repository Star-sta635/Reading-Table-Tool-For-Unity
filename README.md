<div align="center">

# Reading Table Tool For Unity

**从 Excel 填写 → 导出校验 → 代码生成 → 运行加载 → 编辑器调试，全链路重新设计的 Unity 配置表工作流**

[![Unity](https://img.shields.io/badge/Unity-2021%2B-black)](https://unity.com)
[![Language](https://img.shields.io/badge/Language-C%23-blue)]()
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

[特性](#-特性) · [快速开始](#-快速开始) · [Excel模板规范](#-excel-模板规范) · [TBL数据格式](#-tbl-二进制格式) · [编辑器工具](#%EF%B8%8F-编辑器配套) · [实战案例](#-实战案例) · [FAQ](#-faq) · [English](#english)

</div>

---

## 为什么需要它

传统 Unity 配置表流程里，这些坑你大概率都踩过：

- Excel 想怎么填就怎么填，导出工具不校验，错误只在运行到那行代码时才爆炸；
- 策划删了个资源忘了改表，直到打包后某个道具路径为空；
- 想在配置里存一组 Id，只能填 `1001;1021;1031`，运行时 `Split(';')` 到处飞；
- 表结构和代码版本不一致，读出来的数据**静默错位**，查一晚上不知道哪错了；
- 改一行配置，重启一次游戏。

本工具把校验、类型安全、版本一致性检查和热重载做进了整条链路——**错误在导出时拦截，而不是在玩家手机上**。

## ✨ 特性

- 📋 **标准化 Excel 模板** —— 7 行表头、全下拉选择、自带说明页，策划零学习成本
- ✅ **导出期校验** —— 跨表外键 `ref()`、范围 `range()/len()`、跨字段比较 `gt/ge/lt/le`、KEY 唯一性、必填空值定位到单元格
- 🧬 **强类型系统** —— `list[T]` / `map[K|V]` / `vector2/3/4` / `datetime` 原生二进制序列化，运行时拿到的就是 `List<int>`，告别字符串解析
- 🔒 **TBL 数据格式** —— 魔数 + 版本号 + **schema hash** + 行数四重校验，数据与代码版本错配加载即报错，杜绝静默错位
- 🧱 **partial 生成代码** —— `Tab_/TabMgr_/TableManager` 全部 partial，业务扩展文件**重新导出永不覆盖**
- 🚀 **灵活加载** —— Resources / 磁盘（Android 自动 UnityWebRequest）/ 自定义 fetcher（一行 lambda 接 Addressables、YooAsset）；并行加载 + 后台线程解析不卡主线程
- 🔄 **双端一表** —— 每列 `cs/c/s` 端标记，一份 Excel 同时服务客户端与服务端
- 🛠 **编辑器配套** —— 运行中热重载、表数据浏览器、资源路径存在性校验、资源路径批量复制

## 🚀 快速开始

**1. 导入 Unity 配套包**

把 `UnityPackage/DExportExcels` 整个目录拷进 Unity 工程的 `Assets/` 下（任意子目录），自带运行时读取器与编辑器菜单。

**2. 启动导表工具**

Unity 菜单 `读表工具/打开导表工具 (Ctrl+Shift+T)`，首次选择本仓库 `ExportExcels/Tools/ExportExcels/TableExport.exe`。工具会在 Excel 目录不存在时**自动生成标准模板**（含类型下拉与说明页）。

**3. 配置导出路径**

首次运行自动生成 `projectConfig.json`（参考 `ExportExcels/projectConfig.example.json`）：

```json
{
  "is_client": true,
  "path_excel": "./Tables",
  "path_data": "./YourUnityProject/Assets/Game/Download/DataTable",
  "path_script": "./YourUnityProject/Assets/Scripts/DataTable/Create",
  "is_cretescript": true,
  "async_style": "task"
}
```

**4. 填表 → 导出 → 使用**

导出后生成 `Tab_表名.cs`（实体）+ `TabMgr_表名.cs`（管理器）+ `TableManager.cs`（总入口）：

```csharp
// 游戏启动时加载（示例：接 Addressables，见 TableManager.Ext 模式）
await TableManager.LoadAllFromAddressablesAsync();

// 按 KEY 取一行（查无此键自动报错定位）
Tab_Sys_Role role = TableManager.Instance.Sys_Role.GetDataById(2001);

// 复杂类型原生支持，无需 Split
foreach (int effectId in gift.EffectId) { ... }   // List<int>
```

## 📋 Excel 模板规范

每张表一个 `.xlsx`，含 `TableData`（数据）、`Types`（类型字典，隐藏）、`说明`（文档）三个 Sheet：

| 行 | 表头 | 说明 |
|---|---|---|
| 1 | 描述 | 给人看的注释 |
| 2 | **端** | `cs`=客户端+服务端 / `c`=仅客户端 / `s`=仅服务端 / **留空=整列不导出**（注释列） |
| 3 | **类型** | 下拉选择，84 种 |
| 4 | **字段名** | 自动成为 C# 字段名 |
| 5 | **是否允许Null** | `true`=允许留空（导出为类型默认值），`false`=必填 |
| 6 | **规则** | 校验规则，空格分隔可组合多条 |
| 7 | **Key** | 标记主键列；单 KEY 走字典查询，复合 KEY 走列表查询 |
| 8+ | 数据 | A 列 `#` 开头为注释行，全空行自动跳过 |

**类型系统**：`bool / byte / short / int / long / float / double / string / datetime / vector2 / vector3 / vector4 / list[标量] / list[vector] / map[K|V]`

**规则示例**：

```
ref(Item)          该列值必须存在于 Item 表（跨表外键，ref()=引用本表，list 列逐元素校验）
range(1,99)        数值范围
len(1,20)          字符串长度范围
gt(StartTime)      本列必须大于 StartTime 列（同表跨字段比较）
组合示例: ref(Item) range(1,99)
```

## 🔒 TBL 二进制格式

每表一个 `.bytes`，文件头 14 字节：

```
偏移   大小   内容
0      4     魔数 "TBL\0"
4      2     格式版本 (u16)
6      4     schema hash (u32)   ← Excel 结构一变即变
10     4     行数 (i32)
14     ...   行数据（DynamicPacket 读取，string 为 i32 长度前缀 + UTF8）
```

**schema hash 是灵魂**：数据文件与生成代码不匹配时，加载即报错并明确提示重新导出——旧方案里"错位读出全是脏数据"的事故从此绝迹。

## 🛠️ 编辑器配套

| 菜单 | 功能 |
|---|---|
| `读表工具/热重载配置表 (Ctrl+Shift+R)` | **游戏运行中**刷新全部表数据，不用重启 |
| `读表工具/表数据浏览器` | 左侧表列表（带行数）+ 右侧数据表格 + 关键字过滤，目录自动扫描 |
| `读表工具/校验路径列` | 一键扫描全表 `Assets/` 开头的字符串字段，检查资源是否存在 |
| `Assets/读表工具/复制资源路径` | 右键批量复制资源路径为 Excel 可粘贴格式（四种格式） |

## 📦 仓库结构

```
Reading-Table-Tool-For-Unity/
├── ExportExcels/
│   ├── Tools/
│   │   ├── ExportExcels/
│   │   │   └── TableExport.exe        导表工具 (Ctrl+Shift+T 选择它)
│   │   └── DsvnVserion/               SVN 版本号联动脚本（可选）
│   └── projectConfig.example.json     导出路径配置模板
└── UnityPackage/
    └── DExportExcels/                 整个文件夹拷入 Unity 工程即用
        ├── DynamicPacket.cs           运行时二进制读取器（必需）
        ├── IDynamicData.cs
        ├── note.txt
        └── Editor/                     仅编辑器使用，不打进包体
```

## 📊 实战案例

本工具已在一个开发中的完整项目中完成**全量替换迁移**：

- 18 张旧格式表（3 行表头 `.xls`）自动转换为标准模板（7 行表头 `.xlsx`），结构校验一次通过；
- 约 **190 处**业务代码调用切换，**0 编译错误**；
- Play Mode 实测 18 张表行数与旧数据逐一精确匹配，数据抽查全部一致；
- 随后将 3 个 `string` 列升级为原生 `list[int]/list[string]`，运行时 `Split` 代码全部删除，全程不到半小时。

## ❓ FAQ

**Q: 列标 `c` 和 `cs` 导出的客户端脚本为什么一样？**

符合预期。`c`=仅客户端，`cs`=双端共享——**对客户端而言两者都可见**，所以客户端产物相同；区别在于服务端导出时 `c` 列会被剔除。想验证端裁剪：把某列改成 `s` 重新导出，客户端脚本中该字段应消失。

**Q: schema 不匹配报错怎么办？**

改了 Excel 结构（增删列/改类型）后，必须同时重新导出数据与脚本。工具用 schema hash 强制两者同步，这是特性不是缺陷。

**Q: 业务扩展代码会被覆盖吗？**

不会。所有生成类均为 `partial`，把扩展写在 `Tab_XXX.Ext.cs` / `TabMgr_XXX.Ext.cs` / `TableManager.Ext.cs`，重新导出只覆盖生成文件。

**Q: 想存多个值怎么办？**

直接把类型改成 `list[int]` / `list[string]` / `map[int|string]`，单元格填 `1001;1021;1031`（分号分隔）。运行时拿到即 `List<int>`，无需任何解析代码。

## 📄 License

[MIT](LICENSE)

---

## English

**Reading Table Tool For Unity** — an end-to-end configuration-table pipeline for Unity: a standardized Excel template (7-row typed header with built-in docs), export-time validation (`ref` cross-table foreign keys, `range`/`len`, cross-field comparisons, KEY uniqueness), a strongly-typed binary format (`TBL\0` magic + version + **schema hash** for data/code consistency), generated `partial` C# classes that never overwrite your business extensions, and editor tooling (hot reload while playing, table browser, asset-path validation).

```csharp
await TableManager.LoadAllFromAddressablesAsync();        // or Resources / disk / custom fetcher
var role = TableManager.Instance.Sys_Role.GetDataById(1); // typed dictionary access
foreach (int id in gift.EffectId) { }                     // native List<int>, no string parsing
```

Field-tested: migrated a live project (18 tables, ~190 call sites) with zero compile errors and byte-accurate row-count parity. MIT licensed.

