#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MVZ2.Localization;
using NGettext;

namespace LanguagePackEnhance;

/// <summary>
/// 把 Mod 自带语言包的内嵌资源解析成游戏的 <see cref="LanguagePack"/>。
/// 支持两种格式：
/// 1. 与游戏语言包一致的 zip（内含 pack.json / assets/&lt;nsp&gt;/&lt;lang&gt;/*.mo）；
/// 2. 简化的 JSON，结构为 <c>{ "语言": { "原文": "译文" } }</c>，值也可为数组表示复数形式；
///    某个 key 的值为对象时按 context 处理（对应游戏的 <c>_p</c>）：
///    <c>{ "语言": { "music.description": { "原文": "译文" } } }</c>。
/// </summary>
internal static class ModLanguagePackLoader
{
    private const char ContextGlue = '\u0004';

    public static LanguagePack? Load(LanguageManager languageManager, string key, byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return null;
        return IsZip(bytes)
            ? languageManager.ReadLanguagePackZip(key, bytes)
            : BuildFromJson(key, bytes);
    }

    private static bool IsZip(byte[] bytes)
    {
        return bytes.Length >= 4
            && bytes[0] == 0x50 && bytes[1] == 0x4B
            && (bytes[2] == 0x03 || bytes[2] == 0x05 || bytes[2] == 0x07);
    }

    private static LanguagePack? BuildFromJson(string key, byte[] bytes)
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

        var pack = new LanguagePack(key);
        foreach (var languagePair in root)
        {
            var entries = MiniJson.Obj(languagePair.Value);
            if (entries == null)
            {
                Diag.Warn($"JSON 语言包 '{key}' 中语言 '{languagePair.Key}' 的内容不是对象，已跳过。");
                continue;
            }

            var asset = pack.GetOrCreateLanguageAsset(languagePair.Key);
            var catalog = new Catalog();

            foreach (var entry in entries)
            {
                // value 为对象时按 context 处理：{ "music.description": { "原文": "译文" } }
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
            asset.catalogs.Add("mod", catalog);
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
        translations[key] = new Il2CppStringArray(forms);
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
