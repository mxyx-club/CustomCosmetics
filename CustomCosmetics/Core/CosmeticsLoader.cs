using BepInEx.Unity.IL2CPP.Utils;
using Il2CppInterop.Runtime.Attributes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Networking;

namespace CustomCosmetics.Core;

public class CosmeticsLoader : MonoBehaviour
{
    // download pipeline: config fetch -> MD5 diff -> PNG fetch
    private bool _busy;
    private bool _draining;
    private bool _ok = true;
    private static MD5 _md5;
    private List<RepositorySource> _sources = new();

    // per-kind contract
    [HideFromIl2Cpp] protected virtual string Kind => "cosmetic";
    [HideFromIl2Cpp] protected virtual string ConfigFile => "Cosmetics.json";
    [HideFromIl2Cpp] protected virtual string ResDir => "";
    [HideFromIl2Cpp] protected virtual string LocalDir => "";
    [HideFromIl2Cpp] protected virtual void OnConfig(string json) { }
    [HideFromIl2Cpp] protected virtual void OnFileReady(string file) { }
    [HideFromIl2Cpp] protected virtual void OnDownloadComplete() { }
    [HideFromIl2Cpp] protected virtual List<string> MissingFiles() { return new List<string>(); }
    // >0 records injected, 0 queue empty, HostNotReady host not ready
    [HideFromIl2Cpp] protected virtual int InjectBatch() => 0;
    protected const int HostNotReady = -1;
    [HideFromIl2Cpp] protected virtual string ConfigFor(RepositorySource src) => ConfigFile;
    [HideFromIl2Cpp] protected virtual string ResDirFor(RepositorySource src) => ResDir;

    [HideFromIl2Cpp]
    protected string Alias { get; private set; }

    [HideFromIl2Cpp]
    protected string PrefixResource(string resource)
    {
        if (resource == null) return null;
        var clean = CleanName(resource);
        return clean == null ? null : $"{Alias}/{clean}";
    }

    [HideFromIl2Cpp]
    protected static void RegisterPackages(List<PackageConfig> packages,
        Dictionary<string, string> displayNames, Dictionary<string, int> priorities)
    {
        if (packages == null) return;
        foreach (var package in packages)
        {
            if (string.IsNullOrWhiteSpace(package.Package)) continue;
            if (!string.IsNullOrWhiteSpace(package.DisplayName)) displayNames[package.Package] = package.DisplayName;
            priorities[package.Package] = package.Priority;
        }
    }

    [HideFromIl2Cpp]
    protected string ConfigPath(string alias) => Path.Combine(LocalDir, $"{Path.GetFileNameWithoutExtension(ConfigFile)}_{alias}.json");

    [HideFromIl2Cpp]
    protected void ReportNotReady(List<string> entries)
    {
        if (entries.Count == 0) return;
        var more = entries.Count > 10 ? $" (+{entries.Count - 10} more)" : "";
        Warn($"{entries.Count} not ready: {string.Join(", ", entries.Take(10))}{more}", Kind);
    }

    [HideFromIl2Cpp]
    public void Fetch(List<RepositorySource> sources)
    {
        if (_busy) return;
        _sources = sources ?? new List<RepositorySource>();
        if (_sources.Count == 0) { Warn("no repositories configured", Kind); return; }
        this.StartCoroutine(CoFetch());
    }

    // decode one batch per frame; a single frame with too many textures crashes on mobile
    [HideFromIl2Cpp]
    protected void RequestInject()
    {
        if (_draining) return;
        _draining = true;
        this.StartCoroutine(CoInject());
    }

    [HideFromIl2Cpp]
    private IEnumerator CoInject()
    {
        var retries = 0;
        try
        {
            while (true)
            {
                var injected = InjectBatch();
                if (injected > 0)
                {
                    retries = 0;
                    yield return new WaitForEndOfFrame();
                    continue;
                }
                if (injected == 0) break;
                // host not ready: give up after ~30s, the next download re-requests
                if (++retries > 60) break;
                yield return new WaitForSecondsRealtime(0.5f);
            }
        }
        finally { _draining = false; }
    }

    [HideFromIl2Cpp]
    private IEnumerator CoFetch()
    {
        _busy = true;

        foreach (var src in _sources)
        {
            _ok = true;
            Alias = src.Alias;
            Message($"[{src.Alias}] fetching config from {src.Url}", Kind);
            yield return CoFetchConfig(src);
        }
        _busy = false;
    }

