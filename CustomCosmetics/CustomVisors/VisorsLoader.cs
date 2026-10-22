using CustomCosmetics.Core;
using System.Collections.Generic;
using System.Text.Json;

namespace CustomCosmetics.CustomVisors;

public class VisorsLoader : CosmeticsLoader
{
    protected override string Kind => "visors";
    protected override string ConfigFile => "CustomVisors.json";
    protected override string ResDir => "visors";
    protected override string LocalDir => CosmeticsManager.CustomVisorsDir;
    protected override string ConfigFor(RepositorySource src) => src.VisorsFile ?? ConfigFile;
    protected override string ResDirFor(RepositorySource src) => src.VisorsDir ?? ResDir;

    protected override void OnConfig(string json)
    {
        var config = JsonSerializer.Deserialize<VisorsConfigFile>(json, new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true
        });
        if (config?.Visors == null) return;

        RegisterPackages(config.Packages, CustomVisorManager.PackageDisplayNames, CustomVisorManager.PackagePriorities);
        PrefixResources(config);
        var dropped = config.Visors.RemoveAll(visor => visor.Resource == null);
        if (dropped > 0) Warn($"[{Alias}] {dropped} entries without a valid .png resource skipped", Kind);
        CustomVisorManager.UnregisteredVisors.AddRange(config.Visors);
        Message($"[{Alias}] {config.Visors.Count} visors loaded, {MissingFiles().Count} resources to sync", Kind);
        CustomVisorManager.PromoteAll();
    }

    protected override void OnFileReady(string file) => CustomVisorManager.PromoteFor(file);

    // HostNotReady makes the drain loop retry on the next frame
    protected override int InjectBatch()
    {
        if (CustomVisorManager.ReadyVisors.Count == 0) return 0;
        var hatManager = DestroyableSingleton<HatManager>.Instance;
        return hatManager == null || hatManager.allVisors == null
            ? HostNotReady
            : CustomVisorManager.InjectReady(hatManager, CustomVisorManager.InjectPerFrame);
    }

    protected override void OnDownloadComplete()
    {
        var blocked = new List<string>();
        foreach (var visor in CustomVisorManager.UnregisteredVisors)
        {
            var gate = CustomVisorManager.MissingGateField(visor);
            if (gate != null) blocked.Add($"{visor.Name}({gate})");
        }
        ReportNotReady(blocked);
    }

    protected override List<string> MissingFiles()
    {
        var files = new List<string>();
        foreach (var visor in CustomVisorManager.UnregisteredVisors)
        {
            if (visor.Resource != null && NeedDownload(visor.Resource, visor.ResHashA, LocalDir))
                files.Add(visor.Resource);
            if (visor.FlipResource != null && NeedDownload(visor.FlipResource, visor.ResHashF, LocalDir))
                files.Add(visor.FlipResource);
        }
        return files;
    }

    private void PrefixResources(VisorsConfigFile config)
    {
        foreach (var visor in config.Visors)
        {
            visor.Resource = PrefixResource(visor.Resource);
            visor.FlipResource = PrefixResource(visor.FlipResource);
        }
    }
}
