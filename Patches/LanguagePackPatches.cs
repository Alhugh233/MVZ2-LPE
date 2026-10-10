#nullable enable

using HarmonyLib;
using MVZ2.Addons;
using MVZ2.Localization;
using MVZ2.UI.Addons;

namespace LanguagePackEnhance.Patches;

/// <summary>
/// 语言包加载完成后（<see cref="LanguageManager.ValidateCurrentLanguage"/> 在
/// InitLanguagePacks / ReloadLanguagePacks 的末尾同步调用），注入 Mod 自带语言包。
/// </summary>
[HarmonyPatch(typeof(LanguageManager), nameof(LanguageManager.ValidateCurrentLanguage))]
internal static class ValidateCurrentLanguagePatch
{
    private static void Postfix(LanguageManager __instance)
    {
        try
        {
            LanguagePackInjector.Inject(__instance);
        }
        catch (System.Exception e)
        {
            Diag.Error($"注入 Mod 自带语言包失败：{e}");
        }
    }
}

/// <summary>
/// 在语言包页刷新后，把「Mod 自带语言包」条目插入到内置包之上。
/// </summary>
[HarmonyPatch(typeof(LanguagePacksController), "UpdateLanguagePacks")]
internal static class UpdateLanguagePacksPatch
{
    private static void Postfix(LanguagePacksController __instance)
    {
        try
        {
            LanguagePacksEntryUI.Refresh(__instance);
        }
        catch (System.Exception e)
        {
            Diag.Error($"注入「Mod 自带语言包」条目失败：{e}");
        }
    }
}

/// <summary>
/// 虚拟条目插在槽位（<see cref="LanguagePacksEntryUI.EntrySlot"/>）处：
/// UI 下标 0..slot-1 对应 enabledReferences 同下标；slot 对应虚拟条目（无引用）；
/// slot+1..count 对应 enabledReferences 下标 -1。
/// </summary>
[HarmonyPatch(typeof(LanguagePacksController), "GetLanguagePackReferenceByUI")]
internal static class GetLanguagePackReferenceByUIPatch
{
    private static bool Prefix(LanguagePacksController __instance, bool __0, int __1, ref LanguagePackReference __result)
    {
        if (!__0 || !LanguagePacksEntryUI.ShouldShow)
            return true;

        var enabled = __instance.enabledReferences;
        int slot = LanguagePacksEntryUI.EntrySlot;
        if (__1 < 0 || __1 > enabled.Count || __1 == slot)
        {
            __result = null!;
            return false;
        }
        int index = __1 < slot ? __1 : __1 - 1;
        __result = index >= 0 && index < enabled.Count ? enabled[index] : null!;
        return false;
    }
}

/// <summary>把已启用引用索引映射为带虚拟条目的 UI 下标（槽位及之后的 +1）。</summary>
[HarmonyPatch(typeof(LanguagePacksController), "GetItemUIIndex")]
internal static class GetItemUIIndexPatch
{
    private static void Postfix(bool __0, ref int __result)
    {
        if (!__0 || __result < 0 || !LanguagePacksEntryUI.ShouldShow)
            return;
        if (__result >= LanguagePacksEntryUI.EntrySlot)
            __result += 1;
    }
}

/// <summary>记录虚拟条目是否被选中（用于接管上/下移）。</summary>
[HarmonyPatch(typeof(LanguagePacksController), "OnPackItemToggledCallback")]
internal static class OnPackItemToggledPatch
{
    private static void Postfix(LanguagePacksController __instance, bool __0, int __1, bool __2)
    {
        LanguagePacksEntryUI.SetEntrySelected(__0 && __1 == LanguagePacksEntryUI.EntrySlot && __2);
        if (LanguagePacksEntryUI.EntrySelected)
        {
            // 选中虚拟条目时原逻辑把所有按钮都禁用了，这里按槽位重设上/下移。
            __instance.UpdateButtonInteractions();
        }
    }
}

/// <summary>选中虚拟条目时，允许上/下移（但不能移到内置包下面）。</summary>
[HarmonyPatch(typeof(LanguagePacksController), "UpdateButtonInteractions")]
internal static class UpdateButtonInteractionsPatch
{
    private static void Postfix(LanguagePacksController __instance)
    {
        if (!LanguagePacksEntryUI.EntrySelected)
            return;
        var ui = __instance.ui;
        if (ui == null)
            return;
        ui.SetButtonInteractable(LanguagePacksUI.Buttons.MoveUp, LanguagePacksEntryUI.CanMove(-1, __instance));
        ui.SetButtonInteractable(LanguagePacksUI.Buttons.MoveDown, LanguagePacksEntryUI.CanMove(1, __instance));
    }
}

/// <summary>选中虚拟条目时接管上/下移按钮。</summary>
[HarmonyPatch(typeof(LanguagePacksController), "OnButtonClickCallback")]
internal static class OnButtonClickPatch
{
    private static bool Prefix(LanguagePacksController __instance, LanguagePacksUI.Buttons __0)
    {
        if (!LanguagePacksEntryUI.EntrySelected)
            return true;
        if (__0 == LanguagePacksUI.Buttons.MoveUp || __0 == LanguagePacksUI.Buttons.MoveDown)
        {
            LanguagePacksEntryUI.MoveEntry(__0 == LanguagePacksUI.Buttons.MoveUp ? -1 : 1, __instance);
            return false;
        }
        return true;
    }
}

/// <summary>取消选择时清除虚拟条目的选中状态。</summary>
[HarmonyPatch(typeof(LanguagePacksUI), nameof(LanguagePacksUI.DeselectAll))]
internal static class DeselectAllPatch
{
    private static void Postfix()
    {
        LanguagePacksEntryUI.SetEntrySelected(false);
    }
}
