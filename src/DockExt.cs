// JumpDock — config store, menu metrics, jump provider registry.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows.Forms;

public class DockItem
{
    public string name { get; set; }
    public string lnk { get; set; }
    public string icon { get; set; }
}

public class DockConfig
{
    public string edge { get; set; }
    public int iconSize { get; set; }
    /// <summary>Design DIP size for More apps grid icons (default 64).</summary>
    public int moreIconSize { get; set; }
    /// <summary>Accent preset: blue | teal | amber | gray.</summary>
    public string accent { get; set; }
    public int peek { get; set; }
    public double menuScale { get; set; }
    public int maxVisible { get; set; }
    public string monitor { get; set; }
    public bool startWithWindows { get; set; }
    public bool setupComplete { get; set; }
    public List<DockItem> items { get; set; }
    public List<HubPin> hubPins { get; set; }
}

public class HubPin
{
    public string list { get; set; }
    public string title { get; set; }
    public string url { get; set; }
}

public static class DockDefaults
{
    public const double MenuScaleNormal = 1.0;
    public const double MenuScaleLarge = 1.15;
    public const int DefaultMaxVisible = 16;
    public const int DefaultIconSize = 96;
    public const int DefaultMoreIconSize = 64;
    public const int DefaultPeek = 6;

    public static void Normalize(DockConfig cfg)
    {
        if (cfg == null)
            return;
        if (string.IsNullOrEmpty(cfg.edge))
            cfg.edge = "top";
        if (cfg.iconSize < 72 || cfg.iconSize > 128)
            cfg.iconSize = cfg.iconSize > 0 ? ClampInt(cfg.iconSize, 72, 128) : DefaultIconSize;
        if (cfg.moreIconSize <= 0)
            cfg.moreIconSize = DefaultMoreIconSize;
        else
            cfg.moreIconSize = ClampInt(cfg.moreIconSize, 48, 96);
        cfg.accent = DockAccent.NormalizeKey(cfg.accent);
        if (cfg.peek < 0 || cfg.peek > 24)
            cfg.peek = DefaultPeek;
        if (cfg.menuScale < 0.99 || cfg.menuScale > 1.35)
            cfg.menuScale = MenuScaleLarge;
        if (cfg.maxVisible < 8 || cfg.maxVisible > 24)
            cfg.maxVisible = cfg.maxVisible > 0 ? ClampInt(cfg.maxVisible, 8, 24) : DefaultMaxVisible;
        if (string.IsNullOrEmpty(cfg.monitor))
            cfg.monitor = "primary";
        // Prefer live Startup folder state over stale JSON.
        cfg.startWithWindows = DockStartup.IsEnabled();
        if (cfg.items == null)
            cfg.items = new List<DockItem>();
        if (cfg.hubPins == null)
            cfg.hubPins = new List<HubPin>();
    }

    public static List<HubPin> DefaultHubPins()
    {
        return new List<HubPin>();
    }

    private static int ClampInt(int v, int lo, int hi)
    {
        if (v < lo) return lo;
        if (v > hi) return hi;
        return v;
    }
}

/// <summary>User Startup folder shortcut for JumpDock.exe.</summary>
public static class DockStartup
{
    public const string ShortcutName = "JumpDock.lnk";

    public static string ShortcutPath
    {
        get
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                ShortcutName);
        }
    }

    public static string ExePath
    {
        get
        {
            try
            {
                string running = Application.ExecutablePath;
                if (!string.IsNullOrEmpty(running) && File.Exists(running))
                    return running;
            }
            catch { }
            return Path.Combine(JumpPaths.ConfigDir, "JumpDock.exe");
        }
    }

    public static bool IsEnabled()
    {
        try { return File.Exists(ShortcutPath); }
        catch { return false; }
    }

    public static void SetEnabled(bool enabled)
    {
        if (enabled)
            EnsureShortcut();
        else
            RemoveShortcut();
    }

    public static void EnsureShortcut()
    {
        string lnk = ShortcutPath;
        string exe = ExePath;
        string dir = Path.GetDirectoryName(exe) ?? "";
        try
        {
            var t = Type.GetTypeFromProgID("WScript.Shell");
            if (t == null)
                return;
            object shell = Activator.CreateInstance(t);
            object sc = t.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
            Type st = sc.GetType();
            st.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { exe });
            st.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { dir });
            st.InvokeMember("WindowStyle", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { 1 });
            st.InvokeMember("Description", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { "JumpDock" });
            st.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, sc, null);
        }
        catch { }
    }

    public static void RemoveShortcut()
    {
        try
        {
            string lnk = ShortcutPath;
            if (File.Exists(lnk))
                File.Delete(lnk);
        }
        catch { }
    }
}

