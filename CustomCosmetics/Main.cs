global using HarmonyLib;
global using static CustomCosmetics.Helpers;
global using static CustomCosmetics.Logger;
global using ISystem = Il2CppSystem.Collections.Generic;
global using Main = CustomCosmetics.CosmeticsManager;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using CustomCosmetics.CustomHats;
using System.IO;

namespace CustomCosmetics;

[BepInAutoPlugin("com.mxyx.cosmetics")]
[BepInProcess("Among Us.exe")]
public partial class CosmeticsManager : BasePlugin
{
    internal static string CosmeticDir = Path.Combine(Paths.GameRootPath, "Cosmetics");
    internal static string CustomHatsDir => Path.Combine(CosmeticDir, "CustomHats");
    internal static string CustomVisorsDir => Path.Combine(CosmeticDir, "CustomVisors");
    internal static string CustomPlatesDir => Path.Combine(CosmeticDir, "CustomPlates");
    internal static string CosmeticsConfigDir => Path.Combine(CosmeticDir, "CosmeticsConfig");

    public static CosmeticsManager Instance { get; set; }
    public Harmony Harmony { get; } = new(Id);

    internal static string RepositoryUrl => Repository.Value.GithubUrl();
    public static ConfigEntry<string> Repository { get; set; }
    public static ConfigEntry<bool> LocalHats { get; set; }
    public static ConfigEntry<bool> CosmeticsUnlocker { get; set; }

    public override void Load()
    {
        SetLogSource(Log);
        Harmony.PatchAll();

        CosmeticsUnlocker = Config.Bind("General", "Cosmetics Unlocker", false, "Unlock all cosmetics in the game, including paid ones.");

        LocalHats = Config.Bind("CustomHats", "Local Hats", false,
            "Enable to only use local hat files without downloading from online repository");
        Repository = Config.Bind("CustomHats", "Repository Source", "https://raw.githubusercontent.com/TheOtherRolesAU/TheOtherHats/master"
            , "URL for downloading custom hats when Local Hats is disabled");

        Instance = this;
        CustomHatManager.LoadHats();
        Message("CosmeticsManager Loading!");
    }

}