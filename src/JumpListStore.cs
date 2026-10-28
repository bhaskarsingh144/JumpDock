// JumpDock.
using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

public class JumpItem
{
    public string kind { get; set; }
    public string title { get; set; }
    public string target { get; set; }
    public string lastUsed { get; set; }
}

public class JumpList
{
    public List<JumpItem> pinned { get; set; }
    public List<JumpItem> recent { get; set; }

    public JumpList()
    {
        pinned = new List<JumpItem>();
        recent = new List<JumpItem>();
    }
}

public class JumpFile
{
    public Dictionary<string, JumpList> lists { get; set; }

    public JumpFile()
    {
        lists = new Dictionary<string, JumpList>();
    }
}

public sealed class JumpListStore
{
    public const int RecentCap = 500;
    private readonly string _path;
    private readonly JavaScriptSerializer _ser = new JavaScriptSerializer();
    private JumpFile _file;

    public JumpListStore()
        : this(DefaultPath())
    {
    }

    /// <summary>Test / alternate store path. Empty harvest must not wipe persisted recents.</summary>
    public JumpListStore(string path)
    {
        _ser.MaxJsonLength = int.MaxValue;
        _path = string.IsNullOrEmpty(path) ? DefaultPath() : path;
        Load();
    }

    public static string DefaultPath()
    {
        return JumpPaths.JumpListsPath;
    }

    public JumpList For(string name)
    {
        if (string.IsNullOrEmpty(name))
            name = "";
        JumpList list;
        if (!_file.lists.TryGetValue(name, out list) || list == null)
        {
            list = new JumpList();
            _file.lists[name] = list;
        }
        if (list.pinned == null) list.pinned = new List<JumpItem>();
        if (list.recent == null) list.recent = new List<JumpItem>();
        return list;
    }

    public void Pin(string name, JumpItem item)
    {
        if (item == null || string.IsNullOrEmpty(item.target))
            return;
        var list = For(name);
        RemoveByTarget(list.pinned, item.target);
        RemoveByTarget(list.recent, item.target);
        item.lastUsed = DateTime.UtcNow.ToString("o");
        list.pinned.Insert(0, item);
        Save();
    }

    /// <summary>Add pin if missing; does not reorder existing pins.</summary>
    public void EnsurePinned(string name, JumpItem item)
    {
        if (item == null || string.IsNullOrEmpty(item.target))
            return;
        var list = For(name);
        if (FindByTarget(list.pinned, item.target) != null)
            return;
        RemoveByTarget(list.recent, item.target);
        if (item.lastUsed == null)
            item.lastUsed = DateTime.UtcNow.ToString("o");
        list.pinned.Add(Clone(item));
        Save();
    }

    /// <summary>
    /// Drop auto pins (in autoTargets) and broken user pins; keep valid user pins.
    /// </summary>
    public void PrunePins(string name, HashSet<string> autoTargets)
    {
        var list = For(name);
        if (list.pinned == null || list.pinned.Count == 0)
            return;
        var next = new List<JumpItem>();
        for (int i = 0; i < list.pinned.Count; i++)
        {
            var p = list.pinned[i];
            if (p == null || string.IsNullOrEmpty(p.target))
                continue;
            string key = p.target.Trim();
            if (key.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                key = ChromeAppUrl.Canonical(key) ?? key;
            if (autoTargets != null && autoTargets.Contains(key))
                continue;
            if (!JumpLaunch.TargetLooksOk(p))
                continue;
            next.Add(Clone(p));
        }
        list.pinned = next;
        Save();
    }

    public void Unpin(string name, string target)
    {
        var list = For(name);
        RemoveByTarget(list.pinned, target);
        Save();
    }

    public void RecordOpen(string name, JumpItem item)
    {
        if (item == null || string.IsNullOrEmpty(item.target))
            return;
        var list = For(name);
        item.lastUsed = DateTime.UtcNow.ToString("o");
        if (FindByTarget(list.pinned, item.target) != null)
        {
            var pin = FindByTarget(list.pinned, item.target);
            pin.lastUsed = item.lastUsed;
            Save();
            return;
        }
        RemoveByTarget(list.recent, item.target);
        list.recent.Insert(0, Clone(item));
        while (list.recent.Count > RecentCap)
            list.recent.RemoveAt(list.recent.Count - 1);
        Save();
    }

    public bool IsPinned(string name, string target)
    {
        return FindByTarget(For(name).pinned, target) != null;
    }

    public void ReplaceRecents(string name, List<JumpItem> items)
    {
        // Empty / failed harvest must not erase what Reload / Sync already stored.
        // Call ClearRecent when an intentional wipe is required.
        if (items == null || items.Count == 0)
            return;
        var list = For(name);
        var next = new List<JumpItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            if (item == null || string.IsNullOrEmpty(item.target))
                continue;
            if (FindByTarget(list.pinned, item.target) != null)
                continue;
            string key = item.target.Trim();
            if (!seen.Add(key))
                continue;
            if (item.lastUsed == null)
                item.lastUsed = DateTime.UtcNow.ToString("o");
            next.Add(Clone(item));
            if (next.Count >= 40)
                break;
        }
        // Still nothing usable after filtering — keep the previous recent list.
        if (next.Count == 0)
            return;
        list.recent = next;
        Save();
    }

