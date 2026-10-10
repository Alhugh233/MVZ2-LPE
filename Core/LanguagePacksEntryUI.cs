#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MVZ2.Addons;
using MVZ2.Managers;
using MVZ2.Sprites;
using MVZ2.UI.Addons;
using UnityEngine;

namespace LanguagePackEnhance;

/// <summary>
/// 在游戏「语言包」界面中注入一条虚拟的「Mod 自带语言包」条目。
/// 它可在已启用列表中上/下移动，但始终位于内置包之上（不允许排到内置包下面）。
/// </summary>
internal static class LanguagePacksEntryUI
{
    public const string EntryName = "Mod 自带语言包";
    private const string IconResourceName = "LanguagePackEnhance.icon.png";

    /// <summary>虚拟条目在已启用列表中的 UI 下标。</summary>
    public static int EntrySlot { get; private set; }

    /// <summary>虚拟条目当前是否被选中。</summary>
    public static bool EntrySelected { get; private set; }

    private static bool slotInitialized;

    public static bool ShouldShow => ModLanguagePackRegistry.Count > 0 || DynamicTranslationStore.HasAny;

    public static void SetEntrySelected(bool value)
    {
        EntrySelected = value;
    }

    public static void Refresh(LanguagePacksController controller)
    {
        if (!ShouldShow)
            return;

        var ui = controller.ui;
        if (ui == null)
            return;

        var enabled = controller.enabledReferences;
        var languageManager = MainManager.Instance?.LanguageManager;

        int count = enabled?.Count ?? 0;
        int builtinIndex = FindBuiltinIndex(controller);

        // 首次默认放在内置包之上；随后限制在 [0, 内置包下标]，保证始终在内置包之上。
        if (!slotInitialized)
        {
            EntrySlot = builtinIndex;
            slotInitialized = true;
        }
        if (EntrySlot > builtinIndex)
            EntrySlot = builtinIndex;
        if (EntrySlot < 0)
            EntrySlot = 0;

        var viewDatas = new Il2CppReferenceArray<LanguagePackViewData>(count + 1);
        for (int s = 0; s <= count; s++)
        {
            if (s == EntrySlot)
            {
                viewDatas[s] = new LanguagePackViewData
                {
                    name = EntryName,
                    description = BuildDescription(),
                    icon = GetEntryIcon(),
                };
            }
            else
            {
                int refIndex = s < EntrySlot ? s : s - 1;
                var metadata = languageManager?.GetLanguagePackMetadata(enabled![refIndex]);
                viewDatas[s] = metadata == null
                    ? new LanguagePackViewData()
                    : new LanguagePackViewData
                    {
                        name = metadata.name ?? string.Empty,
                        description = metadata.description ?? string.Empty,
                        icon = metadata.icon,
                    };
            }
        }

        ui.SetEnabledLanguagePacks(viewDatas);
    }

    public static bool CanMove(int delta, LanguagePacksController controller)
    {
        int target = EntrySlot + delta;
        return target >= 0 && target <= FindBuiltinIndex(controller);
    }

    public static void MoveEntry(int delta, LanguagePacksController controller)
    {
        if (!CanMove(delta, controller))
            return;
        EntrySlot += delta;
        controller.UpdateLanguagePacks();
        controller.UpdateButtonInteractions();
        controller.ui?.SelectItemUI(true, EntrySlot);
    }

    private static int FindBuiltinIndex(LanguagePacksController controller)
    {
        var enabled = controller.enabledReferences;
        int count = enabled?.Count ?? 0;
        for (int i = 0; i < count; i++)
        {
            var reference = enabled![i];
            if (reference != null && reference.IsBuiltin)
                return i;
        }
        return count;
    }

    private static string BuildDescription()
    {
        var names = new List<string>();
        foreach (var registration in ModLanguagePackRegistry.Registrations)
        {
            if (!names.Contains(registration.Nsp))
                names.Add(registration.Nsp);
        }
        foreach (var nsp in DynamicTranslationStore.All.Keys)
        {
            if (!names.Contains(nsp))
                names.Add(nsp);
        }
        return string.Join("、", names);
    }

    private static Sprite? entryIcon;
    private static bool iconResolved;

    private static Sprite? GetEntryIcon()
    {
        if (iconResolved)
            return entryIcon;
        iconResolved = true;
        try
        {
            using var stream = typeof(LanguagePacksEntryUI).Assembly.GetManifestResourceStream(IconResourceName);
            if (stream == null)
            {
                Diag.Warn($"找不到内嵌图标资源 '{IconResourceName}'。");
                return null;
            }
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            var texture = SpriteHelper.LoadTextureFromBytes(memory.ToArray());
            if (texture == null)
                return null;
            var resourceManager = MainManager.Instance?.ResourceManager;
            if (resourceManager == null)
                return null;
            entryIcon = resourceManager.CreateSprite(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(texture.width * 0.5f, texture.height * 0.5f),
                "lpe-mod-langpack-icon",
                "lpe_mod_icon");
            return entryIcon;
        }
        catch (Exception e)
        {
            Diag.Error($"加载 LPE 图标失败：{e}");
            return null;
        }
    }
}
