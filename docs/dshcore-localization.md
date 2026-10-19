# LPE × DSHCore 模式本地化

适用：**LPE 1.2.1+**、**DSHCore 0.4.0+**、**MVZ2 0.7.X**。

[English](dshcore-localization.en.md) | 简体中文

本文说明 LPE 检测到 DSHCore（`qoznos.mvz2.core`）时的本地化行为、优先级，以及如何用语言包覆盖 DSHCore Mod 的文本。

## 1. 两种模式

| 模式 | 触发条件 | LPE 的行为 |
| --- | --- | --- |
| 独立模式 | 未安装 DSHCore | 只把 Mod 语言包注入游戏的 `catalogProviders`（内置 provider 之前） |
| DSHCore 模式 | 检测到 `qoznos.mvz2.core` | 除注入外，还会调用 `UiOptions.DeclareMod`，并把 Mod 译文**镜像**进 `UiLocale` |

镜像入口：

- `LanguagePackEnhanceService.RegisterTranslations` → `DshCoreIntegration.MirrorTranslations`
- `ModLanguagePackRegistry.Register` → `DshCoreIntegration.MirrorLanguagePack`（解析内嵌 `.lang.json`，支持 context）

镜像的目的是让 DSHCore 的界面（`UiLocale.Get`、设置页、`AddonPage` 等）也能读到 Mod 译文。

## 2. 优先级

LPE 存在时（含 DSHCore 模式）：

```
1. LPE 链路：第三方语言包 → Mod 自带语言包
2. DSHCore UiLocale 外部文件：BepInEx/config/DSHCore/lang/<lang>.txt
                                  BepInEx/config/DSHCore/lang/<lang>.d/*.txt
3. DSHCore UiLocale 代码表：UiLocale.Add / UiLocale.Table（含 LPE 镜像）
4. 游戏内置语言包
5. 原文
```

也就是说：

- 玩家导入的**第三方语言包可以覆盖 DSHCore Mod 的文本**；
- 玩家手改的 `config/DSHCore/lang/*.txt` 仍在 `UiLocale` 代码表之上，但**低于** LPE 链路；
- 没有 LPE 时，DSHCore 自己的顺序是「外部文件 > 代码表 > 语言包」，第三方语言包压不过已登记的 Mod 文本。

## 3. LPE 如何覆盖 UiLocale

DSHCore 的 `UiLocale.Apply()` 在 `LanguageManager` 上挂 **4 个 prefix**（普通/上下文 × 原生/托管重载），命中就跳过原方法。

LPE 采用 **postfix**，而不是 prefix，原因：

> HarmonyX 下所有 prefix 默认都会执行，返回 `false` 只跳过原方法，**不会**跳过其它 prefix。
> 用高优先级 prefix + `return false` 无法阻止 `UiLocale` 的 prefix 继续运行。

所以 LPE 在同样的 4 个重载上挂 postfix，在 `UiLocale` 之后运行：

- 只查**内置 provider 之前**的条目（第三方语言包 + Mod 语言包）；
- 命中则覆盖 `translated` 与 `__result`；
- 未命中则保留原结果；
- 带参数的查找（`args.Length > 0`）直接放行，与 `UiLocale` 行为一致。

实现：`Patches/LpePriorityPatch.cs`（`LpePriorityPatch.Apply()`，由 `Plugin.Load()` 调用）。

## 4. 用语言包覆盖 DSHCore Mod 文本

MVZ2 0.7.X 的语言包就是资源包，最小结构：

```
MyPack/
├─ pack.json
├─ localization.json
└─ assets/
   └─ myns/catalogs/
      ├─ en-US/*.mo
      └─ zh-Hans/*.mo
```

要点：

- `localization.json` 的 `catalogs` 路径**相对 `assets/`**；
- `.mo` 必须带 header entry（`Content-Type: text/plain; charset=UTF-8`）；
- 带上下文的条目用 `\u0004` 连接，例如 `music.name\u0004秋意阑珊之瀑`；
- 放进游戏 `resource_packs/` 并在资源包页启用。

仓库内的测试包：`~/MVZ2Mod开发/ModTextTestPack/`（译文一律带 `(ModTest)` 后缀，便于区分来源）。

## 5. 外部译文文件

DSHCore 的外部文件仍在 `UiLocale` 内生效：

- `BepInEx/config/DSHCore/lang/<lang>.txt`
- `BepInEx/config/DSHCore/lang/<lang>.d/*.txt`

格式与指令见 DSHCore 的 `UI_GUIDE.md` §11 与 `/dshtrans`。按当前策略，它们**低于** LPE 链路。

## 6. 限制与注意

- **`UiLocale.Get` 不查语言包**：如果某处代码直接用 `UiLocale.Get(...)`（例如 `AddonPage.EntryLabelText` / `TitleText`），语言包覆盖不了，它只读 DSHCore 的表。
- **带参数的查找不覆盖**：LPE postfix 与 `UiLocale` 一样只处理不带参数的查找。
- **每条文本查找都会多走一次 provider 链**（postfix）。provider 数量通常很少，但属于额外开销。
- **内置 provider 不参与覆盖**：LPE postfix 只查内置之前的 provider，避免把游戏原文也顶掉。
- **DSHCore 未安装时**：postfix 仍然挂载，结果与原本的 provider 顺序一致，行为不变。

## 7. 自检

启动日志：

```
[LPE] 优先级 postfix 已挂载 4/4 处。
```

不是 `4/4` 时说明某个重载没找到，检查 MVZ2 版本与 `LanguageManager` 签名。

DSHCore 侧会打印：

```
[UiLocale] 已挂进游戏文本查找链（补丁 4/4 处）
```

## 8. 源码索引

| 位置 | 作用 |
| --- | --- |
| `Core/DshCoreIntegration.cs` | 检测 DSHCore、`DeclareMod`、镜像译文 |
| `Patches/LpePriorityPatch.cs` | 4 个 postfix，实现 LPE > UiLocale |
| `Plugin.cs` | 调用 `DshCoreIntegration.Initialize()` 与 `LpePriorityPatch.Apply()` |
| `Core/LanguagePackInjector.cs` | 注入 Mod 语言包到内置 provider 之前 |
| `Core/ModLanguagePackLoader.cs` | `.lang.json` → `ResourcePack`（context 用 `\u0004`） |
