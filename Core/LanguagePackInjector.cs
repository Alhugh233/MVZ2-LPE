#nullable enable

using System;
using System.Collections.Generic;
using MVZ2.Localization;
using MVZ2.Managers;
using MVZ2.ResourcePacks;

namespace LanguagePackEnhance;

/// <summary>
/// 把 Mod 自带语言包注入到 <see cref="LanguageManager.catalogProviders"/>，
/// 位置在内置 provider 之前（第三方 provider 在更前），得到优先级：
/// 第三方语言包 &gt; Mod 自带语言包 &gt; 游戏内置。
/// </summary>
internal static class LanguagePackInjector
{
    public const string ModKeyPrefix = "modlang:";
    public const string ModDynamicKeyPrefix = "modlangdyn:";

    private static readonly List<ResourcePack> injected = new List<ResourcePack>();

    public static void Reinject()
    {
        var languageManager = MainManager.Instance?.LanguageManager;
        if (languageManager == null)
            return;

        try
        {
            var providers = languageManager.catalogProviders;
            if (providers == null)
                return;

            // 1. 移除上一次注入的 provider。
            foreach (var old in injected)
            {
                try
                {
                    languageManager.RemoveCatalogProvider(old.Cast<ICatalogProvider>());
                }
                catch (Exception e)
                {
                    Diag.Warn($"移除旧 Mod 译文包失败：{e.Message}");
                }
            }
            injected.Clear();

            // 2. 重建 Mod 语言包。
            foreach (var registration in ModLanguagePackRegistry.Registrations)
            {
                var bytes = registration.GetBytes();
                if (bytes == null)
                    continue;
                var pack = ModLanguagePackLoader.Load(registration.Key, bytes);
                if (pack != null)
                    injected.Add(pack);
            }
            foreach (var pair in DynamicTranslationStore.All)
            {
                var pack = DynamicLanguagePackBuilder.Build(ModDynamicKeyPrefix + pair.Key, pair.Value);
                if (pack != null)
                    injected.Add(pack);
            }
            if (injected.Count == 0)
                return;

            // 3. 插到内置 provider 之前（找不到内置则追加到末尾）。
            int insertAt = FindBuiltinIndex(providers);
            foreach (var pack in injected)
            {
                var provider = pack.Cast<ICatalogProvider>();
                if (insertAt >= 0 && insertAt <= providers.Count)
                {
                    providers.Insert(insertAt, provider);
                    insertAt++;
                }
                else
                {
                    providers.Add(provider);
                }
            }
            Diag.Info($"已注入 {injected.Count} 个 Mod 译文包。");
        }
        catch (Exception e)
        {
            Diag.Error($"注入 Mod 译文包失败：{e}");
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
