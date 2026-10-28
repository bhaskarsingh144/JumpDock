using System;
using System.Collections.Generic;
using System.Drawing;

/// <summary>
/// Shared icon bitmaps keyed by path + size. Callers must not Dispose returned images
/// unless they used <see cref="GetOwned"/>.
/// </summary>
internal static class IconCache
{
    private const int MaxEntries = 128;
    private static readonly Dictionary<string, Bitmap> Map = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);
    private static readonly LinkedList<string> Lru = new LinkedList<string>();

    public static Bitmap Get(string name, string iconPath, string lnk, int size)
    {
        if (size < 8) size = 8;
        string key = MakeKey(iconPath, lnk, size);
        lock (Map)
        {
            Bitmap hit;
            if (Map.TryGetValue(key, out hit) && hit != null)
            {
                Touch(key);
                return hit;
            }
        }

        Bitmap loaded = null;
        try
        {
            loaded = IconLoader.Load(name, iconPath, lnk, size);
        }
        catch
        {
            loaded = null;
        }
        if (loaded == null)
            return null;

        lock (Map)
        {
            Bitmap existing;
            if (Map.TryGetValue(key, out existing) && existing != null)
            {
                loaded.Dispose();
                Touch(key);
                return existing;
            }
            EvictIfNeeded();
            Map[key] = loaded;
            Lru.AddFirst(key);
            return loaded;
        }
    }

    /// <summary>Clone for controls that Dispose their image (e.g. IconTile).</summary>
    public static Bitmap GetOwned(string name, string iconPath, string lnk, int size)
    {
        Bitmap shared = Get(name, iconPath, lnk, size);
        if (shared == null)
            return null;
        try
        {
            return new Bitmap(shared);
        }
        catch
        {
            return null;
        }
    }

    public static void Clear()
    {
        lock (Map)
        {
            foreach (KeyValuePair<string, Bitmap> kv in Map)
            {
                if (kv.Value != null)
                {
                    try { kv.Value.Dispose(); }
                    catch { }
                }
            }
            Map.Clear();
            Lru.Clear();
        }
    }

    private static void Touch(string key)
    {
        LinkedListNode<string> node = Lru.Find(key);
        if (node != null)
        {
            Lru.Remove(node);
            Lru.AddFirst(node);
        }
    }

    private static void EvictIfNeeded()
    {
        while (Map.Count >= MaxEntries && Lru.Count > 0)
        {
            string old = Lru.Last.Value;
            Lru.RemoveLast();
            Bitmap bmp;
            if (Map.TryGetValue(old, out bmp))
            {
                Map.Remove(old);
                if (bmp != null)
                {
                    try { bmp.Dispose(); }
                    catch { }
                }
            }
        }
    }

    private static string MakeKey(string iconPath, string lnk, int size)
    {
        string a = iconPath ?? "";
        string b = lnk ?? "";
        if (b.IndexOf('\n') >= 0)
            b = b.Split('\n')[0];
        return size.ToString() + "|" + a + "|" + b;
    }
}
