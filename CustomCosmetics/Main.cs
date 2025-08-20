global using static CustomCosmetics.Helpers;
global using static CustomCosmetics.Logger;
global using Main = CustomCosmetics.CosmeticsManager;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using CustomCosmetics.CustomHats;
using HarmonyLib;

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

    public override void Load()
    {
        SetLogSource(Log);
        Harmony.PatchAll();

        Repository = Config.Bind("CustomHats", "Repository Source", "https://raw.githubusercontent.com/TheOtherRolesAU/TheOtherHats/master");
        LocalHats = Config.Bind("Custom", "Local Hats", false);

        Instance = this;
        CustomHatManager.LoadHats();
        Message("CosmeticsManager Loading!");
    }

}