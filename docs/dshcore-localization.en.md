# LPE × DSHCore mode localization

Applies to **LPE 1.2.1+**, **DSHCore 0.4.0+**, **MVZ2 0.7.X**.

This document describes how LPE behaves when DSHCore (`qoznos.mvz2.core`) is present,
the lookup priority, and how to override DSHCore mod text with a language pack.

English | [简体中文](dshcore-localization.md)

## 1. Two modes

| Mode | Trigger | LPE behavior |
| --- | --- | --- |
| Standalone | DSHCore is not installed | Injects mod language packs into the game's `catalogProviders` (before the built-in provider) |
| DSHCore mode | `qoznos.mvz2.core` is present | Injects as usual, plus calls `UiOptions.DeclareMod` and **mirrors** mod translations into `UiLocale` |

Mirror entry points:

- `LanguagePackEnhanceService.RegisterTranslations` → `DshCoreIntegration.MirrorTranslations`
- `ModLanguagePackRegistry.Register` → `DshCoreIntegration.MirrorLanguagePack` (parses embedded `.lang.json`, supports context)

The mirroring lets DSHCore UI (`UiLocale.Get`, the settings page, `AddonPage`, etc.) read mod translations too.

## 2. Priority

With LPE present (including DSHCore mode):

```
1. LPE chain: third-party language packs → mod-bundled language packs
2. DSHCore UiLocale external files: BepInEx/config/DSHCore/lang/<lang>.txt
                                      BepInEx/config/DSHCore/lang/<lang>.d/*.txt
3. DSHCore UiLocale code table: UiLocale.Add / UiLocale.Table (including LPE mirrors)
4. Game built-in language packs
5. Original text
```

In other words:

- Player-imported **third-party language packs can override DSHCore mod text**;
- Player-edited `config/DSHCore/lang/*.txt` still sits above the `UiLocale` code table, but **below** the LPE chain;
- Without LPE, DSHCore's own order is "external files > code table > language packs", so third-party packs cannot override registered mod text.

## 3. How LPE overrides UiLocale

`UiLocale.Apply()` attaches **4 prefixes** to `LanguageManager` (plain/particular × native/managed overloads) and skips the original method on a hit.

LPE uses **postfixes** instead of prefixes because:

> In HarmonyX, all prefixes run by default; returning `false` only skips the original method and does **not** skip other prefixes.
> A higher-priority prefix returning `false` therefore cannot stop the `UiLocale` prefix from running.

LPE attaches postfixes to the same 4 overloads, running after `UiLocale`:

- only queries entries **before the built-in provider** (third-party + mod packs);
- on a hit, overrides `translated` and `__result`;
- otherwise keeps the original result;
- parameterized lookups (`args.Length > 0`) are passed through, matching `UiLocale` behavior.

Implementation: `Patches/LpePriorityPatch.cs` (`LpePriorityPatch.Apply()`, called from `Plugin.Load()`).

## 4. Overriding DSHCore mod text with a language pack

In MVZ2 0.7.X, a language pack is a resource pack. Minimal layout:

```
MyPack/
├─ pack.json
├─ localization.json
└─ assets/
   └─ myns/catalogs/
      ├─ en-US/*.mo
      └─ zh-Hans/*.mo
```

Notes:

- `catalogs` paths in `localization.json` are **relative to `assets/`**;
- `.mo` files must contain a header entry (`Content-Type: text/plain; charset=UTF-8`);
- context entries are joined with `\u0004`, e.g. `music.name\u0004秋意阑珊之瀑`;
- put the pack in the game's `resource_packs/` and enable it on the resource pack page.

Test pack in the workspace: `~/MVZ2Mod开发/ModTextTestPack/` (all translations carry a `(ModTest)` suffix so the source is obvious).

## 5. External translation files

DSHCore external files still work inside `UiLocale`:

- `BepInEx/config/DSHCore/lang/<lang>.txt`
- `BepInEx/config/DSHCore/lang/<lang>.d/*.txt`

Format and commands are documented in DSHCore's `UI_GUIDE.md` §11 and `/dshtrans`.
Under the current policy they are **below** the LPE chain.

## 6. Limitations

- **`UiLocale.Get` does not query language packs**: code paths that call `UiLocale.Get(...)` directly (e.g. `AddonPage.EntryLabelText` / `TitleText`) cannot be overridden by a pack; they only read DSHCore's table.
- **Parameterized lookups are not overridden**: the LPE postfix, like `UiLocale`, only handles lookups without arguments.
- **Every text lookup walks the provider chain once more** (the postfix). The provider count is usually small, but it is extra work.
- **The built-in provider is excluded**: the LPE postfix only queries providers before the built-in one, so original game text is never displaced.
- **Without DSHCore**: the postfix is still attached; the result matches the original provider order, so behavior is unchanged.

## 7. Self-check

Startup log:

```
[LPE] 优先级 postfix 已挂载 4/4 处。
```

Anything other than `4/4` means an overload was not found; check the MVZ2 version and the `LanguageManager` signature.

DSHCore logs:

```
[UiLocale] 已挂进游戏文本查找链（补丁 4/4 处）
```

## 8. Source index

| Location | Purpose |
| --- | --- |
| `Core/DshCoreIntegration.cs` | Detect DSHCore, `DeclareMod`, mirror translations |
| `Patches/LpePriorityPatch.cs` | 4 postfixes implementing LPE > UiLocale |
| `Plugin.cs` | Calls `DshCoreIntegration.Initialize()` and `LpePriorityPatch.Apply()` |
| `Core/LanguagePackInjector.cs` | Injects mod packs before the built-in provider |
| `Core/ModLanguagePackLoader.cs` | `.lang.json` → `ResourcePack` (context uses `\u0004`) |
