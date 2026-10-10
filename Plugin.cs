#nullable enable

using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace LanguagePackEnhance;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    internal static new ManualLogSource Log = null!;

    public static bool Loaded { get; private set; }

    public override void Load()
    {
        Log = base.Log;

        // 注册瘦 API 的实现，供其它 Mod 通过 LanguagePackEnhanceApi 调用。
        LanguagePackEnhanceApi.Service = new LanguagePackEnhanceService();

        var harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        harmony.PatchAll();

        Loaded = true;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
    }
}

internal static class Diag
{
    public static void Info(string message) => Plugin.Log?.LogInfo($"[LPE] {message}");
    public static void Warn(string message) => Plugin.Log?.LogWarning($"[LPE] {message}");
    public static void Error(string message) => Plugin.Log?.LogError($"[LPE] {message}");
}
