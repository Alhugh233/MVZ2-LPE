#nullable enable

using System;
using HarmonyLib;
using MVZ2.Addons;
using MVZ2.Managers;
using MVZ2.ResourcePacks;

namespace LanguagePackEnhance.Patches;

/// <summary>
/// 资源包加载/重排完成后，重新注入 Mod 自带语言包
/// （<see cref="ResourcePackManager.ReorderResourcePacks"/> 在加载与重排后同步调用）。
/// </summary>
[HarmonyPatch(typeof(ResourcePackManager), "ReorderResourcePacks")]
internal static class ReorderResourcePacksPatch
{
    private static void Postfix()
    {
        try
        {
            LanguagePackInjector.Reinject();
        }
        catch (Exception e)
        {
            Diag.Error($"注入 Mod 自带语言包失败：{e}");
        }
    }
}

/// <summary>启动兜底：游戏初始化后也尝试注入一次。</summary>
[HarmonyPatch(typeof(MainManager), nameof(MainManager.Initialize))]
internal static class StartupInjectPatch
{
    private static void Postfix()
    {
        try
        {
            LanguagePackInjector.Reinject();
        }
        catch (Exception e)
        {
            Diag.Error($"启动注入 Mod 自带语言包失败：{e}");
        }
    }
}

/// <summary>
/// 在资源包页刷新后，把「Mod 自带语言包」条目插入到内置包之上。
/// </summary>
[HarmonyPatch(typeof(ResourcePacksController), "UpdateLanguagePacks")]
internal static class UpdateLanguagePacksPatch
{
    private static void Postfix(ResourcePacksController __instance)
    {
        try
        {
            LanguagePacksEntryUI.Refresh(__instance);
        }
        catch (Exception e)
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
[HarmonyPatch(typeof(ResourcePacksController), "GetLanguagePackReferenceByUI")]
internal static class GetLanguagePackReferenceByUIPatch
{
    private static bool Prefix(ResourcePacksController __instance, bool __0, int __1, ref ResourcePackReference __result)
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
[HarmonyPatch(typeof(ResourcePacksController), "GetItemUIIndex")]
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
[HarmonyPatch(typeof(ResourcePacksController), "OnPackItemToggledCallback")]
internal static class OnPackItemToggledPatch
{
    private static void Postfix(ResourcePacksController __instance, bool __0, int __1, bool __2)
    {
        LanguagePacksEntryUI.SetEntrySelected(__0 && __1 == LanguagePacksEntryUI.EntrySlot && __2);
        if (LanguagePacksEntryUI.EntrySelected)
        {
            __instance.UpdateButtonInteractions();
        }
    }
}

/// <summary>选中虚拟条目时，允许上/下移（但不能移到内置包下面）。</summary>
[HarmonyPatch(typeof(ResourcePacksController), "UpdateButtonInteractions")]
internal static class UpdateButtonInteractionsPatch
{
    private static void Postfix(ResourcePacksController __instance)
    {
        if (!LanguagePacksEntryUI.EntrySelected)
            return;
        var ui = __instance.ui;
        if (ui == null)
            return;
        ui.SetButtonInteractable(ResourcePacksUI.Buttons.MoveUp, LanguagePacksEntryUI.CanMove(-1, __instance));
        ui.SetButtonInteractable(ResourcePacksUI.Buttons.MoveDown, LanguagePacksEntryUI.CanMove(1, __instance));
    }
}

/// <summary>选中虚拟条目时接管上/下移按钮。</summary>
[HarmonyPatch(typeof(ResourcePacksController), "OnButtonClickCallback")]
internal static class OnButtonClickPatch
{
    private static bool Prefix(ResourcePacksController __instance, ResourcePacksUI.Buttons __0)
    {
        if (!LanguagePacksEntryUI.EntrySelected)
            return true;
        if (__0 == ResourcePacksUI.Buttons.MoveUp || __0 == ResourcePacksUI.Buttons.MoveDown)
        {
            LanguagePacksEntryUI.MoveEntry(__0 == ResourcePacksUI.Buttons.MoveUp ? -1 : 1, __instance);
            return false;
        }
        return true;
    }
}

/// <summary>取消选择时清除虚拟条目的选中状态。</summary>
[HarmonyPatch(typeof(ResourcePacksUI), nameof(ResourcePacksUI.DeselectAll))]
internal static class DeselectAllPatch
{
    private static void Postfix()
    {
        LanguagePacksEntryUI.SetEntrySelected(false);
    }
}