public static class DockConfigStore
{
    public static string DefaultPath
    {
        get
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "JumpDock",
                "dock.json");
        }
    }

    public static DockConfig Load(string path)
    {
        if (string.IsNullOrEmpty(path))
            path = DefaultPath;
        DockConfig cfg = null;
        try
        {
            if (File.Exists(path))
            {
                var ser = new JavaScriptSerializer();
                ser.MaxJsonLength = int.MaxValue;
                cfg = ser.Deserialize<DockConfig>(File.ReadAllText(path));
            }
        }
        catch { }
        if (cfg == null)
            cfg = new DockConfig();
        DockDefaults.Normalize(cfg);
        return cfg;
    }

    public static void Save(DockConfig cfg, string path)
    {
        if (cfg == null)
            return;
        if (string.IsNullOrEmpty(path))
            path = DefaultPath;
        DockDefaults.Normalize(cfg);
        var ser = new JavaScriptSerializer();
        ser.MaxJsonLength = int.MaxValue;
        string json = ser.Serialize(cfg);
        json = PrettyJson(json);
        string dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, json);
    }

    private static string PrettyJson(string compact)
    {
        // Light pretty-print for dock.json readability (C# 5 safe).
        if (string.IsNullOrEmpty(compact))
            return compact;
        var sb = new System.Text.StringBuilder(compact.Length + 64);
        int depth = 0;
        bool inStr = false;
        for (int i = 0; i < compact.Length; i++)
        {
            char c = compact[i];
            if (c == '"' && (i == 0 || compact[i - 1] != '\\'))
                inStr = !inStr;
            if (inStr)
            {
                sb.Append(c);
                continue;
            }
            if (c == '{' || c == '[')
            {
                sb.Append(c);
                sb.Append('\n');
                depth++;
                sb.Append(' ', depth * 2);
            }
            else if (c == '}' || c == ']')
            {
                sb.Append('\n');
                depth--;
                if (depth < 0) depth = 0;
                sb.Append(' ', depth * 2);
                sb.Append(c);
            }
            else if (c == ',')
            {
                sb.Append(c);
                sb.Append('\n');
                sb.Append(' ', depth * 2);
            }
            else if (c == ':')
            {
                sb.Append(c);
                sb.Append(' ');
            }
            else if (!char.IsWhiteSpace(c))
                sb.Append(c);
        }
        sb.Append('\n');
        return sb.ToString();
    }

    public static string Expand(string p)
    {
        if (string.IsNullOrEmpty(p)) return p;
        p = p.Replace("%DESKTOP%", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
        return Environment.ExpandEnvironmentVariables(p);
    }
}

public sealed class MenuMetrics
{
    public double Scale;
    public float HeaderPt;
    public float HintPt;
    public float TitlePt;
    public float SubPt;
    public float SectionPt;
    public int BaseWidth;
    public int MaxList;
    public FontStyle TitleStyle;