    public void MergeRecent(string name, List<JumpItem> items)
    {
        if (items == null || items.Count == 0)
            return;
        var list = For(name);
        bool changed = false;
        for (int i = items.Count - 1; i >= 0; i--)
        {
            var item = items[i];
            if (item == null || string.IsNullOrEmpty(item.target))
                continue;
            if (FindByTarget(list.pinned, item.target) != null)
                continue;
            if (FindByTarget(list.recent, item.target) != null)
                continue;
            if (item.lastUsed == null)
                item.lastUsed = DateTime.UtcNow.ToString("o");
            list.recent.Insert(0, Clone(item));
            changed = true;
        }
        while (list.recent.Count > RecentCap)
            list.recent.RemoveAt(list.recent.Count - 1);
        if (changed)
            Save();
    }

    public void ClearRecent(string name)
    {
        var list = For(name);
        if (list.recent == null || list.recent.Count == 0)
            return;
        list.recent.Clear();
        Save();
    }

    public void TrimRecent(string name, int max)
    {
        if (max < 1)
            return;
        var list = For(name);
        if (list.recent == null || list.recent.Count <= max)
            return;
        list.recent.RemoveRange(max, list.recent.Count - max);
        Save();
    }

    private void Load()
    {
        _file = new JumpFile();
        try
        {
            if (!File.Exists(_path))
                return;
            var json = File.ReadAllText(_path);
            var parsed = _ser.Deserialize<JumpFile>(json);
            if (parsed != null && parsed.lists != null)
                _file = parsed;
        }
        catch
        {
            _file = new JumpFile();
        }
    }

    private void Save()
    {
        var dir = Path.GetDirectoryName(_path);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        var tmp = _path + ".tmp";
        var json = _ser.Serialize(_file);
        File.WriteAllText(tmp, json);
        if (File.Exists(_path))
            File.Replace(tmp, _path, null);
        else
            File.Move(tmp, _path);
    }

    private static JumpItem FindByTarget(List<JumpItem> items, string target)
    {
        if (items == null || string.IsNullOrEmpty(target))
            return null;
        foreach (var it in items)
        {
            if (it != null && SameTarget(it.target, target))
                return it;
        }
        return null;
    }

    private static void RemoveByTarget(List<JumpItem> items, string target)
    {
        if (items == null)
            return;
        for (int i = items.Count - 1; i >= 0; i--)
        {
            if (items[i] != null && SameTarget(items[i].target, target))
                items.RemoveAt(i);
        }
    }

    private static bool SameTarget(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            return false;
        if (a.StartsWith("http", StringComparison.OrdinalIgnoreCase) || b.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            string ca = ChromeAppUrl.Canonical(a.Trim());
            string cb = ChromeAppUrl.Canonical(b.Trim());
            return string.Equals(ca, cb, StringComparison.OrdinalIgnoreCase);
        }
        try
        {
            return string.Equals(Path.GetFullPath(a.Trim()), Path.GetFullPath(b.Trim()), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }

    private static JumpItem Clone(JumpItem src)
    {
        return new JumpItem
        {
            kind = src.kind,
            title = src.title,
            target = src.target,
            lastUsed = src.lastUsed
        };
    }
}
