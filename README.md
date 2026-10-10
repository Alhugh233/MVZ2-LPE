Language: English | [简体中文](README.zh-CN.md)

# LanguagePackEnhance

![Vibe Coding](https://img.shields.io/badge/Vibe%20Coding-100%25-blueviolet)

A BepInEx 6 (IL2CPP) localization framework for **MVZ2 (Minecraft VS Zombies 2) / MVZ2Expand** mods.

It lets other mods expose their own text to the game's localization system, ships their own translations as embedded language packs, and lets players override/extend them with third-party language packs.

## Table of Contents

- [Overview](#overview)
- [Vibe Coding](#vibe-coding)
- [How it works](#how-it-works)
- [For mod authors (API)](#for-mod-authors-api)
- [Language pack formats](#language-pack-formats)
- [Music Room metadata](#music-room-metadata)
- [Installation](#installation)
- [Building from Source](#building-from-source)
- [Compatibility](#compatibility)
- [Credits](#credits)
- [License](#license)

## Overview

LanguagePackEnhance (LPE) is a **framework for other mods**, not an end-user content mod. It provides:

- a small API for mods to register their own translations;
- automatic integration with the game's existing language pack system (so player-imported language packs can translate/override mod text too);
- a single merged **"Mod 自带语言包" (mod-bundled language packs)** entry on the game's language pack page.

It is split into **two assemblies** (like a framework + thin API):

- `LanguagePackEnhance.dll` — the BepInEx plugin (implementation).
- `LanguagePackEnhance.API.dll` — the **thin API** (`LanguagePackEnhanceApi`, `TranslationEntry`, `ILanguagePackEnhanceService`), with no game or BepInEx dependencies. Other mods **compile against this one only**. Repo: [MVZ2-LPEAPI](https://github.com/Alhugh233/MVZ2-LPEAPI).

Lookup priority for every string: **imported third-party language pack > mod-bundled pack > game built-in pack > original text**.

## Vibe Coding

This project is developed with **Vibe Coding**: the code and documentation are written primarily by prompting AI assistants. Parts of the codebase are AI-generated. Please review and test before relying on it in your own setup.

## How it works

- LPE hooks `LanguageManager.ValidateCurrentLanguage` (called synchronously at the end of `InitLanguagePacks` / `ReloadLanguagePacks`) and injects each registered mod's `LanguagePack` into the game's `loadedLanguagePacks` list, **after third-party packs and before the built-in pack**. The game's own ordered lookup then yields the priority above without reimplementing any translation logic.
- The built-in pack is moved to the end so it always has the lowest priority.
- On the language pack page, LPE injects a single virtual entry (**"Mod 自带语言包"**). It is placed **above the built-in pack** and behaves like a normal entry: it can be selected and moved up/down, but it can never be reordered below the built-in pack, and it has no disable/delete/export actions.

## For mod authors (API)

Reference the **thin API** `LanguagePackEnhance.API.dll` at compile time (do **not** ship it; the plugin provides it at runtime), and declare the dependency:

```csharp
[BepInDependency("com.mvz2.languagepackenhance")]
```

> Compile-time reference only: use `Private=false` (or `compileOnly`), since both `LanguagePackEnhance.API.dll` and the framework are present in `BepInEx/plugins/` at runtime.

### 1. Register an embedded language pack

Ship a language pack as an **embedded resource** in your DLL (`.zip` in the game's language pack format, or the simplified JSON below):

```csharp
// called from your plugin's Load()
LanguagePackEnhanceApi.RegisterMod("MyMod", "MyMod.lang.json");
// or with an explicit assembly:
LanguagePackEnhanceApi.RegisterMod("MyMod", typeof(MyPlugin).Assembly, "MyMod.lang.json");
```

The format is auto-detected: a resource starting with `PK` is treated as a zip, otherwise as JSON.

### 2. Translate a string at runtime

```csharp
string s = LanguagePackEnhanceApi.Translate("我的设置");
string s2 = LanguagePackEnhanceApi.Translate("已选择 {0} 项", count);
```

`Translate` is equivalent to the game's `LanguageManager._` and resolves through the priority chain above. Mods can also call the game's `_` / `_p` directly; the injected packs are consulted either way.

### 3. Register runtime translations (for dynamically loaded content)

For content loaded at runtime (e.g. user-installed music packs), register translations programmatically:

```csharp
LanguagePackEnhanceApi.RegisterTranslations("MyMod", new[]
{
    new TranslationEntry("en-US", "我的设置", "My Settings"),
    new TranslationEntry("en-US", "Bossrush的音乐。…", "Boss Rush theme", "music.description"),
});

// replace the whole set for the nsp later, or clear it:
LanguagePackEnhanceApi.ClearTranslations("MyMod");
```

- `RegisterTranslations` replaces the nsp's previous runtime set and re-injects immediately if the game is ready.
- `TranslationEntry(language, source, translation, context?)`: a null/empty `context` is plain text (game `_`); a non-null `context` is a context lookup (game `_p`).

## Language pack formats

### Simplified JSON

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

- Top level: language code → entries.
- An entry value that is a **string** is a plain translation; an **array of strings** provides plural forms.
- An entry value that is an **object** is treated as a **context** map (game `_p`): `{ "context": { "source": "translation" } }`. The key stored internally is `context + "\u0004" + source`.

### Game language pack zip

This is the **standard MVZ2 language pack format**: a zip (or folder) with `pack.json` + `assets/<nsp>/<lang>/*.mo` (gettext catalogs, including `msgctxt`), optionally `sprite_manifest.json` + textures. (Sprite localization is only supported through this zip format.)

If you are new to creating language packs, see the official template & tutorial: **Cuerzor/MVZ2LanguagePack** — <https://github.com/Cuerzor/MVZ2LanguagePack>.

### What is LPE-specific

For someone merely **making a language pack**, there is **no LPE-specific format** — a normal MVZ2 language pack works, keyed by the mod's source strings (and `msgctxt` for contexts such as `music.*`). LPE adds only:

- **Priority**: imported third-party pack > mod-bundled pack > game built-in > original text.
- A single merged **"Mod 自带语言包"** entry on the game's language pack page. It is selectable and can be moved up/down, but is always kept **above the built-in pack** and has no disable/delete/export actions.
- The optional **simplified JSON** alternative to `.mo` (for mod authors' embedded packs).
- The mod-side API (`RegisterMod` / `RegisterTranslations` / `Translate`).

## Music Room metadata

The game localizes per-track music metadata through the `_p` contexts `music.name` / `music.source` / `music.origin` / `music.author` / `music.description`, keyed by `context + "\u0004" + <source text>`. LPE-registered packs participate in those lookups automatically, so a mod can localize its custom tracks by providing entries under those contexts (plain JSON context map, or a `.mo` with `msgctxt`).

## Installation

1. Make sure the game has **BepInEx 6 (IL2CPP)** installed.
2. Copy **both** `LanguagePackEnhance.dll` and `LanguagePackEnhance.API.dll` into `BepInEx/plugins/`.
3. Start the game. If the log shows `Plugin com.mvz2.languagepackenhance is loaded!`, installation succeeded.

Other mods that integrate with LPE must be installed alongside it (they reference `LanguagePackEnhance.API.dll`).

## Building from Source

Requirements: `.NET SDK` (target `net6.0`).

The source is split across **two sibling projects**: this repo (`LanguagePackEnhance`, the plugin) and `LanguagePackEnhance.API` (the thin API). Place them next to each other:

```
<parent>/
  LanguagePackEnhance/        (this repo, the plugin)
  LanguagePackEnhance.API/    (the thin API)
```

1. Put the game **interop DLLs** into this repo's `interop/` folder (this folder is `.gitignore`d), or pass `-p:InteropDir=`.
2. Build (the plugin's `ProjectReference` builds the API project and copies `LanguagePackEnhance.API.dll` into the output):

```bash
dotnet build
# or
dotnet build -p:InteropDir=/path/to/interop_mvz2expand
```

Output: `bin/Debug/net6.0/LanguagePackEnhance.dll` + `LanguagePackEnhance.API.dll`.

### Dependencies

- `BepInEx.Unity.IL2CPP`, `BepInEx.PluginInfoProps` (plugin only)
- `LanguagePackEnhance.API` (project reference)

## Compatibility

- Target framework: `net6.0` (BepInEx 6 IL2CPP / CoreCLR).
- Plugin GUID: `com.mvz2.languagepackenhance`.
- Integrates with **MVZ2Expand** (interop: `interop_mvz2expand`). Not yet verified against vanilla MVZ2.

## Credits

- **MVZ2 (original game)** by Cuerzor — <https://github.com/Cuerzor/MinecraftVsZombies2Unity>
- **MVZ2Expand** by Tr-DESE107 — <https://github.com/Tr-DESE107/MinecraftVsZombies2_EXPAND>
- Framework: BepInEx.

## License

Copyright (C) 2026 Alhugh233.

This project is licensed under the **GNU General Public License v3.0**. The full license text is included verbatim in [LICENSE](LICENSE).

The bundled thin API assembly **LanguagePackEnhance.API** is licensed separately under the **Apache License 2.0** (see the `LanguagePackEnhance.API` project). Apache-2.0 is compatible with GPL-3.0.

This project is **directly based on the two open-source games MVZ2 and MVZ2Expand** (both GPL-3.0). It is a runtime mod that interoperates with them through BepInEx / Harmony and does not redistribute the games' assemblies.
