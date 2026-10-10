#nullable enable

using System;
using MVZ2.Localization;

namespace LanguagePackEnhance;

/// <summary>
/// 把 Mod 自带语言包注入到游戏的 <see cref="LanguageManager.loadedLanguagePacks"/> 中，
/// 位置位于第三方语言包之后、内置语言包之前，从而得到优先级：
/// 第三方语言包 &gt; Mod 自带语言包 &gt; 游戏内置。
/// </summary>
internal static class LanguagePackInjector
{
    public const string ModKeyPrefix = "modlang:";
    public const string ModDynamicKeyPrefix = "modlangdyn:";
    private const string ModKeyRoot = "modlang";
    public const string BuiltinKey = "builtin*";

    public static void Inject(LanguageManager languageManager)
    {
        if (languageManager == null)
            return;

        var loaded = languageManager.loadedLanguagePacks;
        if (loaded == null)
            return;

        var registrations = ModLanguagePackRegistry.Registrations;
        if (registrations.Count == 0 && !DynamicTranslationStore.HasAny)
            return;

        // 1. 移除上一次注入的 Mod 语言包（内嵌 + 运行时）。
        for (int i = loaded.Count - 1; i >= 0; i--)
        {
            var pack = loaded[i];
            if (pack != null && pack.Key != null && pack.Key.StartsWith(ModKeyRoot, StringComparison.Ordinal))
                loaded.RemoveAt(i);
        }

        // 2. 把内置语言包移动到末尾，使其优先级最低。
        int builtinIndex = -1;
        for (int i = 0; i < loaded.Count; i++)
        {
            var pack = loaded[i];
            if (pack != null && pack.Key == BuiltinKey)
            {
                builtinIndex = i;
                break;
            }
        }
        if (builtinIndex >= 0 && builtinIndex != loaded.Count - 1)
        {
            var builtin = loaded[builtinIndex];
            loaded.RemoveAt(builtinIndex);
            loaded.Add(builtin);
            builtinIndex = loaded.Count - 1;
        }

        // 3. 在内置语言包之前插入 Mod 语言包。
        int insertAt = builtinIndex >= 0 ? loaded.Count - 1 : loaded.Count;
        int injected = 0;
        foreach (var registration in registrations)
        {
            var bytes = registration.GetBytes();
            if (bytes == null)
                continue;
            try
            {
                var pack = ModLanguagePackLoader.Load(languageManager, registration.Key, bytes);
                if (pack == null)
                    continue;
                loaded.Insert(insertAt + injected, pack);
                injected++;
            }
            catch (Exception e)
            {
                Diag.Error($"解析 Mod 语言包 '{registration.Key}' 失败：{e}");
            }
        }

        // 4. 插入运行时注册的译文。
        foreach (var pair in DynamicTranslationStore.All)
        {
            try
            {
                var pack = DynamicLanguagePackBuilder.Build(ModDynamicKeyPrefix + pair.Key, pair.Value);
                if (pack == null)
                    continue;
                loaded.Insert(insertAt + injected, pack);
                injected++;
            }
            catch (Exception e)
            {
                Diag.Error($"构建运行时译文包 '{pair.Key}' 失败：{e}");
            }
        }

        if (injected > 0)
            Diag.Info($"已注入 {injected} 个 Mod 译文包（内嵌 + 运行时）。");
    }
}
