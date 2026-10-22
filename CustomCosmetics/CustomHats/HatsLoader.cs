using CustomCosmetics.Core;
using System.Collections.Generic;
using System.Text.Json;

namespace CustomCosmetics.CustomHats;

public class HatsLoader : CosmeticsLoader
{
    protected override string Kind => "hats";
    protected override string ConfigFile => "CustomHats.json";
    protected override string ResDir => "hats";
    protected override string LocalDir => CosmeticsManager.CustomHatsDir;
    protected override string ConfigFor(RepositorySource src) => src.HatsFile ?? ConfigFile;
    protected override string ResDirFor(RepositorySource src) => src.HatsDir ?? ResDir;

    protected override void OnConfig(string json, bool local)
    {
        var config = JsonSerializer.Deserialize<HatsConfigFile>(json, new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true
        });
        if (config?.Hats == null) return;

        RegisterPackages(config.Packages, CustomHatManager.PackageDisplayNames, CustomHatManager.PackagePriorities);
        PrefixResources(config);
        var dropped = config.Hats.RemoveAll(hat => hat.Resource == null);
        if (dropped > 0) Warn($"[{Alias}] {dropped} entries without a valid .png resource skipped", Kind);
        CustomHatManager.UnregisteredHats.AddRange(config.Hats);
        Message($"[{Alias}] {config.Hats.Count} hats loaded, {MissingFiles().Count} resources to sync", Kind);
        CustomHatManager.PromoteAll();
    }

    protected override List<string> MissingFiles() =>
        CustomHatManager.GenerateDownloadList(CustomHatManager.UnregisteredHats);

    protected override void OnFileReady(string file) => CustomHatManager.PromoteFor(file);

    // HostNotReady makes the drain loop retry on the next frame
    protected override int InjectBatch()
    {
        if (CustomHatManager.ReadyHats.Count == 0) return 0;
        var hatManager = DestroyableSingleton<HatManager>.Instance;
        return hatManager == null || hatManager.allHats == null
            ? HostNotReady
            : CustomHatManager.InjectReady(hatManager, CustomHatManager.InjectPerFrame);
    }

    protected override void OnDownloadComplete()
    {
        var blocked = new List<string>();
        foreach (var hat in CustomHatManager.UnregisteredHats)
        {
            var gate = CustomHatManager.MissingGateField(hat);
            if (gate != null) blocked.Add($"{hat.Name}({gate})");
        }
        ReportNotReady(blocked);
    }

    private void PrefixResources(HatsConfigFile config)
    {
        foreach (var hat in config.Hats)
        {
            hat.Resource = PrefixResource(hat.Resource);
            hat.BackResource = PrefixResource(hat.BackResource);
            hat.ClimbResource = PrefixResource(hat.ClimbResource);
            hat.FlipResource = PrefixResource(hat.FlipResource);
            hat.BackFlipResource = PrefixResource(hat.BackFlipResource);
        }
    }
}
