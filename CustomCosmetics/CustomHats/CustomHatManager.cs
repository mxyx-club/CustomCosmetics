using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace CustomCosmetics.CustomHats;

public static class CustomHatManager
{
    public const string InnerslothPackageName = "Innersloth Hats";

    internal static List<CustomHatConfig> UnregisteredHats = new();
    // entries whose resources are all on disk; hot paths only touch this list
    internal static readonly List<CustomHatConfig> ReadyHats = new();
    // cache key: hat.name
    internal static readonly Dictionary<string, HatViewData> ViewDataCache = new();
    internal static readonly Dictionary<string, HatExtension> ExtensionCache = new();
    internal static readonly Dictionary<string, string> AuthorCache = new();
    internal static readonly Dictionary<string, string> PackageDisplayNames = new();
    internal static readonly Dictionary<string, int> PackagePriorities = new();

    internal static HatExtension TestExtension { get; private set; }

    internal static bool TryGetCached(this HatParent hatParent, out HatViewData asset)
    {
        if (hatParent && hatParent.Hat) return hatParent.Hat.TryGetCached(out asset);
        asset = null;
        return false;
    }

    internal static bool TryGetCached(this HatData hat, out HatViewData asset)
    {
        return ViewDataCache.TryGetValue(hat.name, out asset);
    }

    internal static bool IsCached(this HatData hat) => ViewDataCache.ContainsKey(hat.name);
    internal static bool IsCached(this HatParent hatParent) => hatParent.Hat != null && hatParent.Hat.IsCached();

    internal static HatData CreateHatBehaviour(CustomHatConfig config, bool testOnly = false)
    {
        var mainImage = CreateHatSprite(config.Resource, config.AutoScale);
        if (mainImage == null)
            throw new FileNotFoundException("File not downloaded yet");

        var backImage = config.BackResource == null ? null : CreateHatSprite(config.BackResource, config.AutoScale);
        // a missing or undecodable back image changes the render topology, reject before building
        if (config.BackResource != null && backImage == null)
            throw new FileNotFoundException("Back image not usable");

        var viewData = ScriptableObject.CreateInstance<HatViewData>();
        viewData.MainImage = mainImage;
        viewData.FloorImage = mainImage;
        if (backImage != null)
        {
            viewData.BackImage = backImage;
            config.Behind = true;
        }
        var hat = ScriptableObject.CreateInstance<HatData>();

        if (config.ClimbResource != null)
        {
            viewData.ClimbImage = CreateHatSprite(config.ClimbResource, config.AutoScale);
            viewData.LeftClimbImage = viewData.ClimbImage;
        }

        hat.name = config.Name;
        hat.displayOrder = 0;
        hat.ProductId = "cmh_" + config.Name.Replace(' ', '_');
        hat.InFront = !config.Behind;
        hat.NoBounce = !config.Bounce;
        hat.ChipOffset = new Vector2(0f, 0.2f);
        hat.Free = true;
        viewData.MatchPlayerColor = config.Adaptive;

        var extension = new HatExtension
        {
            Author = config.Author ?? "Unknown",
            Package = config.Package ?? "Misc.",
            Condition = config.Condition ?? "none",
            Adaptive = config.Adaptive
        };
        AuthorCache[hat.name] = extension.Author;

        if (config.FlipResource != null)
            extension.FlipImage = CreateHatSprite(config.FlipResource, config.AutoScale);
        if (config.BackFlipResource != null)
            extension.BackFlipImage = CreateHatSprite(config.BackFlipResource, config.AutoScale);

        if (testOnly)
        {
            TestExtension = extension;
            TestExtension.Condition = hat.name;
        }
        else
        {
            ExtensionCache[hat.name] = extension;
        }

        hat.ViewDataRef = new AssetReference(viewData.Pointer);
        hat.CreateAddressableAsset();
        // cache last: a throw above must not leave a half-built entry behind
        ViewDataCache[config.Name] = viewData;
        return hat;
    }

    // tutorial test hat: loads PNGs from Cosmetics/CustomHats/Test
    private static Sprite CreateHatSprite(string path, bool autoScale = true)
    {
        var texture = Core.CosmeticsLoader.LoadTex(
            Path.Combine(CosmeticsManager.CustomHatsDir, path))
            ?? Helpers.LoadTextureFromResources(path);
        if (texture == null) return null;
        // default: 300px base ppu; off: raw pixels (ppu 100)
        return Core.CosmeticsLoader.MakeSprite(texture,
            new Vector2(0.53f, 0.575f),
            autoScale ? texture.width * 0.375f : 100f);
    }

