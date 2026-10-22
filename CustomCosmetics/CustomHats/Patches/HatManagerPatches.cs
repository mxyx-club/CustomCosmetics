namespace CustomCosmetics.CustomHats.Patches;

[HarmonyPatch(typeof(HatManager))]
internal static class HatManagerPatches
{
    private static bool _isRunning;

    [HarmonyPatch(nameof(HatManager.GetHatById))]
    [HarmonyPrefix]
    private static void GetHatByIdPrefix(HatManager __instance)
    {
        if (_isRunning || CustomHatManager.ReadyHats.Count == 0) return;
        _isRunning = true;
        CustomHatManager.InjectReady(__instance, CustomHatManager.InjectPerFrame);
    }

    [HarmonyPatch(nameof(HatManager.GetHatById))]
    [HarmonyPostfix]
    private static void GetHatByIdPostfix()
    {
        _isRunning = false;
    }

    [HarmonyPatch(typeof(CosmeticsCache), nameof(CosmeticsCache.GetHat))]
    [HarmonyPrefix]
    private static bool GetHatPrefix(string id, ref HatViewData __result)
    {
        return !CustomHatManager.ViewDataCache.TryGetValue(id, out __result);
    }
}
