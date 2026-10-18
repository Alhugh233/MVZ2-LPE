# Third-Party Notices

LanguagePackEnhance **optionally** integrates with DSHCore when it is installed at runtime.
DSHCore is **not** redistributed in this project's runtime output; a compile-time reference
(`DSHCore.dll`) is **not** included in this repository; builders supply it locally via `-p:DshCoreDir=`.

## DSHCore

- Author: **QoZnoS**
- Project: <https://github.com/QoZnoS/MVZ2Mod>
- License: **GNU General Public License v3.0**

When DSHCore is present, LanguagePackEnhance declares itself as a DSHCore mod
(`UiOptions.DeclareMod`) and mirrors its translations into DSHCore's `UiLocale` lookup chain.
Without DSHCore, LanguagePackEnhance runs in its normal ResourcePacks mode.
