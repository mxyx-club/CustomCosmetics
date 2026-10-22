namespace CustomCosmetics.CustomVisors.Patches;

[HarmonyPatch(typeof(HatManager))]
internal static class VisorManagerPatches
{
    private static bool _isRunning;

    [HarmonyPatch(nameof(HatManager.GetVisorById))]
    [HarmonyPrefix]
    private static void GetVisorByIdPrefix(HatManager __instance)
    {
        if (_isRunning || CustomVisorManager.ReadyVisors.Count == 0) return;
        _isRunning = true;
        // reset even on throw, otherwise visors can never be injected again this session
        try { CustomVisorManager.InjectReady(__instance, CustomVisorManager.InjectPerFrame); }
        finally { _isRunning = false; }
    }

    [HarmonyPatch(typeof(CosmeticsCache), nameof(CosmeticsCache.GetVisor))]
    [HarmonyPrefix]
    private static bool GetVisorPrefix(string id, ref VisorViewData __result)
    {
        return !CustomVisorManager.ViewDataCache.TryGetValue(id, out __result);
    }
}