    public static List<CustomHatConfig> CreateHatDetailsFromFileNames(string[] fileNames, bool fromDisk = false)
    {
        var fronts = new Dictionary<string, CustomHatConfig>();
        var backs = new Dictionary<string, string>();
        var flips = new Dictionary<string, string>();
        var backFlips = new Dictionary<string, string>();
        var climbs = new Dictionary<string, string>();

        foreach (var fileName in fileNames)
        {
            var dirEnd = fileName.LastIndexOf("\\", StringComparison.InvariantCulture) + 1;
            var baseName = fromDisk ? fileName[dirEnd..].Split('.')[0] : fileName.Split('.')[3];
            var parts = baseName.Split('_');
            var options = new HashSet<string>(parts);
            if (options.Contains("back") && options.Contains("flip"))
                backFlips[parts[0]] = fileName;
            else if (options.Contains("climb"))
                climbs[parts[0]] = fileName;
            else if (options.Contains("back"))
                backs[parts[0]] = fileName;
            else if (options.Contains("flip"))
                flips[parts[0]] = fileName;
            else
                fronts[parts[0]] = new CustomHatConfig
                {
                    Resource = fileName,
                    Name = parts[0].Replace('-', ' '),
                    Bounce = options.Contains("bounce"),
                    Adaptive = options.Contains("adaptive"),
                    Behind = options.Contains("behind")
                };
        }

        var hats = new List<CustomHatConfig>();
        foreach (var frontEntry in fronts)
        {
            var hatName = frontEntry.Key;
            var hat = frontEntry.Value;
            backs.TryGetValue(hatName, out var backResource);
            climbs.TryGetValue(hatName, out var climbResource);
            flips.TryGetValue(hatName, out var flipResource);
            backFlips.TryGetValue(hatName, out var backFlipResource);
            if (backResource != null) hat.BackResource = backResource;
            if (climbResource != null) hat.ClimbResource = climbResource;
            if (flipResource != null) hat.FlipResource = flipResource;
            if (backFlipResource != null) hat.BackFlipResource = backFlipResource;
            if (hat.BackResource != null) hat.Behind = true;
            hats.Add(hat);
        }
        return hats;
    }

    private static bool Exists(string res) =>
        res == null || File.Exists(Path.Combine(CosmeticsManager.CustomHatsDir, res));

    // first missing resource blocking the ready gate, null when ready
    internal static string MissingGateField(CustomHatConfig config)
    {
        if (config.Resource == null || !Exists(config.Resource)) return "main";
        return Exists(config.BackResource) ? null : "back";
    }

    private static bool IsReady(CustomHatConfig config) => MissingGateField(config) == null;

    private static bool References(CustomHatConfig config, string file) =>
        config.Resource == file || config.BackResource == file || config.ClimbResource == file
        || config.FlipResource == file || config.BackFlipResource == file;

    // run after config load and again before the tab opens
    internal static void PromoteAll()
    {
        if (UnregisteredHats.Count == 0) return;
        foreach (var config in UnregisteredHats)
        {
            if (ReadyHats.Contains(config) || !IsReady(config)) continue;
            ReadyHats.Add(config);
        }
    }

    // one downloaded file: only re-check entries referencing it
    internal static void PromoteFor(string file)
    {
        if (UnregisteredHats.Count == 0) return;
        foreach (var config in UnregisteredHats)
        {
            if (ReadyHats.Contains(config) || !References(config, file)) continue;
            if (IsReady(config)) ReadyHats.Add(config);
        }
    }

    internal const int InjectPerFrame = 8;

    internal static int InjectReady(HatManager hatManager, int limit)
    {
        if (hatManager == null || hatManager.allHats == null || ReadyHats.Count == 0 || limit <= 0) return 0;
        try
        {
            var hats = hatManager.allHats.ToList();

            // take the batch in config order: ReadyHats grows in download order, which would
            // shuffle entries inside a priority group
            var batch = new List<CustomHatConfig>();
            foreach (var config in UnregisteredHats)
            {
                if (!ReadyHats.Contains(config)) continue;
                batch.Add(config);
                if (batch.Count >= limit) break;
            }

            var injected = 0;
            foreach (var config in batch)
            {
                try
                {
                    hats.Add(CreateHatBehaviour(config));
                    ReadyHats.Remove(config);
                    UnregisteredHats.Remove(config);
                    injected++;
                }
                catch (Exception ex)
                {
                    // undecodable resource, retrying would fail the same way
                    ReadyHats.Remove(config);
                    UnregisteredHats.Remove(config);
                    Warn($"dropped {config.Name}: {ex.Message}", "hats");
                }
            }

            if (injected > 0) hatManager.allHats = hats.OrderBy(PriorityFor).ToArray();
            return batch.Count;
        }
        catch (Exception ex)
        {
            // must never throw: a prefix exception skips the postfix that resets _isRunning
            Warn($"inject failed: {ex.Message}", "hats");
            return 0;
        }
    }

    private static int PriorityFor(HatData hat)
    {
        var extension = hat.GetHatExtension();
        var package = extension?.Package ?? "Innersloth";
        if (package == InnerslothPackageName) return 100;
        return PackagePriorities.TryGetValue(package, out var priority) ? priority : 50;
    }

    internal static List<string> GenerateDownloadList(List<CustomHatConfig> hats)
    {
        var toDownload = new List<string>();
        foreach (var hat in hats)
        {
            var files = new List<(string, string)>
            {
                (hat.Resource, hat.ResHashA),
                (hat.BackResource, hat.ResHashB),
                (hat.ClimbResource, hat.ResHashC),
                (hat.FlipResource, hat.ResHashF),
                (hat.BackFlipResource, hat.ResHashBf)
            };
            foreach (var (fileName, fileHash) in files)
            {
                if (fileName != null && Core.CosmeticsLoader.NeedDownload(fileName, fileHash, CosmeticsManager.CustomHatsDir))
                    toDownload.Add(fileName);
            }
        }
        return toDownload;
    }
}
