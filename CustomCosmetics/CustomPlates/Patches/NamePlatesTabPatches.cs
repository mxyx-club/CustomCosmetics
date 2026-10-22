using AmongUs.Data;
using Cpp2IL.Core.Extensions;
using Innersloth.Assets;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace CustomCosmetics.CustomPlates.Patches;

[HarmonyPatch(typeof(NameplatesTab))]
internal static class NameplatesTabPatches
{
    private static bool _injected;
    private static TextMeshPro _textTemplate;

    private static void EnsureInjectedPlates(HatManager hatManager)
    {
        if (_injected || CustomNamePlateManager.UnregisteredPlates.Count == 0) return;

        var all = hatManager.allNamePlates.ToList();
        var cache = CustomNamePlateManager.UnregisteredPlates.Clone();
        foreach (var c in cache)
        {
            try
            {
                all.Add(CustomNamePlateManager.CreatePlateBehaviour(c));
                CustomNamePlateManager.UnregisteredPlates.Remove(c);
            }
            catch { }
        }
        hatManager.allNamePlates = all.OrderBy(p => p.displayOrder).ToArray();
        if (CustomNamePlateManager.UnregisteredPlates.Count == 0) _injected = true;
    }

    [HarmonyPatch(nameof(NameplatesTab.OnEnable))]
    [HarmonyPrefix]
    private static bool OnEnablePrefix(NameplatesTab __instance)
    {
        try
        {
            var hatManager = DestroyableSingleton<HatManager>.Instance;
            if (hatManager == null) return false;
            EnsureInjectedPlates(hatManager);

            if (hatManager.allNamePlates == null) return false;
            var ordered = hatManager.allNamePlates.OrderBy(p => p.displayOrder).ToList();

            var packages = new Dictionary<string, List<NamePlateData>>();
            foreach (var p in ordered)
            {
                if (p == null) continue;
                var pkg = CustomNamePlateManager.PackageCache.TryGetValue(p.name, out var pn) ? pn : "Innersloth";
                if (!packages.ContainsKey(pkg))
                    packages[pkg] = new List<NamePlateData>();
                packages[pkg].Add(p);
            }

            // stop vanilla CoLoadNameplatePreview coroutines before destroying chips
            __instance.StopAllCoroutines();
            Core.Helpers.DestroyChildren(__instance.scroller.Inner);
            __instance.ColorChips = new ISystem.List<ColorChip>();

            var yOffset = __instance.YStart - 0.5f * __instance.YOffset;
            _textTemplate = Core.Helpers.GetTitleTemplate(__instance, ref _textTemplate);

            var orderedKeys = Core.Helpers.OrderPackageKeys(packages.Keys, "Innersloth",
                CustomNamePlateManager.PackagePriorities);
            foreach (var key in orderedKeys)
            {
                var list = packages[key];
                InsertNoPlate(list, hatManager);
                yOffset = CreatePlatePackage(list, key, yOffset, __instance);
            }

            __instance.scroller.ContentYBounds.max = -(yOffset + 4.1f);
            return false;
        }
        catch (Exception ex)
        {
            Error($"tab rebuild failed: {ex}", "nameplates");
            return false;
        }
    }

    private static void InsertNoPlate(List<NamePlateData> plates, HatManager hatManager)
    {
        // no-plate (vanilla white plate) at head of every package
        if (plates.Count > 0 && plates[0].ProductId == "nameplate_NoPlate") return;
        var noPlate = hatManager.GetNamePlateById("nameplate_NoPlate");
        if (noPlate != null)
            plates.Insert(0, noPlate);
    }

    private static float CreatePlatePackage(List<NamePlateData> plates, string pkg, float yStart, NameplatesTab tab)
    {
        var isVanilla = pkg == "Innersloth";
        var offset = yStart;

        if (_textTemplate != null)
        {
            var title = UObject.Instantiate(_textTemplate, tab.scroller.Inner);
            title.gameObject.SetActive(true);
            title.rectTransform.sizeDelta = new Vector2(5f, 1f);
            title.transform.localPosition = new Vector3(tab.XRange.Lerp(0.5f), yStart + 0.5f * tab.YOffset, -1f);
            title.alignment = TextAlignmentOptions.Center;
            title.transform.localScale = Vector3.one * 1.5f;
            title.fontSize *= 0.5f;
            title.enableAutoSizing = false;
            var displayName = isVanilla
                ? pkg
                : (CustomNamePlateManager.PackageDisplayNames.TryGetValue(pkg, out var dn) ? dn : pkg);
            tab.StartCoroutine(Effects.Lerp(0.1f, new Action<float>(_ => title.SetText(displayName))));
            offset -= 0.6f * tab.YOffset;
        }

        for (var i = 0; i < plates.Count; i++)
        {
            var plate = plates[i];
            var xPos = tab.XRange.Lerp(i % tab.NumPerRow / (tab.NumPerRow - 1f));
            var yPos = offset - (i / tab.NumPerRow * 0.8f * tab.YOffset);
            var chip = UObject.Instantiate(tab.ColorTabPrefab, tab.scroller.Inner);
            chip.ProductId = plate.ProductId;
            chip.Tag = plate;

            if (ActiveInputManager.currentControlType == ActiveInputManager.InputType.Keyboard)
            {
                chip.Button.OnMouseOver.AddListener((Action)(() => tab.SelectNameplate(plate)));
                chip.Button.OnMouseOut.AddListener((Action)(() =>
                {
                    var current = DestroyableSingleton<HatManager>.Instance.GetNamePlateById(DataManager.Player.Customization.NamePlate);
                    if (current != null) tab.SelectNameplate(current);
                }));
                chip.Button.OnClick.AddListener((Action)tab.ClickEquip);
            }
            else
            {
                chip.Button.OnClick.AddListener((Action)(() => tab.SelectNameplate(plate)));
            }
            chip.Button.ClickMask = tab.scroller.Hitbox;

            var npc = chip.GetComponent<NameplateChip>();
            if (npc != null)
            {
                // custom: sync ViewDataCache; vanilla: GUID async load
                if (CustomNamePlateManager.ViewDataCache.TryGetValue(plate.ProductId, out var vd) && vd.Image != null)
                {
                    npc.image.sprite = vd.Image;
                }
                else if (!plate.ProductId.StartsWith("cmp_") && !string.IsNullOrEmpty(plate.ViewDataRef?.AssetGUID))
                {
                    var imageRef = npc.image;
                    tab.StartCoroutine(AddressableAssetExtensions.CoLoadAssetAsync<NamePlateViewData>(tab,
                        plate.ViewDataRef,
                        (Action<NamePlateViewData>)(vd2 =>
                        {
                            if (vd2 != null && imageRef != null)
                                imageRef.sprite = vd2.Image;
                        })));
                }
            }

            chip.transform.localPosition = new Vector3(xPos, yPos, -1f);
            chip.SelectionHighlight.gameObject.SetActive(false);
            tab.ColorChips.Add(chip);
        }

        return offset - ((plates.Count - 1) / tab.NumPerRow * 0.8f * tab.YOffset) - 1.3f;
    }

    [HarmonyPatch(nameof(NameplatesTab.SelectNameplate))]
    [HarmonyPostfix]
    private static void SelectNameplatePostfix(NamePlateData plate)
    {
        // item name: custom plates show "{name}\nby {author}"
        Core.Helpers.SetCustomItemName(plate.ProductId, plate.name, "cmp_", CustomNamePlateManager.AuthorCache);
    }
}
