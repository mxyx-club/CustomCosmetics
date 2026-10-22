using AmongUs.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace CustomCosmetics.CustomHats.Patches;

[HarmonyPatch(typeof(HatsTab))]
internal static class HatsTabPatches
{
    private static TextMeshPro _textTemplate;

    [HarmonyPatch(nameof(HatsTab.OnEnable))]
    [HarmonyPrefix]
    private static bool OnEnablePrefix(HatsTab __instance)
    {
        // pending coroutine callbacks would touch destroyed chips
        __instance.StopAllCoroutines();
        Core.Helpers.DestroyChildren(__instance.scroller.Inner);

        __instance.ColorChips = new ISystem.List<ColorChip>();
        var hatManager = DestroyableSingleton<HatManager>.Instance;
        if (hatManager == null) return false;

        // inject before snapshotting, otherwise InsertNoHat rescans mid-loop
        CustomHatManager.PromoteAll();
        CustomHatManager.InjectReady(hatManager, CustomHatManager.InjectPerFrame);
        var unlockedHats = hatManager.GetUnlockedHats();
        var packages = new Dictionary<string, List<Tuple<HatData, HatExtension>>>();

        foreach (var hatBehaviour in unlockedHats)
        {
            var extension = hatBehaviour.GetHatExtension();
            if (extension != null)
            {
                if (!packages.ContainsKey(extension.Package))
                    packages[extension.Package] = new List<Tuple<HatData, HatExtension>>();
                packages[extension.Package].Add(new Tuple<HatData, HatExtension>(hatBehaviour, extension));
            }
            else
            {
                if (!packages.ContainsKey(CustomHatManager.InnerslothPackageName))
                    packages[CustomHatManager.InnerslothPackageName] = new List<Tuple<HatData, HatExtension>>();
                packages[CustomHatManager.InnerslothPackageName].Add(new Tuple<HatData, HatExtension>(hatBehaviour, null));
            }
        }

        var yOffset = __instance.YStart;
        _textTemplate = GameObject.Find("HatsGroup")?.transform.FindChild("Text")?.GetComponent<TextMeshPro>();

        var packageOrder = Core.Helpers.OrderPackageKeys(packages.Keys, CustomHatManager.InnerslothPackageName,
            CustomHatManager.PackagePriorities);
        foreach (var packageName in packageOrder)
        {
            var hats = packages[packageName];
            InsertNoHat(hats, hatManager);
            yOffset = CreateHatPackage(hats, packageName, yOffset, __instance);
        }

        __instance.scroller.ContentYBounds.max = -(yOffset + 4.1f);
        return false;
    }

    private static void InsertNoHat(List<Tuple<HatData, HatExtension>> hats, HatManager hatManager)
    {
        // empty hat at head of every package (vanilla hat_NoHat)
        if (hats.Count > 0 && hats[0].Item1.ProductId == "hat_NoHat") return;
        var noHat = hatManager.GetHatById("hat_NoHat");
        if (noHat != null)
            hats.Insert(0, new Tuple<HatData, HatExtension>(noHat, null));
    }

