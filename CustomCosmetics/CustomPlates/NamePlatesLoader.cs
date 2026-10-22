using CustomCosmetics.Core;
using System.Collections.Generic;
using System.Text.Json;

namespace CustomCosmetics.CustomPlates;

public class NamePlatesLoader : CosmeticsLoader
{
    protected override string Kind => "nameplates";
    protected override string ConfigFile => "CustomNamePlates.json";
    protected override string ResDir => "nameplates";
    protected override string LocalDir => CosmeticsManager.CustomPlatesDir;
    protected override string ConfigFor(RepositorySource src) => src.PlatesFile ?? ConfigFile;
    protected override string ResDirFor(RepositorySource src) => src.PlatesDir ?? ResDir;

    protected override void OnConfig(string json)
    {
        var r = JsonSerializer.Deserialize<NamePlatesConfigFile>(json, new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true
        });
        if (r == null || r.NamePlates == null) return;

        RegisterPackages(r.Packages, CustomNamePlateManager.PackageDisplayNames, CustomNamePlateManager.PackagePriorities);

        foreach (var p in r.NamePlates)
            p.Resource = PrefixResource(p.Resource);
        CustomNamePlateManager.UnregisteredPlates.AddRange(r.NamePlates);
        Message($"[{Alias}] {r.NamePlates.Count} nameplates loaded, {MissingFiles().Count} resources to sync", Kind);
    }

    protected override List<string> MissingFiles()
    {
        var list = new List<string>();
        foreach (var p in CustomNamePlateManager.UnregisteredPlates)
            if (p.Resource != null && NeedDownload(p.Resource, p.ResHashA, LocalDir)) list.Add(p.Resource);
        return list;
    }
}
