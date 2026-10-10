#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MVZ2.Managers;

namespace LanguagePackEnhance;

/// <summary>
/// <see cref="ILanguagePackEnhanceService"/> 的实现，在核心插件加载时注册到 <see cref="LanguagePackEnhanceApi.Service"/>。
/// </summary>
internal sealed class LanguagePackEnhanceService : ILanguagePackEnhanceService
{
    public void RegisterMod(string nsp, Assembly assembly, string resourceName)
        => ModLanguagePackRegistry.Register(nsp, assembly, resourceName);

    public void RegisterTranslations(string nsp, IEnumerable<TranslationEntry> entries)
        => DynamicTranslationStore.Set(nsp, entries);

    public void ClearTranslations(string nsp)
        => DynamicTranslationStore.Clear(nsp);

    public string Translate(string text, object?[] args)
    {
        var languageManager = MainManager.Instance?.LanguageManager;
        if (languageManager == null)
            return text;

        if (args == null || args.Length == 0)
            return languageManager._(text);

        var il2cppArgs = new Il2CppSystem.Object[args.Length];
        for (int i = 0; i < args.Length; i++)
        {
            il2cppArgs[i] = Box(args[i]);
        }
        return languageManager._(text, il2cppArgs);
    }

    private static Il2CppSystem.Object Box(object? value)
    {
        return value switch
        {
            null => null!,
            string s => s,
            int i => i,
            long l => l,
            float f => f,
            double d => d,
            bool b => b,
            char c => c,
            byte by => by,
            sbyte sb => sb,
            short sh => sh,
            ushort ush => ush,
            uint ui => ui,
            ulong ul => ul,
            _ => value.ToString()!,
        };
    }
}