    [HideFromIl2Cpp]
    private IEnumerator CoFetchConfig(RepositorySource src)
    {
        Alias = src.Alias;
        var www = new UnityWebRequest
        {
            method = UnityWebRequest.kHttpVerbGET,
            downloadHandler = new DownloadHandlerBuffer(),
            url = $"{src.Url}/{ConfigFor(src)}"
        };
        var op = www.SendWebRequest();
        while (!op.isDone) yield return new WaitForEndOfFrame();

        if (www.isNetworkError || www.isHttpError)
        {
            Error($"[{src.Alias}] config fetch failed: {www.error}", Kind);
            _ok = false;
            goto done;
        }

        var shouldDownload = false;
        try
        {
            if (!Directory.Exists(LocalDir)) Directory.CreateDirectory(LocalDir);
            var path = ConfigPath(src.Alias);
            File.WriteAllText(path, www.downloadHandler.text);
            OnConfig(File.ReadAllText(path));
            RequestInject();
            shouldDownload = _ok;
        }
        catch (Exception ex) { _ok = false; Warn($"[{src.Alias}] config parse failed: {ex.Message}", Kind); }
        if (shouldDownload) { var list = MissingFiles(); if (list.Count > 0) yield return CoDownload(list); }

    done:
        www.downloadHandler.Dispose();
        www.Dispose();
    }

    [HideFromIl2Cpp]
    private IEnumerator CoDownload(List<string> files)
    {
        for (var i = 0; i < files.Count; i++)
            yield return CoDownloadOne(files[i], i + 1, files.Count);

        Message($"download finished, {files.Count} files", Kind);
        OnDownloadComplete();
    }

    [HideFromIl2Cpp]
    private IEnumerator CoDownloadOne(string relativePath, int index, int total)
    {
        var parts = relativePath.Split('/');
        var alias = parts.Length > 1 ? parts[0] : Alias;
        var name = parts.Length > 1 ? parts[^1] : relativePath;
        var src = _sources.Find(s => s.Alias == alias) ?? _sources[0];

        Message($"[{alias}] downloading {name} ({index}/{total})", Kind);

        var www = new UnityWebRequest();
        www.SetMethod(UnityWebRequest.UnityWebRequestMethod.Get);
        var resDir = ResDirFor(src);
        var url = string.IsNullOrEmpty(resDir)
            ? $"{src.Url}/{Uri.EscapeDataString(name)}"
            : $"{src.Url}/{resDir}/{Uri.EscapeDataString(name)}";
        www.SetUrl(url);
        www.downloadHandler = new DownloadHandlerBuffer();
        var op = www.SendWebRequest();
        while (!op.isDone) yield return new WaitForEndOfFrame();

        if (!www.isNetworkError && !www.isHttpError)
        {
            var path = Path.Combine(LocalDir, alias, name);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var task = File.WriteAllBytesAsync(path, www.downloadHandler.GetNativeData().ToArray());
            while (!task.IsCompleted)
                yield return new WaitForEndOfFrame();
            if (task.IsFaulted)
                Error($"[{alias}] write failed: {task.Exception?.Message}", Kind);
            else
            {
                OnFileReady(relativePath);
                RequestInject();
            }
        }
        else
        {
            Warn($"[{alias}] download failed: {www.error}", Kind);
        }

        www.downloadHandler.Dispose();
        www.Dispose();
    }

    // shared helpers
    public static bool NeedDownload(string file, string hash, string dir)
    {
        var path = Path.Combine(dir, file);
        if (hash == null || !File.Exists(path)) return true;
        _md5 ??= MD5.Create();
        using var s = File.OpenRead(path);
        return !hash.Equals(BitConverter.ToString(_md5.ComputeHash(s)).Replace("-", "").ToLowerInvariant());
    }

    public static string CleanName(string path)
    {
        if (path == null || !path.EndsWith(".png")) return null;
        // strip separators only; ".." inside a filename is legal (e.g. "a....png")
        return path.Replace("\\", "").Replace("/", "").Replace("*", "");
    }

    public static Texture2D LoadTex(string path)
    {
        try
        {
            // guard before LoadImage: truncated data crashes the native decoder
            if (File.Exists(path) && IsValidImage(path))
            {
                var t = new Texture2D(2, 2, TextureFormat.ARGB32, true);
                if (ImageConversion.LoadImage(t, File.ReadAllBytes(path), false) && t.width > 0 && t.height > 0)
                    return t;
            }
        }
        catch { }
        return null;
    }


    // integrity: PNG needs IEND, JPEG needs EOI; other formats rejected
    private static bool IsValidImage(string path)
    {
        try
        {
            var b = File.ReadAllBytes(path);
            if (b.Length < 200) return false;
            if (b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47)
                return b[^12] == 0 && b[^11] == 0 && b[^10] == 0 && b[^9] == 0 &&
                       b[^8] == 0x49 && b[^7] == 0x45 && b[^6] == 0x4E && b[^5] == 0x44;
            if (b[0] == 0xFF && b[1] == 0xD8)
                return b[^2] == 0xFF && b[^1] == 0xD9;
            return false;
        }
        catch { return false; }
    }

    public static Sprite MakeSprite(Texture2D tex, Vector2 pivot, float ppu)
    {
        if (tex == null || tex.width <= 0 || tex.height <= 0) return null;
        var s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, ppu);
        if (s == null) return null;
        tex.hideFlags |= HideFlags.HideAndDontSave | HideFlags.DontUnloadUnusedAsset;
        s.hideFlags |= HideFlags.HideAndDontSave | HideFlags.DontUnloadUnusedAsset;
        return s;
    }
}
