#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using BepInEx.Unity.IL2CPP;
using DSHCore.Ui;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace LanguagePackEnhance;

/// <summary>
/// 可选的 DSHCore 集成。
/// 检测到 DSHCore 时，把 LPE 声明为一个 DSHCore Mod，并把全部译文镜像到
/// <see cref="UiLocale"/>（DSHCore 的本地化查找链），同时保留原有 ResourcePacks 注入。
/// </summary>
internal static class DshCoreIntegration
{
    public const string DshCoreGuid = "qoznos.mvz2.core";
    public const string OwnNsp = "com.mvz2.languagepackenhance";

    public static bool Available { get; private set; }

    private static bool initialized;

    public static void Initialize()
    {
        if (initialized)
            return;
        initialized = true;

        try
        {
            Available = IL2CPPChainloader.Instance != null && IL2CPPChainloader.Instance.Plugins != null && IL2CPPChainloader.Instance.Plugins.ContainsKey(DshCoreGuid);
        }
        catch (Exception e)
        {
            Available = false;
            Diag.Warn($"检测 DSHCore 失败：{e.Message}");
        }

        if (!Available)
        {
            Diag.Info("未检测到 DSHCore，使用常规模式。");
            return;
        }

        try
        {
            InitializeDshCore();
            Diag.Info("检测到 DSHCore，已切换为 DSHCore 模式。");
        }
        catch (Exception e)
        {
            Diag.Warn($"DSHCore 模式初始化失败：{e.Message}");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void InitializeDshCore()
    {
        UiOptions.DeclareMod(
            OwnNsp,
            "LanguagePackEnhance",
            "Localization framework for MVZ2 mods.",
            "Alhugh233");
    }

    public static void MirrorTranslations(IEnumerable<TranslationEntry>? entries)
    {
        if (!Available || entries == null)
            return;
        try
        {
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.Source))
                    continue;
                UiLocale.Add(entry.Source, entry.Translation ?? string.Empty, entry.Language, entry.Context);
            }
        }
        catch (Exception e)
        {
            Diag.Warn($"镜像运行时译文到 DSHCore 失败：{e.Message}");
        }
    }

    public static void MirrorLanguagePack(Assembly assembly, string resourceName, string nsp)
    {
        if (!Available || assembly == null || string.IsNullOrEmpty(resourceName))
            return;
        try
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                return;
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            MirrorLanguagePackJson(memory.ToArray());
        }
        catch (Exception e)
        {
            Diag.Warn($"镜像语言包 '{nsp}' 到 DSHCore 失败：{e.Message}");
        }
    }

    private static void MirrorLanguagePackJson(byte[] bytes)
    {
        var text = Encoding.UTF8.GetString(bytes);
        if (text.Length > 0 && text[0] == '\uFEFF')
            text = text.Substring(1);

        var root = MiniJson.Obj(MiniJson.Parse(text));
        if (root == null)
            return;

        foreach (var languagePair in root)
        {
            var language = languagePair.Key;
            var entries = MiniJson.Obj(languagePair.Value);
            if (entries == null)
                continue;

            foreach (var entry in entries)
            {
                if (entry.Value is Dictionary<string, object?> contextEntries)
                {
                    foreach (var contextEntry in contextEntries)
                    {
                        var forms = ToForms(contextEntry.Value);
                        if (forms != null && forms.Length > 0)
                            UiLocale.Add(contextEntry.Key, forms[0], language, entry.Key);
                    }
                }
                else
                {
                    var forms = ToForms(entry.Value);
                    if (forms != null && forms.Length > 0)
                        UiLocale.Add(entry.Key, forms[0], language);
                }
            }
        }
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
