using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace CustomCosmetics.CustomVisors;

public static class CustomVisorManager
{
    internal static List<CustomVisorConfig> UnregisteredVisors = new();
    // entries whose resources are all on disk; hot paths only touch this list
    internal static readonly List<CustomVisorConfig> ReadyVisors = new();
    internal static readonly Dictionary<string, VisorViewData> ViewDataCache = new();
    // cache key: visor.ProductId (cmv_*)
    internal static readonly Dictionary<string, bool> BehindHatsCache = new();
    internal static readonly Dictionary<string, string> AuthorCache = new();
    internal static readonly Dictionary<string, string> PackageCache = new();
    internal static readonly Dictionary<string, string> PackageDisplayNames = new();
    internal static readonly Dictionary<string, int> PackagePriorities = new();

    internal static VisorData CreateVisorBehaviour(CustomVisorConfig config)
    {
        var spr = CreateVisorSprite(config.Resource, config.AutoScale) ?? throw new FileNotFoundException("Visor file not downloaded yet");
        var viewData = ScriptableObject.CreateInstance<VisorViewData>();
        viewData.IdleFrame = spr;

        if (config.FlipResource != null)
            viewData.LeftIdleFrame = CreateVisorSprite(config.FlipResource, config.AutoScale);
        viewData.MatchPlayerColor = config.Adaptive;

        var visor = ScriptableObject.CreateInstance<VisorData>();
        visor.name = config.Name;
        visor.displayOrder = 0;
        visor.ProductId = $"cmv_{config.Name.Replace(' ', '_')}";
        visor.BundleId = visor.ProductId;
        visor.Free = true;
        visor.ChipOffset = new Vector2(0f, 0.2f);
        visor.ViewDataRef = new AssetReference(viewData.Pointer);
        visor.CreateAddressableAsset();
        BehindHatsCache[visor.ProductId] = config.BehindHats;
        AuthorCache[visor.name] = config.Author ?? "Unknown";
        PackageCache[visor.name] = config.Package ?? "Misc.";
        // cache last: a throw above must not leave a half-built entry behind
        ViewDataCache[config.Name] = viewData;
        return visor;
    }

    private static Sprite CreateVisorSprite(string path, bool autoScale)
    {
        var texture = Core.CosmeticsLoader.LoadTex(
            Path.Combine(CosmeticsManager.CustomVisorsDir, path));
        if (texture == null) return null;
        // autoScale: 300px base ppu; off: raw pixels (ppu 100)
        return Core.CosmeticsLoader.MakeSprite(texture,
            new Vector2(0.5f, 0.5f),
            autoScale ? texture.width * 0.375f : 100f);
    }

    private static bool Exists(string res) =>
        res == null || File.Exists(Path.Combine(CosmeticsManager.CustomVisorsDir, res));

    // first missing resource blocking the ready gate, null when ready
    internal static string MissingGateField(CustomVisorConfig config) =>
        config.Resource != null && Exists(config.Resource) ? null : "main";

    private static bool IsReady(CustomVisorConfig config) => MissingGateField(config) == null;

    private static bool References(CustomVisorConfig config, string file) =>
        config.Resource == file || config.FlipResource == file;

    // run after config load and again before the tab opens
    internal static void PromoteAll()
    {
        if (UnregisteredVisors.Count == 0) return;
        foreach (var config in UnregisteredVisors)
        {
            if (ReadyVisors.Contains(config) || !IsReady(config)) continue;
            ReadyVisors.Add(config);
        }
    }

    // one downloaded file: only re-check entries referencing it
    internal static void PromoteFor(string file)
    {
        if (UnregisteredVisors.Count == 0) return;
        foreach (var config in UnregisteredVisors)
        {
            if (ReadyVisors.Contains(config) || !References(config, file)) continue;
            if (IsReady(config)) ReadyVisors.Add(config);
        }
    }

    internal const int InjectPerFrame = 8;

    internal static int InjectReady(HatManager hatManager, int limit)
    {
        if (hatManager == null || hatManager.allVisors == null || ReadyVisors.Count == 0 || limit <= 0) return 0;
        try
        {
            var visors = hatManager.allVisors.ToList();

            // take the batch in config order: ReadyVisors grows in download order, which would
            // shuffle entries inside a priority group
            var batch = new List<CustomVisorConfig>();
            foreach (var config in UnregisteredVisors)
            {
                if (!ReadyVisors.Contains(config)) continue;
                batch.Add(config);
                if (batch.Count >= limit) break;
            }

            var injected = 0;
            foreach (var config in batch)
            {
                try
                {
                    visors.Add(CreateVisorBehaviour(config));
                    ReadyVisors.Remove(config);
                    UnregisteredVisors.Remove(config);
                    injected++;
                }
                catch (Exception ex)
                {
                    // undecodable resource, retrying would fail the same way
                    ReadyVisors.Remove(config);
                    UnregisteredVisors.Remove(config);
                    Warn($"dropped {config.Name}: {ex.Message}", "visors");
                }
            }

            if (injected > 0) hatManager.allVisors = visors.ToArray();
            return batch.Count;
        }
        catch (Exception ex)
        {
            // must never throw: a prefix exception skips the postfix that resets _isRunning
            Warn($"inject failed: {ex.Message}", "visors");
            return 0;
        }
    }
}
