#nullable enable

using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MVZ2.Localization;
using MVZ2.Managers;
using MVZ2.ResourcePacks;

namespace LanguagePackEnhance.Patches;

/// <summary>
/// LPE 存在时，让 LPE 的解析链路（第三方资源包 + Mod 自带语言包）
/// 优先于 DSHCore 的 UiLocale（外部文件与代码表）：
/// LPE &gt; DSHCore 外部文件 &gt; DSHCore 代码表 &gt; 游戏内置。
///
/// <para>
/// ⚠ HarmonyX 下 prefix 默认全部执行，返回 false 只跳过原方法，不会跳过别的 prefix，
/// 所以不能用 prefix 去抢 DSHCore 的 UiLocale。这里改用 postfix：
/// 所有 prefix（含 UiLocale）跑完后，再用 LPE 的结果覆盖。
/// </para>
/// </summary>
internal static class LpePriorityPatch
{
    private const string HarmonyId = MyPluginInfo.PLUGIN_GUID + ".priority";
    private static bool applied;

    public static void Apply()
    {
        if (applied)
            return;
        applied = true;

        var harmony = new Harmony(HarmonyId);
        var plainNative = new HarmonyMethod(AccessTools.Method(typeof(LpePriorityPatch), nameof(PlainNative)));
        var plainManaged = new HarmonyMethod(AccessTools.Method(typeof(LpePriorityPatch), nameof(PlainManaged)));
        var particularNative = new HarmonyMethod(AccessTools.Method(typeof(LpePriorityPatch), nameof(ParticularNative)));
        var particularManaged = new HarmonyMethod(AccessTools.Method(typeof(LpePriorityPatch), nameof(ParticularManaged)));

        int patched = 0;
        patched += PatchOne(harmony, Find("TryGetLocalizedString", false, false), plainNative);
        patched += PatchOne(harmony, Find("TryGetLocalizedString", false, true), plainManaged);
        patched += PatchOne(harmony, Find("TryGetLocalizedStringParticular", true, false), particularNative);
        patched += PatchOne(harmony, Find("TryGetLocalizedStringParticular", true, true), particularManaged);

        if (patched == 0)
            Diag.Warn("[LPE] 优先级 postfix 未能挂上 LanguageManager 的文本查找。");
        else
            Diag.Info($"[LPE] 优先级 postfix 已挂载 {patched}/4 处。");
    }

    private static void PlainNative(
        LanguageManager __instance,
        string text,
        string language,
        ref string translated,
        Il2CppReferenceArray<Il2CppSystem.Object>? args,
        ref bool __result)
    {
        OverrideNative(__instance, language, text, null, args, ref translated, ref __result);
    }

    private static void PlainManaged(
        LanguageManager __instance,
        string text,
        string language,
        ref string translated,
        Il2CppSystem.Object[]? args,
        ref bool __result)
    {
        OverrideManaged(__instance, language, text, null, args, ref translated, ref __result);
    }

    private static void ParticularNative(
        LanguageManager __instance,
        string context,
        string text,
        string language,
        ref string translated,
        Il2CppReferenceArray<Il2CppSystem.Object>? args,
        ref bool __result)
    {
        OverrideNative(__instance, language, text, context, args, ref translated, ref __result);
    }

    private static void ParticularManaged(
        LanguageManager __instance,
        string context,
        string text,
        string language,
        ref string translated,
        Il2CppSystem.Object[]? args,
        ref bool __result)
    {
        OverrideManaged(__instance, language, text, context, args, ref translated, ref __result);
    }

    private static void OverrideNative(
        LanguageManager manager,
        string language,
        string text,
        string? context,
        Il2CppReferenceArray<Il2CppSystem.Object>? args,
        ref string translated,
        ref bool __result)
    {
        try
        {
            if (args != null && args.Length > 0)
                return;
            if (!TryResolve(manager, language, text, context, args, out var hit))
                return;
            translated = hit!;
            __result = true;
        }
        catch (Exception e)
        {
            Diag.Warn($"LPE 优先级覆盖失败：{e.Message}");
        }
    }

    private static void OverrideManaged(
        LanguageManager manager,
        string language,
        string text,
        string? context,
        Il2CppSystem.Object[]? args,
        ref string translated,
        ref bool __result)
    {
        try
        {
            if (args != null && args.Length > 0)
                return;
            if (!TryResolveManaged(manager, language, text, context, args, out var hit))
                return;
            translated = hit!;
            __result = true;
        }
        catch (Exception e)
        {
            Diag.Warn($"LPE 优先级覆盖失败：{e.Message}");
        }
    }

    private static bool TryResolve(
        LanguageManager manager,
        string language,
        string text,
        string? context,
        Il2CppReferenceArray<Il2CppSystem.Object>? args,
        out string? translated)
    {
        translated = null;
        try
        {
            var providers = manager.catalogProviders;
            if (providers == null)
                return false;

            int count = FindBuiltinIndex(providers);
            if (count < 0)
                count = providers.Count;

            for (int i = 0; i < count; i++)
            {
                var provider = providers[i];
                if (provider == null)
                    continue;

                bool found = context == null
                    ? provider.TryGetString(language, text, out translated, args)
                    : provider.TryGetStringParticular(language, context, text, out translated, args);
                if (found)
                    return true;
            }
        }
        catch (Exception e)
        {
            Diag.Warn($"LPE 优先级解析失败：{e.Message}");
        }
        return false;
    }

    private static bool TryResolveManaged(
        LanguageManager manager,
        string language,
        string text,
        string? context,
        Il2CppSystem.Object[]? args,
        out string? translated)
    {
        translated = null;
        try
        {
            var providers = manager.catalogProviders;
            if (providers == null)
                return false;

            int count = FindBuiltinIndex(providers);
            if (count < 0)
                count = providers.Count;

            for (int i = 0; i < count; i++)
            {
                var provider = providers[i];
                if (provider == null)
                    continue;

                bool found = context == null
                    ? provider.TryGetString(language, text, out translated, args)
                    : provider.TryGetStringParticular(language, context, text, out translated, args);
                if (found)
                    return true;
            }
        }
        catch (Exception e)
        {
            Diag.Warn($"LPE 优先级解析失败：{e.Message}");
        }
        return false;
    }

    private static System.Reflection.MethodInfo? Find(string name, bool particular, bool managedArgs)
    {
        var types = new List<Type>();
        if (particular)
            types.Add(typeof(string));
        types.Add(typeof(string));
        types.Add(typeof(string));
        types.Add(typeof(string).MakeByRefType());
        types.Add(managedArgs
            ? typeof(Il2CppSystem.Object[])
            : typeof(Il2CppReferenceArray<Il2CppSystem.Object>));
        return AccessTools.Method(typeof(LanguageManager), name, types.ToArray());
    }

    private static int PatchOne(Harmony harmony, System.Reflection.MethodInfo? target, HarmonyMethod postfix)
    {
        if (target == null)
            return 0;
        try
        {
            harmony.Patch(target, postfix: postfix);
            return 1;
        }
        catch (Exception e)
        {
            Diag.Warn($"LPE 优先级 postfix 挂载失败（{target}）：{e.Message}");
            return 0;
        }
    }

    private static int FindBuiltinIndex(Il2CppSystem.Collections.Generic.List<ICatalogProvider> providers)
    {
        for (int i = 0; i < providers.Count; i++)
        {
            var provider = providers[i];
            if (provider == null)
                continue;
            var pack = provider.TryCast<ResourcePack>();
            if (pack != null && pack.Key != null && pack.Key.Contains("builtin", StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }
}
