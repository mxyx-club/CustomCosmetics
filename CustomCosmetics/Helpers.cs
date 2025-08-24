using System;
using System.IO;
using System.Reflection;
using AmongUs.Data;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace CustomCosmetics;

internal static class Helpers
{
    public static bool IsCN()
    {
        return (int)DataManager.Settings.Language.CurrentLanguage == 13;
    }

    public static string GithubUrl(this string url)
    {
        if (IsCN() && (url.Contains("github.com") || url.Contains("githubusercontent.com")) && !url.Contains("ghfast.top"))
        {
            return "https://ghfast.top/" + url;
        }
        return url;
    }

    public static unsafe Texture2D loadTextureFromResources(string path)
    {
        try
        {
            var texture = new Texture2D(2, 2, TextureFormat.ARGB32, true);
            var assembly = Assembly.GetExecutingAssembly();
            var stream = assembly.GetManifestResourceStream(path);
            var length = stream!.Length;
            var byteTexture = new Il2CppStructArray<byte>(length);
            _ = stream.Read(new Span<byte>(IntPtr.Add(byteTexture.Pointer, IntPtr.Size * 4).ToPointer(), (int)length));
            ImageConversion.LoadImage(texture, byteTexture, false);
            return texture;
        }
        catch
        {
            //Error("loading texture from resources: " + path);
        }

        return null;
    }

    public static Texture2D loadTextureFromDisk(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var texture = new Texture2D(2, 2, TextureFormat.ARGB32, true);
                var byteTexture = File.ReadAllBytes(path);
                ImageConversion.LoadImage(texture, byteTexture, false);
                return texture;
            }
        }
        catch
        {
            Error("Error loading texture from disk: " + path);
        }

        return null;
    }
}