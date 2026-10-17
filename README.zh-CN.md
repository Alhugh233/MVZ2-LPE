语言：[English](README.md) | 简体中文

# LanguagePackEnhance

![Vibe Coding](https://img.shields.io/badge/Vibe%20Coding-100%25-blueviolet)

面向 **MVZ2 (Minecraft VS Zombies 2) / MVZ2Expand** Mod 的 BepInEx 6 (IL2CPP) 本地化框架。

> **从 v1.1 起，本项目适配 MVZ2 0.7.X 底层。** 对 EXPAND 等改版的支持将在其源码更新后于后续版本恢复。

它让其它 Mod 可以把自己的文本接入游戏的本地化体系、以**内嵌语言包**随 Mod 提供译文，并允许玩家用第三方语言包覆盖 / 扩展。

## 目录

- [概述](#概述)
- [Vibe Coding](#vibe-coding)
- [工作原理](#工作原理)
- [给 Mod 作者的 API](#给-mod-作者的-api)
- [语言包格式](#语言包格式)
- [音乐室元数据](#音乐室元数据)
- [安装](#安装)
- [从源码构建](#从源码构建)
- [兼容性](#兼容性)
- [致谢](#致谢)
- [许可](#许可)

## 概述

LanguagePackEnhance（LPE）是一个**给其它 Mod 用的框架**，而不是内容 Mod。它提供：

- 一套简洁的 API，供 Mod 注册自己的译文；
- 与游戏既有语言包系统的自动整合（因此玩家导入的语言包也能翻译 / 覆盖 Mod 文本）；
- 在游戏语言包页上合并为**单独一条「Mod 自带语言包」**条目。

它拆成**两个程序集**（框架 + 瘦 API）：

- `LanguagePackEnhance.dll` —— BepInEx 插件（实现）。
- `LanguagePackEnhance.API.dll` —— **瘦 API**（`LanguagePackEnhanceApi`、`TranslationEntry`、`ILanguagePackEnhanceService`），不依赖游戏与 BepInEx。其它 Mod **只针对它编译**。仓库：[MVZ2-LPEAPI](https://github.com/Alhugh233/MVZ2-LPEAPI)。

每个字符串的查找优先级：**导入的第三方语言包 > Mod 自带语言包 > 游戏内置 > 原文**。

## Vibe Coding

本项目采用 **Vibe Coding** 开发：代码与文档主要由向 AI 助手提问生成，部分代码为 AI 生成。请在使用前自行审查与测试。

## 工作原理

- LPE 挂 `LanguageManager.ValidateCurrentLanguage`（它在 `InitLanguagePacks` / `ReloadLanguagePacks` 末尾同步调用），把每个已注册 Mod 的 `LanguagePack` 注入游戏的 `loadedLanguagePacks`，位置在**第三方包之后、内置包之前**。于是游戏自身的顺序查找天然得到上面的优先级，不需要重写任何翻译逻辑。
- 内置包被移动到最后，从而永远优先级最低。
- 在语言包页上，LPE 注入一条虚拟条目（**「Mod 自带语言包」**），位于**内置包之上**。它像普通条目一样可被选中、可上移/下移，但永远不会排到内置包下面，也没有禁用/删除/导出操作。

## 给 Mod 作者的 API

编译期引用**瘦 API** `LanguagePackEnhance.API.dll`（**不要**打包它，运行时由插件提供），并声明依赖：

```csharp
[BepInDependency("com.mvz2.languagepackenhance")]
```

> 仅编译期引用：用 `Private=false`（或 `compileOnly`）即可，因为 `LanguagePackEnhance.API.dll` 与框架在运行时都在 `BepInEx/plugins/` 中。

### 1. 注册内嵌语言包

把语言包作为 DLL 的**内嵌资源**随 Mod 提供（游戏语言包格式的 `.zip`，或下面的简化 JSON）：

```csharp
// 在插件 Load() 中调用
LanguagePackEnhanceApi.RegisterMod("MyMod", "MyMod.lang.json");
// 或显式指定程序集：
LanguagePackEnhanceApi.RegisterMod("MyMod", typeof(MyPlugin).Assembly, "MyMod.lang.json");
```

格式按内容自动识别：以 `PK` 开头视为 zip，否则视为 JSON。

### 2. 运行时翻译

```csharp
string s = LanguagePackEnhanceApi.Translate("我的设置");
string s2 = LanguagePackEnhanceApi.Translate("已选择 {0} 项", count);
```

`Translate` 等价于游戏的 `LanguageManager._`，按上面的优先级链解析。Mod 也可以直接调用游戏的 `_` / `_p`；两种方式都会查到注入的包。

### 3. 运行时注册译文（用于动态加载的内容）

对运行时才加载的内容（如玩家安装的音乐包），可用编程方式注册译文：

```csharp
LanguagePackEnhanceApi.RegisterTranslations("MyMod", new[]
{
    new TranslationEntry("en-US", "我的设置", "My Settings"),
    new TranslationEntry("en-US", "Bossrush的音乐。…", "Boss Rush theme", "music.description"),
});

// 之后可整体替换该 nsp 的集合，或清空：
LanguagePackEnhanceApi.ClearTranslations("MyMod");
```

- `RegisterTranslations` 会整体替换该 nsp 之前的运行时集合；若游戏已就绪会立即重新注入。
- `TranslationEntry(language, source, translation, context?)`：`context` 为空表示普通文本（游戏 `_`）；非空表示带 context 的查找（游戏 `_p`）。

## 语言包格式

### 简化 JSON

```json
{
  "zh-Hans": { "我的设置": "我的设置" },
  "en-US": {
    "我的设置": "My Settings",
    "已选择 {0} 项": ["{0} item selected", "{0} items selected"],
    "music.description": { "Bossrush的音乐。…": "Boss Rush theme" }
  }
}
```

- 顶层：语言代码 → 条目。
- 条目值为**字符串**表示普通译文；为**字符串数组**表示复数形式。
- 条目值为**对象**时按 **context**（游戏 `_p`）处理：`{ "context": { "原文": "译文" } }`。内部存储的 key 为 `context + "\u0004" + 原文`。

### 游戏语言包 zip

这就是**标准的 MVZ2 语言包格式**：一个 zip（或文件夹），内含 `pack.json` + `assets/<nsp>/<lang>/*.mo`（gettext 目录，支持 `msgctxt`），可选 `sprite_manifest.json` 与贴图。（贴图本地化只支持这种 zip 格式。）

如果你对语言包制作不熟悉，请参考官方模板与教程：**Cuerzor/MVZ2LanguagePack** — <https://github.com/Cuerzor/MVZ2LanguagePack>。

### LPE 特殊之处

对**只是做语言包**的人来说，**没有 LPE 专属格式**——普通 MVZ2 语言包即可，以 mod 原文作 msgid（音乐等元数据用 `msgctxt`，如 `music.*`）。LPE 增加的只是：

- **优先级**：导入的第三方包 &gt; Mod 自带包 &gt; 游戏内置 &gt; 原文。
- 语言包页上一条合并的 **「Mod 自带语言包」** 条目。可被选中、可上/下移，但始终保持在**内置包之上**，且没有禁用/删除/导出操作。
- 可选的 **简化 JSON**（作为 `.mo` 的替代，用于 mod 作者的内嵌语言包）。
- mod 侧 API（`RegisterMod` / `RegisterTranslations` / `Translate`）。

## 音乐室元数据

游戏通过 `_p` 的 context `music.name` / `music.source` / `music.origin` / `music.author` / `music.description` 本地化逐曲元数据，key 为 `context + "\u0004" + 原文`。LPE 注册的包会自动参与这些查找，所以 Mod 只要在这些 context 下提供条目（JSON 的 context 映射，或带 `msgctxt` 的 `.mo`），就能本地化自己的自定义曲目。

## 安装

1. 确认游戏已安装 **BepInEx 6 (IL2CPP)**。
2. 把 `LanguagePackEnhance.dll` 与 `LanguagePackEnhance.API.dll` **两个都**复制到 `BepInEx/plugins/`。
3. 启动游戏。日志出现 `Plugin com.mvz2.languagepackenhance is loaded!` 即安装成功。

接入 LPE 的其它 Mod 需与它一起安装（它们引用的是 `LanguagePackEnhance.API.dll`）。

## 从源码构建

需要 `.NET SDK`（目标 `net6.0`）。

源码分为**两个并列工程**：本仓库（`LanguagePackEnhance`，插件）与 `LanguagePackEnhance.API`（瘦 API）。把它们放在同一层目录：

```
<父目录>/
  LanguagePackEnhance/        （本仓库，插件）
  LanguagePackEnhance.API/    （瘦 API）
```

1. 把游戏 **interop DLL** 放入本仓库的 `interop/` 目录（该目录已在 `.gitignore` 中），或用 `-p:InteropDir=` 指定。
2. 构建（插件的 `ProjectReference` 会一并构建 API 工程，并把 `LanguagePackEnhance.API.dll` 复制到输出）：

```bash
dotnet build
# 或
dotnet build -p:InteropDir=/path/to/interop_mvz2
```

如果 API 仓库的目录名不同（例如 `MVZ2-LPEAPI`），可以显式指定它的工程：

```bash
dotnet build -p:ApiProject=../MVZ2-LPEAPI/LanguagePackEnhance.API.csproj
```

产物：`bin/Debug/net6.0/LanguagePackEnhance.dll` + `LanguagePackEnhance.API.dll`。

### 依赖

- `BepInEx.Unity.IL2CPP`、`BepInEx.PluginInfoProps`（仅插件）
- `LanguagePackEnhance.API`（工程引用）

## 兼容性

- 目标框架：`net6.0`（BepInEx 6 IL2CPP / CoreCLR）。
- 插件 GUID：`com.mvz2.languagepackenhance`。
- 面向 **MVZ2 0.7.X** 适配（interop：0.7.X 的 interop 集）。EXPAND 等改版将在其源码更新到 0.7.X 底层后恢复支持。
- 已在实机上针对 MVZ2 0.7.X 验证。

## 致谢

- **MVZ2（原作）** 作者 Cuerzor — <https://github.com/Cuerzor/MinecraftVsZombies2Unity>
- **MVZ2Expand** 作者 Tr-DESE107 — <https://github.com/Tr-DESE107/MinecraftVsZombies2_EXPAND>
- 框架：BepInEx。
- 插件图标（`Resources/LanguagePackEnhance.icon.png`）：原图取自 **MC大战僵尸2 wiki** — <https://minecraftvszombies2.wiki.gg/zh/>。

## 许可

版权所有 (C) 2026 Alhugh233。

本项目采用 **GNU 通用公共许可证 v3.0** 授权，完整许可证文本原样收录于 [LICENSE](LICENSE)。

随附的瘦 API 程序集 **LanguagePackEnhance.API** 单独采用 **Apache License 2.0** 授权（见 `LanguagePackEnhance.API` 工程）。Apache-2.0 与 GPL-3.0 兼容。

本项目**直接基于两个开源游戏 MVZ2 与 MVZ2Expand**（均为 GPL-3.0）。它是一个通过 BepInEx / Harmony 与游戏互操作的运行时 Mod，不重新分发游戏的程序集。