    public static MenuMetrics FromScale(double scale)
    {
        if (scale < 1.0) scale = 1.0;
        if (scale > 1.35) scale = 1.35;
        var m = new MenuMetrics();
        m.Scale = scale;
        m.HeaderPt = (float)(10.5 * scale / DockDefaults.MenuScaleLarge * 1.15);
        m.HintPt = (float)(8.5 * scale / DockDefaults.MenuScaleLarge * 1.15);
        m.TitlePt = (float)(10.0 * scale / DockDefaults.MenuScaleLarge * 1.15);
        m.SubPt = (float)(8.5 * scale / DockDefaults.MenuScaleLarge * 1.15);
        m.SectionPt = (float)(8.5 * scale / DockDefaults.MenuScaleLarge * 1.15);
        // At 1.15 -> ~710; at 1.0 -> ~620
        m.BaseWidth = (int)Math.Round(620 * scale / 1.0 * (scale >= 1.1 ? 1.0 : 1.0));
        m.BaseWidth = (int)Math.Round(620.0 * scale / 1.0);
        if (Math.Abs(scale - DockDefaults.MenuScaleLarge) < 0.02)
            m.BaseWidth = 710;
        else if (Math.Abs(scale - DockDefaults.MenuScaleNormal) < 0.02)
            m.BaseWidth = 620;
        else
            m.BaseWidth = (int)Math.Round(620.0 * scale);
        m.MaxList = (int)Math.Round(840.0 * Math.Min(1.1, scale));
        // Large = slight bump over original (8.5/7.5), not heavy bold 10pt
        m.TitleStyle = FontStyle.Regular;
        if (Math.Abs(scale - DockDefaults.MenuScaleLarge) < 0.02)
        {
            m.HeaderPt = 9.5f;
            m.HintPt = 8f;
            m.TitlePt = 9f;
            m.SubPt = 8f;
            m.SectionPt = 8f;
            m.BaseWidth = 660;
            m.TitleStyle = FontStyle.Regular;
        }
        else if (Math.Abs(scale - DockDefaults.MenuScaleNormal) < 0.02)
        {
            m.HeaderPt = 9f;
            m.HintPt = 7.75f;
            m.TitlePt = 8.5f;
            m.SubPt = 7.5f;
            m.SectionPt = 7.5f;
            m.BaseWidth = 620;
            m.TitleStyle = FontStyle.Regular;
        }
        else
        {
            m.HeaderPt = 9f;
            m.HintPt = 7.75f;
            m.TitlePt = 8.5f;
            m.SubPt = 7.5f;
            m.SectionPt = 7.5f;
            m.BaseWidth = (int)Math.Round(620.0 * scale);
        }
        return m;
    }

    public Font MakeHeaderFont()
    {
        return new Font("Segoe UI", HeaderPt, FontStyle.Bold);
    }

    public Font MakeHintFont()
    {
        return new Font("Segoe UI", HintPt, FontStyle.Regular);
    }

    public Font MakeTitleFont()
    {
        return new Font("Segoe UI", TitlePt, TitleStyle);
    }

    public Font MakeSubFont()
    {
        return new Font("Segoe UI", SubPt, FontStyle.Regular);
    }

    public Font MakeSectionFont()
    {
        return new Font("Segoe UI", SectionPt, FontStyle.Bold);
    }
}

public interface IJumpRecentProvider
{
    string DockName { get; }
    IList<JumpItem> Collect();
}

public static class JumpProviderRegistry
{
    private static readonly List<IJumpRecentProvider> Providers = new List<IJumpRecentProvider>();
    private static bool _built;

    public static void EnsureBuilt()
    {
        if (_built)
            return;
        _built = true;
        Providers.Add(new GitHubDesktopJumpProvider());
        Providers.Add(new MongoCompassJumpProvider());
        Providers.Add(new LensJumpProvider());
        Providers.Add(new TermiusJumpProvider());
    }

    public static void SyncAll(JumpListStore store)
    {
        if (store == null)
            return;
        EnsureBuilt();
        for (int i = 0; i < Providers.Count; i++)
        {
            var p = Providers[i];
            try
            {
                var items = p.Collect();
                if (items != null && items.Count > 0)
                    store.MergeRecent(p.DockName, ToList(items));
            }
            catch { }
        }
    }

    public static void SyncHubPins(JumpListStore store, DockConfig cfg)
    {
        if (store == null || cfg == null)
            return;
        var pins = cfg.hubPins;
        if (pins == null || pins.Count == 0)
            pins = DockDefaults.DefaultHubPins();
        for (int i = 0; i < pins.Count; i++)
        {
            var hp = pins[i];
            if (hp == null || string.IsNullOrEmpty(hp.url))
                continue;
            string listName = string.IsNullOrEmpty(hp.list) ? "Chrome" : hp.list;
            string title = string.IsNullOrEmpty(hp.title) ? hp.url : hp.title;
            store.EnsurePinned(listName, new JumpItem
            {
                kind = "url",
                title = title,
                target = hp.url.Trim(),
                lastUsed = DateTime.UtcNow.ToString("o")
            });
        }
    }

    private static List<JumpItem> ToList(IList<JumpItem> items)
    {
        var list = new List<JumpItem>();
        for (int i = 0; i < items.Count; i++)
            list.Add(items[i]);
        return list;
    }
}
