using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace CustomCosmetics.Core;

internal static class Helpers
{
    public static TextMeshPro GetTitleTemplate(Component tab, ref TextMeshPro cache)
    {
        if (cache != null) return cache;
        var g = GameObject.Find("HatsGroup");
        if (g != null) cache = g.transform.FindChild("Text")?.GetComponent<TextMeshPro>();
        if (cache == null)
        {
            var all = tab.GetComponentsInChildren<TextMeshPro>(true);
            foreach (var t in all)
                if (t != null && t.gameObject.activeInHierarchy) { cache = t; break; }
        }
        if (cache == null) cache = UObject.FindObjectOfType<TextMeshPro>();
        return cache;
    }

    public static IEnumerable<string> OrderPackageKeys(IEnumerable<string> keys, string innerslothName,
        Dictionary<string, int> priorities)
    {
        return keys.OrderBy(x =>
        {
            if (x == innerslothName) return 100;
            return priorities.TryGetValue(x, out var pr) ? pr : 50;
        });
    }

    public static void SetCustomItemName(string productId, string name, string prefix,
        Dictionary<string, string> authorCache)
    {
        if (!productId.StartsWith(prefix)) return;
        var menu = PlayerCustomizationMenu.Instance;
        if (menu == null) return;
        var author = authorCache.TryGetValue(name, out var a) ? a : "Unknown";
        menu.itemName.text = $"{name}\nby {author}";
    }

    public static void DestroyChildren(Transform parent)
    {
        for (var i = 0; i < parent.childCount; i++)
            UObject.Destroy(parent.GetChild(i).gameObject);
    }
}
