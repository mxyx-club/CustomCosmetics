using AmongUs.Data;
using Innersloth.Assets;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace CustomCosmetics.CustomVisors.Patches;

[HarmonyPatch(typeof(VisorsTab))]
internal static class VisorsTabPatches
{
    private static TextMeshPro _textTemplate;

    [HarmonyPatch(nameof(VisorsTab.OnEnable))]
    [HarmonyPrefix]
    private static bool OnEnablePrefix(VisorsTab __instance)
    {
        try
        {
            var hatManager = DestroyableSingleton<HatManager>.Instance;
            if (hatManager == null) return false;

            // fallback: GetVisorById never runs when visors stay disabled at launch
            CustomVisorManager.PromoteAll();
            CustomVisorManager.InjectReady(hatManager, CustomVisorManager.InjectPerFrame);

            var allVisors = hatManager.allVisors;
            if (allVisors == null) return false;

            var packages = new Dictionary<string, List<VisorData>>();
            foreach (var visor in allVisors)
            {
                if (visor == null) continue;
                var packageName = CustomVisorManager.PackageCache.TryGetValue(visor.name, out var package)
                    ? package
                    : "Innersloth";
                if (!packages.ContainsKey(packageName))
                    packages[packageName] = new List<VisorData>();
                packages[packageName].Add(visor);
            }

            // stop vanilla preview coroutines before destroying chips (avoid NRE on destroyed objects)
            __instance.StopAllCoroutines();
            Core.Helpers.DestroyChildren(__instance.scroller.Inner);
            __instance.ColorChips = new ISystem.List<ColorChip>();

            var yOffset = __instance.YStart - 0.5f * __instance.YOffset;
            _textTemplate = Core.Helpers.GetTitleTemplate(__instance, ref _textTemplate);

            var packageOrder = Core.Helpers.OrderPackageKeys(packages.Keys, "Innersloth",
                CustomVisorManager.PackagePriorities);
            foreach (var packageName in packageOrder)
            {
                var visors = packages[packageName];
                InsertNoVisor(visors, hatManager);
                yOffset = CreatePackage(visors, packageName, yOffset, __instance);
            }

            __instance.scroller.ContentYBounds.max = -(yOffset + 4.1f);
            return false;
        }
        catch (Exception ex)
        {
            Error($"tab rebuild failed: {ex}", "visors");
            return false;
        }
    }

    private static void InsertNoVisor(List<VisorData> visors, HatManager hatManager)
    {
        // empty visor at head of every package (vanilla visor_EmptyVisor)
        if (visors.Count > 0 && visors[0].ProductId == "visor_EmptyVisor") return;
        var noVisor = hatManager.GetVisorById("visor_EmptyVisor");
        if (noVisor != null)
            visors.Insert(0, noVisor);
    }

    private static float CreatePackage(List<VisorData> visors, string packageName, float yStart, VisorsTab tab)
    {
        var isVanilla = packageName == "Innersloth";
        var offset = yStart;

        if (_textTemplate != null)
        {
            var title = UObject.Instantiate(_textTemplate, tab.scroller.Inner);
            title.gameObject.SetActive(true);
            title.rectTransform.sizeDelta = new Vector2(5f, 1f);
            title.transform.localPosition = new Vector3(tab.XRange.Lerp(0.5f), yStart + 0.4f * tab.YOffset, -1f);
            title.alignment = TextAlignmentOptions.Center;
            title.transform.localScale = Vector3.one * 1.5f;
            title.fontSize *= 0.5f;
            title.enableAutoSizing = false;
            var displayName = isVanilla
                ? packageName
                : (CustomVisorManager.PackageDisplayNames.TryGetValue(packageName, out var customName)
                    ? customName
                    : packageName);
            tab.StartCoroutine(Effects.Lerp(0.1f, new Action<float>(_ => title.SetText(displayName))));
            offset -= 0.6f * tab.YOffset;
        }

        for (var i = 0; i < visors.Count; i++)
        {
            var visor = visors[i];
            var xPos = tab.XRange.Lerp(i % tab.NumPerRow / (tab.NumPerRow - 1f));
            var yPos = offset - (i / tab.NumPerRow * tab.YOffset);
            var chip = UObject.Instantiate(tab.ColorTabPrefab, tab.scroller.Inner);
            // vanilla VisorsTab.Update compares chip.ProductId
            chip.ProductId = visor.ProductId;

            if (ActiveInputManager.currentControlType == ActiveInputManager.InputType.Keyboard)
            {
                chip.Button.OnMouseOver.AddListener((Action)(() => tab.SelectVisor(visor)));
                chip.Button.OnMouseOut.AddListener((Action)(() =>
                    tab.SelectVisor(DestroyableSingleton<HatManager>.Instance.GetVisorById(DataManager.Player.Customization.Visor))));
                chip.Button.OnClick.AddListener((Action)tab.ClickEquip);
            }
            else
            {
                chip.Button.OnClick.AddListener((Action)(() => tab.SelectVisor(visor)));
            }
            chip.Button.ClickMask = tab.scroller.Hitbox;
            if (chip.Inner != null)
            {
                chip.Inner.SetMaskType(PlayerMaterial.MaskType.ScrollingUI);
                tab.UpdateMaterials(chip.Inner.FrontLayer, visor);
            }

            chip.transform.localPosition = new Vector3(xPos, yPos, -1f);
            var renderer = FindVisorSprite(chip);
            if (renderer != null)
            {
                if (CustomVisorManager.ViewDataCache.TryGetValue(visor.name, out var viewData))
                {
                    renderer.sprite = viewData.IdleFrame;
                    renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                }
                else
                {
                    var chipRenderer = renderer;
                    tab.StartCoroutine(AddressableAssetExtensions.CoLoadAssetAsync<VisorViewData>(tab,
                        visor.ViewDataRef,
                        (Action<VisorViewData>)(loaded =>
                        {
                            if (loaded != null && chipRenderer != null)
                            {
                                chipRenderer.sprite = loaded.IdleFrame;
                                chipRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                            }
                        })));
                }
            }
            chip.Tag = visor;
            chip.SelectionHighlight.gameObject.SetActive(false);
            tab.ColorChips.Add(chip);
        }

        return offset - ((visors.Count - 1) / tab.NumPerRow * tab.YOffset) - 1.75f;
    }

    private static SpriteRenderer FindVisorSprite(ColorChip chip)
    {
        // visor chip has no Inner(HatParent); sprite lives at GetChild(2).GetChild(0)
        if (chip == null) return null;
        var root = chip.transform;

        if (root.childCount > 2)
        {
            var child = root.GetChild(2);
            if (child != null && child.childCount > 0)
            {
                var spriteRenderer = child.GetChild(0).GetComponent<SpriteRenderer>();
                if (spriteRenderer != null) return spriteRenderer;
            }
        }

        foreach (var spriteRenderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            if (spriteRenderer != null && spriteRenderer.sprite == null) return spriteRenderer;

        var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        return renderers.Length > 0 ? renderers[^1] : null;
    }

    [HarmonyPatch(nameof(VisorsTab.SelectVisor))]
    [HarmonyPostfix]
    private static void SelectVisorPostfix(VisorData visor)
    {
        // item name: custom visors show "{name}\nby {author}"
        Core.Helpers.SetCustomItemName(visor.ProductId, visor.name, "cmv_", CustomVisorManager.AuthorCache);
    }
}
