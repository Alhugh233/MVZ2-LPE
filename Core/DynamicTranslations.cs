#nullable enable

using System;
using System.Collections.Generic;
using MVZ2.Managers;
using MVZ2.ResourcePacks;
using NGettext;

namespace LanguagePackEnhance;

/// <summary>
/// 运行时注册的译文（不来自内嵌资源）。每个 nsp 一个集合，可整体替换/清除。
/// </summary>
internal static class DynamicTranslationStore
{
    private static readonly Dictionary<string, List<TranslationEntry>> byNsp = new();

    public static IReadOnlyDictionary<string, List<TranslationEntry>> All => byNsp;
    public static bool HasAny => byNsp.Count > 0;

    public static void Set(string nsp, IEnumerable<TranslationEntry>? entries)
    {
        if (string.IsNullOrEmpty(nsp))
        {
            Diag.Error("RegisterTranslations 参数无效（nsp 不能为空）。");
            return;
        }

        var list = new List<TranslationEntry>();
        if (entries != null)
        {
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.Language) || string.IsNullOrEmpty(entry.Source))
                    continue;
                list.Add(entry);
            }
        }
        byNsp[nsp] = list;
        Diag.Info($"已更新运行时译文：'{nsp}'（{list.Count} 条）。");
        InjectIfReady();
    }

    public static void Clear(string nsp)
    {
        if (byNsp.Remove(nsp))
            Diag.Info($"已清除运行时译文：'{nsp}'。");
        InjectIfReady();
    }

    private static void InjectIfReady()
    {
        try
        {
            if (MainManager.Instance != null)
                LanguagePackInjector.Reinject();
        }
        catch (Exception e)
        {
            Diag.Error($"运行时译文注入失败：{e}");
        }
    }
}

/// <summary>把运行时译文构建成游戏的 <see cref="ResourcePack"/>。</summary>
internal static class DynamicLanguagePackBuilder
{
    public static ResourcePack? Build(string key, IReadOnlyList<TranslationEntry> entries)
    {
        if (entries == null || entries.Count == 0)
            return null;

        var pack = new ResourcePack(key, key);
        var catalogs = new Dictionary<string, Catalog>();
        foreach (var entry in entries)
        {
            if (!catalogs.TryGetValue(entry.Language, out var catalog))
            {
                catalog = new Catalog();
                catalogs[entry.Language] = catalog;
            }
            ModLanguagePackLoader.SetTranslation(catalog, entry.Context, entry.Source, new[] { entry.Translation ?? string.Empty });
        }
        foreach (var pair in catalogs)
        {
            var languageCatalogs = pack.GetOrCreateLanguageCatalogs(pair.Key);
            languageCatalogs.catalogs.Add("mod", pair.Value);
        }
        return pack;
    }
}
