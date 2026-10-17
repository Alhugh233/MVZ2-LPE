#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using MVZ2.ResourcePacks;
using NGettext;

namespace LanguagePackEnhance;

/// <summary>
/// 把 Mod 自带语言包的内嵌资源解析成游戏的 <see cref="ResourcePack"/>。
/// JSON 结构：<c>{ "语言": { "原文": "译文" } }</c>，值为数组表示复数；
/// 值为对象时按 context 处理（对应游戏的 <c>_p</c>）：
/// <c>{ "语言": { "music.description": { "原文": "译文" } } }</c>。
/// </summary>
internal static class ModLanguagePackLoader
{
    private const char ContextGlue = '\u0004';

    public static ResourcePack? Load(string key, byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return null;
        return BuildFromJson(key, bytes);
    }

    private static ResourcePack? BuildFromJson(string key, byte[] bytes)
    {
        string text;
        try
        {
            text = Encoding.UTF8.GetString(bytes);
        }
        catch (Exception e)
        {
            Diag.Error($"解析 JSON 语言包 '{key}' 失败：{e}");
            return null;
        }
        if (text.Length > 0 && text[0] == '\uFEFF')
            text = text.Substring(1);

        Dictionary<string, object?>? root;
        try
        {
            root = MiniJson.Obj(MiniJson.Parse(text));
        }
        catch (Exception e)
        {
            Diag.Error($"解析 JSON 语言包 '{key}' 失败：{e}");
            return null;
        }
        if (root == null)
        {
            Diag.Error($"JSON 语言包 '{key}' 的根节点不是对象。");
            return null;
        }

        var pack = new ResourcePack(key, key);
        foreach (var languagePair in root)
        {
            var entries = MiniJson.Obj(languagePair.Value);
            if (entries == null)
            {
                Diag.Warn($"JSON 语言包 '{key}' 中语言 '{languagePair.Key}' 的内容不是对象，已跳过。");
                continue;
            }

            var catalogs = pack.GetOrCreateLanguageCatalogs(languagePair.Key);
            var catalog = new Catalog();

            foreach (var entry in entries)
            {
                // value 为对象时按 context 处理。
                if (entry.Value is Dictionary<string, object?> contextEntries)
                {
                    foreach (var contextEntry in contextEntries)
                    {
                        SetTranslation(catalog, entry.Key, contextEntry.Key, ToForms(contextEntry.Value));
                    }
                }
                else
                {
                    SetTranslation(catalog, null, entry.Key, ToForms(entry.Value));
                }
            }
            catalogs.catalogs.Add("mod", catalog);
        }
        return pack;
    }

    /// <summary>把一条译文写入 catalog（context 为空表示普通文本，否则对应游戏的 <c>_p</c>）。</summary>
    internal static void SetTranslation(Catalog catalog, string? context, string source, string[]? forms)
    {
        if (catalog == null || string.IsNullOrEmpty(source) || forms == null || forms.Length == 0)
            return;
        var translations = catalog.Translations;
        if (translations == null)
        {
            Diag.Error("无法创建翻译目录。");
            return;
        }
        var key = string.IsNullOrEmpty(context) ? source : context + ContextGlue + source;
        translations[key] = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStringArray(forms);
    }

    private static string[]? ToForms(object? value)
    {
        switch (value)
        {
            case string s:
                return new[] { s };
            case List<object?> list:
                var forms = new List<string>();
                foreach (var item in list)
                    forms.Add(item as string ?? item?.ToString() ?? string.Empty);
                return forms.ToArray();
            default:
                return null;
        }
    }
}