    private static float CreateHatPackage(List<Tuple<HatData, HatExtension>> hats, string packageName, float yStart,
        HatsTab tab)
    {
        var isDefaultPackage = CustomHatManager.InnerslothPackageName == packageName;
        if (!isDefaultPackage) hats = hats.ToList();

        var offset = yStart;
        if (_textTemplate != null)
        {
            var title = UObject.Instantiate(_textTemplate, tab.scroller.Inner);
            title.transform.localPosition = new Vector3(tab.XRange.Lerp(0.5f), yStart + 0.15f * tab.YOffset, -1f);
            title.alignment = TextAlignmentOptions.Center;
            title.transform.localScale = Vector3.one * 1.5f;
            title.fontSize *= 0.5f;
            title.enableAutoSizing = false;
            var displayName = isDefaultPackage
                ? packageName
                : (CustomHatManager.PackageDisplayNames.TryGetValue(packageName, out var customName) ? customName : packageName);
            tab.StartCoroutine(Effects.Lerp(0.1f, new Action<float>(_ => { title.SetText(displayName); })));
            offset -= 1.0f * tab.YOffset;
        }

        for (var i = 0; i < hats.Count; i++)
        {
            var (hat, extension) = hats[i];
            var xPos = tab.XRange.Lerp(i % tab.NumPerRow / (tab.NumPerRow - 1f));
            var yPos = offset - (i / tab.NumPerRow * (isDefaultPackage ? 1f : 1.5f) * tab.YOffset);
            var colorChip = UObject.Instantiate(tab.ColorTabPrefab, tab.scroller.Inner);
            if (ActiveInputManager.currentControlType == ActiveInputManager.InputType.Keyboard)
            {
                colorChip.Button.OnMouseOver.AddListener((Action)(() => tab.SelectHat(hat)));
                colorChip.Button.OnMouseOut.AddListener((Action)(() =>
                    tab.SelectHat(DestroyableSingleton<HatManager>.Instance.GetHatById(DataManager.Player.Customization.Hat))));
                colorChip.Button.OnClick.AddListener((Action)tab.ClickEquip);
            }
            else
            {
                colorChip.Button.OnClick.AddListener((Action)(() => tab.SelectHat(hat)));
            }
            colorChip.Button.ClickMask = tab.scroller.Hitbox;
            colorChip.Inner.SetMaskType(PlayerMaterial.MaskType.ScrollingUI);
            tab.UpdateMaterials(colorChip.Inner.FrontLayer, hat);
            var background = colorChip.transform.FindChild("Background");
            var foreground = colorChip.transform.FindChild("ForeGround");

            if (!isDefaultPackage)
            {
                if (background != null)
                    background.localScale = new Vector3(background.localScale.x, 0.75f, background.localScale.y);
                if (foreground != null)
                    foreground.localScale = new Vector3(foreground.localScale.x, 0.98f, foreground.localScale.y);
                var shade = colorChip.transform.FindChild("Shade") ?? foreground?.FindChild("Shade");
                if (shade != null)
                    shade.localScale = new Vector3(shade.localScale.x, 0.8f, shade.localScale.y);
            }

            if (extension != null && _textTemplate != null)
            {
                var description = UObject.Instantiate(_textTemplate, colorChip.transform);
                description.transform.localPosition = new Vector3(0f, -0.65f, -1f);
                description.alignment = TextAlignmentOptions.Center;
                description.transform.localScale = Vector3.one * 0.65f;
                tab.StartCoroutine(Effects.Lerp(0.1f,
                   new Action<float>(_ => { description.SetText($"{hat.name}\nby {extension.Author}"); })));
            }

            colorChip.transform.localPosition = new Vector3(xPos, yPos, -1f);
            colorChip.Inner.SetHat(hat,
                tab.HasLocalPlayer()
                    ? PlayerControl.LocalPlayer.Data.DefaultOutfit.ColorId
                    : DataManager.Player.Customization.Color);
            colorChip.Inner.transform.localPosition = hat.ChipOffset;
            colorChip.Tag = hat;
            colorChip.SelectionHighlight.gameObject.SetActive(false);
            tab.ColorChips.Add(colorChip);
        }

        return offset - ((hats.Count - 1) / tab.NumPerRow * (isDefaultPackage ? 1f : 1.5f) * tab.YOffset) -
               1.75f;
    }

    [HarmonyPatch(nameof(HatsTab.SelectHat))]
    [HarmonyPostfix]
    private static void SelectHatPostfix(HatData hat)
    {
        // item name: custom hats show "{name}\nby {author}"
        Core.Helpers.SetCustomItemName(hat.ProductId, hat.name, "cmh_", CustomHatManager.AuthorCache);
    }
}