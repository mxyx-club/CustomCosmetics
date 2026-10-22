using Cpp2IL.Core.Extensions;
using System.Linq;

namespace CustomCosmetics.CustomPlates.Patches;

[HarmonyPatch(typeof(HatManager))]
internal static class NamePlateManagerPatches
{
    private static bool _isLoaded;

    [HarmonyPatch(nameof(HatManager.GetNamePlateById))]
    [HarmonyPrefix]
    private static void GetNamePlateByIdPrefix(HatManager __instance)
    {
        // custom plates injected into allNamePlates so vanilla enumerables see them
        if (_isLoaded || CustomNamePlateManager.UnregisteredPlates.Count == 0) return;

        var all = __instance.allNamePlates.ToList();
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
        __instance.allNamePlates = all.OrderBy(p => p.displayOrder).ToArray();
        if (CustomNamePlateManager.UnregisteredPlates.Count == 0) _isLoaded = true;
    }

    [HarmonyPatch(typeof(CosmeticsCache), nameof(CosmeticsCache.GetNameplate))]
    [HarmonyPrefix]
    private static bool GetNameplatePrefix(CosmeticsCache __instance, string id, ref NamePlateViewData __result)
    {
        // shortcut: custom -> ViewDataCache, fallback NoPlate (same as vanilla empty plate)
        if (!id.StartsWith("cmp_")) return true;
        CustomNamePlateManager.ViewDataCache.TryGetValue(id, out __result);
        if (__result == null)
            __result = __instance.nameplates["nameplate_NoPlate"].GetAsset();
        return false;
    }
}
