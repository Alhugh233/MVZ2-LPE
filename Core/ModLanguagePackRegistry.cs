#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MVZ2.Localization;
using MVZ2.Managers;

namespace LanguagePackEnhance;

/// <summary>一个已注册的 Mod 自带语言包。</summary>
internal sealed class ModLanguagePackRegistration
{
    public string Nsp = string.Empty;
    public Assembly Assembly = null!;
    public string ResourceName = string.Empty;

    /// <summary>注入到 <see cref="LanguageManager.loadedLanguagePacks"/> 时使用的 key。</summary>
    public string Key => LanguagePackInjector.ModKeyPrefix + Nsp;

    private byte[]? cachedBytes;

    public byte[]? GetBytes()
    {
        if (cachedBytes != null)
            return cachedBytes;
        try
        {
            using var stream = Assembly.GetManifestResourceStream(ResourceName);
            if (stream == null)
                return null;
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            cachedBytes = memory.ToArray();
            return cachedBytes;
        }
        catch (Exception e)
        {
            Diag.Error($"读取内嵌语言包资源 '{Assembly.GetName().Name}/{ResourceName}' 失败：{e}");
            return null;
        }
    }
}

/// <summary>Mod 自带语言包的注册表。</summary>
internal static class ModLanguagePackRegistry
{
    private static readonly List<ModLanguagePackRegistration> registrations = new();
    private static readonly HashSet<string> registeredKeys = new();

    public static IReadOnlyList<ModLanguagePackRegistration> Registrations => registrations;
    public static int Count => registrations.Count;

    public static void Register(string nsp, Assembly assembly, string resourceName)
    {
        if (string.IsNullOrEmpty(nsp) || assembly == null || string.IsNullOrEmpty(resourceName))
        {
            Diag.Error("RegisterMod 参数无效（nsp/assembly/resourceName 不能为空）。");
            return;
        }

        var key = LanguagePackInjector.ModKeyPrefix + nsp;
        if (registeredKeys.Contains(key))
        {
            Diag.Warn($"Mod '{nsp}' 已注册过语言包，忽略重复注册。");
            return;
        }

        if (assembly.GetManifestResourceStream(resourceName) == null)
        {
            Diag.Error($"Mod '{nsp}' 注册失败：程序集 '{assembly.GetName().Name}' 中不存在内嵌资源 '{resourceName}'。");
            return;
        }

        registeredKeys.Add(key);
        registrations.Add(new ModLanguagePackRegistration
        {
            Nsp = nsp,
            Assembly = assembly,
            ResourceName = resourceName,
        });
        Diag.Info($"已注册 Mod 语言包：'{nsp}'（{assembly.GetName().Name}/{resourceName}）。");

        // 同步镜像到 DSHCore（若存在），让 /dshtrans 能看到这些译文。
        DshCoreIntegration.MirrorLanguagePack(assembly, resourceName, nsp);

        // 如果游戏已经初始化，立即重新注入。
        try
        {
            if (MainManager.Instance != null)
                LanguagePackInjector.Reinject();
        }
        catch (Exception e)
        {
            Diag.Error($"立即注入 Mod 语言包失败：{e}");
        }
    }
}
