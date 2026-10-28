// JumpDock — scannable app catalog + pin merge.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

public sealed class CatalogApp
{
    public string Id;
    public string DisplayName;
    public string Kind; // exe | lnk | chromeApp
    public string LnkOrExe;
    public string IconPath;
    public string Group;
    public string Jump;
    public bool Starter;
    public string ChromeUrl;
    public string Aumid;
    public string TitleMatch;
    public bool Installed;
}

public static class AppCatalog
{
    private static List<CatalogApp> _scanCache;
    private static int _scanCacheTick;
    private const int ScanCacheMs = 30000;

    public static string JumpDockDir
    {
        get { return JumpPaths.ConfigDir; }
    }

    public static List<CatalogApp> Scan()
    {
        var list = new List<CatalogApp>();
        try
        {
            AddKnown(list);
            for (int i = 0; i < list.Count; i++)
                Resolve(list[i]);
            DiscoverOthers(list);
            SortByCategory(list);
        }
        catch { }
        return list;
    }

    /// <summary>Reuse last Scan for a short window; invalidate after pin/Settings/Reload.</summary>
    public static List<CatalogApp> ScanCached()
    {
        int now = Environment.TickCount;
        if (_scanCache != null)
        {
            int age = now - _scanCacheTick;
            if (age >= 0 && age < ScanCacheMs)
                return _scanCache;
        }
        _scanCache = Scan();
        _scanCacheTick = now;
        return _scanCache;
    }

    public static void InvalidateScan()
    {
        _scanCache = null;
    }

    public static CatalogApp FindByName(IList<CatalogApp> apps, string name)
    {
        if (apps == null || string.IsNullOrEmpty(name))
            return null;
        for (int i = 0; i < apps.Count; i++)
        {
            if (string.Equals(apps[i].DisplayName, name, StringComparison.OrdinalIgnoreCase))
                return apps[i];
        }
        return null;
    }

    public static CatalogApp FindById(IList<CatalogApp> apps, string id)
    {
        if (apps == null || string.IsNullOrEmpty(id))
            return null;
        for (int i = 0; i < apps.Count; i++)
        {
            if (string.Equals(apps[i].Id, id, StringComparison.OrdinalIgnoreCase))
                return apps[i];
        }
        return null;
    }

    public static void EnsureChromeApp(CatalogApp app)
    {
        if (app == null || !string.Equals(app.Kind, "chromeApp", StringComparison.OrdinalIgnoreCase))
            return;
        string dir = JumpDockDir;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        string launcher = Path.Combine(dir, "JumpDockLauncher.exe");
        string lnk = Path.Combine(dir, app.DisplayName + ".lnk");
        string icon = PreferIcon(app.IconPath);
        string args = "--name \"" + app.DisplayName + "\" --url \"" + app.ChromeUrl + "\" --icon \"" + icon +
                      "\" --aumid \"" + app.Aumid + "\"";
        if (!string.IsNullOrEmpty(app.TitleMatch))
            args += " --title-match \"" + app.TitleMatch + "\"";
        WriteShortcut(lnk, launcher, args, dir);
        app.LnkOrExe = lnk;
        app.Installed = File.Exists(launcher) && File.Exists(lnk);
    }

    public static DockItem ToDockItem(CatalogApp app)
    {
        if (app == null)
            return null;
        if (string.Equals(app.Kind, "chromeApp", StringComparison.OrdinalIgnoreCase))
            EnsureChromeApp(app);
        string lnk = app.LnkOrExe ?? "";
        string icon = PreferPng(app.IconPath);
        // Prefer env-friendly paths in dock.json when under JumpDock / known roots
        lnk = ToStoragePath(lnk);
        icon = ToStoragePath(icon);
        return new DockItem
        {
            name = app.DisplayName,
            lnk = lnk,
            icon = icon
        };
    }

    /// <summary>Build a dock pin from an .exe or .lnk path (Settings Add app…).</summary>
    public static DockItem TryCreateCustomItem(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;
        string ext = Path.GetExtension(path) ?? "";
        if (!string.Equals(ext, ".exe", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(ext, ".lnk", StringComparison.OrdinalIgnoreCase))
            return null;
        string name = Path.GetFileNameWithoutExtension(path);
        if (string.IsNullOrEmpty(name))
            name = "App";
        return new DockItem
        {
            name = name,
            lnk = ToStoragePath(path),
            icon = ""
        };
    }

    /// <summary>Append catalog apps not already pinned (by display name). Returns how many were added.</summary>
    public static int AppendCatalogPins(IList<DockItem> items, IList<CatalogApp> apps)
    {
        if (items == null || apps == null)
            return 0;
        int added = 0;
        for (int i = 0; i < apps.Count; i++)
        {
            CatalogApp app = apps[i];
            if (app == null || string.IsNullOrEmpty(app.DisplayName))
                continue;
            if (!app.Installed && !string.Equals(app.Kind, "chromeApp", StringComparison.OrdinalIgnoreCase))
                continue;
            bool exists = false;
            for (int j = 0; j < items.Count; j++)
            {
                if (items[j] != null &&
                    string.Equals(items[j].name, app.DisplayName, StringComparison.OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }
            if (exists)
                continue;
            DockItem item = ToDockItem(app);
            if (item == null)
                continue;
            items.Add(item);
            added++;
        }
        if (added > 0)
            InvalidateScan();
        return added;
    }

    public static bool IsOnDock(DockConfig cfg, string displayName)
    {
        if (cfg == null || cfg.items == null || string.IsNullOrEmpty(displayName))
            return false;
        for (int i = 0; i < cfg.items.Count; i++)
        {
            if (cfg.items[i] != null &&
                string.Equals(cfg.items[i].name, displayName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static void TogglePin(DockConfig cfg, CatalogApp app, string configPath)
    {
        if (cfg == null || app == null)
            return;
        if (cfg.items == null)
            cfg.items = new List<DockItem>();
        int idx = -1;
        for (int i = 0; i < cfg.items.Count; i++)
        {
            if (cfg.items[i] != null &&
                string.Equals(cfg.items[i].name, app.DisplayName, StringComparison.OrdinalIgnoreCase))
            {
                idx = i;
                break;
            }
        }
        if (idx >= 0)
            cfg.items.RemoveAt(idx);
        else
        {
            var item = ToDockItem(app);
            if (item != null)
                cfg.items.Add(item);
        }
        cfg.setupComplete = true;
        DockConfigStore.Save(cfg, configPath);
        InvalidateScan();
    }

    public static void RunSeed()
    {
        try
        {
            string path = DockConfigStore.DefaultPath;
            DockConfig cfg = DockConfigStore.Load(path);
            SeedIfEmpty(cfg, path);
            InvalidateScan();
            WriteReport(ScanCached());
        }
        catch (Exception ex)
        {
            try
            {
                File.WriteAllText(Path.Combine(JumpDockDir, "apps-error.txt"), ex.ToString());
            }
            catch
            {
            }
        }
    }

    public static void SeedIfEmpty(DockConfig cfg, string configPath)
    {
        if (cfg == null)
            return;
        if (cfg.items != null && cfg.items.Count > 0)
            return;
        List<CatalogApp> scan = Scan();
        var items = new List<DockItem>();
        for (int i = 0; i < scan.Count; i++)
        {
            CatalogApp app = scan[i];
            if (app == null || !app.Starter || !app.Installed)
                continue;
            DockItem item = ToDockItem(app);
            if (item != null)
                items.Add(item);
        }
        cfg.items = items;
        DockConfigStore.Save(cfg, configPath);
    }

    public static void WriteReport(IList<CatalogApp> apps)
    {
        try
        {
            string dir = JumpDockDir;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            var sb = new StringBuilder();
            sb.Append("[\r\n");
            bool any = false;
            if (apps != null)
            {
                for (int i = 0; i < apps.Count; i++)
                {
                    CatalogApp app = apps[i];
                    if (app == null)
                        continue;
                    if (any)
                        sb.Append(",\r\n");
                    any = true;
                    sb.Append("  {\"name\":\"");
                    sb.Append(Json(app.DisplayName));
                    sb.Append("\",\"category\":\"");
                    sb.Append(Json(app.Group));
                    sb.Append("\",\"installed\":");
                    sb.Append(app.Installed ? "true" : "false");
                    sb.Append(",\"jump\":");
                    sb.Append(string.IsNullOrEmpty(app.Jump) ? "false" : "true");
                    sb.Append(",\"starter\":");
                    sb.Append(app.Starter ? "true" : "false");
                    sb.Append(",\"path\":\"");
                    sb.Append(Json(app.LnkOrExe));
                    sb.Append("\"}");
                }
            }
            sb.Append("\r\n]\r\n");
            File.WriteAllText(Path.Combine(dir, "apps.json"), sb.ToString());
        }
        catch
        {
        }
    }

    private static string Json(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", " ").Replace("\n", " ");
    }

    private static void AddKnown(List<CatalogApp> list)
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string programs = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string programsX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        Add(list, Chrome("github-pr", "GitHub PR", "Web", "GitHub PR", true,
            "https://github.com/pulls", "JumpDock.GitHubPR", "Pull requests",
            Icon("github.png", "GitHubPR.png", "GitHubPR.ico")));
        Add(list, Exe("explorer", "File Explorer", "Utilities", "File Explorer", true,
            new string[] { Path.Combine(Environment.SystemDirectory, "explorer.exe"), @"C:\Windows\explorer.exe" },
            Icon("Explorer.png", "Explorer.ico")));
        Add(list, Exe("chrome", "Chrome", "Browsers", "Chrome", true,
            new string[] { Path.Combine(programs, @"Google\Chrome\Application\chrome.exe") },
            Icon("Chrome.png", "Chrome.ico")));
        Add(list, Exe("edge", "Edge", "Browsers", "", true,
            new string[]
            {
                Path.Combine(programsX86, @"Microsoft\Edge\Application\msedge.exe"),
                Path.Combine(programs, @"Microsoft\Edge\Application\msedge.exe")
            },
            Icon("Edge.png", "Edge.ico"), "Microsoft Edge"));
        Add(list, Exe("cursor", "Cursor", "Development", "", true,
            new string[]
            {
                Path.Combine(local, @"Programs\cursor\Cursor.exe"),
                Path.Combine(programs, @"cursor\Cursor.exe")
            },
            Icon("Cursor.png", "Cursor.ico"), "Cursor"));
        Add(list, Exe("vscode", "VS Code", "Development", "VS Code", true,
            new string[] { Path.Combine(programs, @"Microsoft VS Code\Code.exe") },
            Icon("VSCode.png", "VSCode.ico"), "Visual Studio Code", "VS Code"));
        Add(list, Lnk("intellij", "IntelliJ IDEA", "Development", "IntelliJ IDEA", true,
            Icon("IntelliJ.png", "IntelliJ.ico"), "IntelliJ IDEA"));
        Add(list, Lnk("github-desktop", "GitHub Desktop", "Development", "GitHub Desktop", true,
            Icon("GitHubDesktop.png", "GitHubDesktop.ico"), "GitHub Desktop"));
        Add(list, Exe("compass", "MongoDB Compass", "Development", "MongoDB Compass", true,
            new string[] { Path.Combine(programs, @"MongoDB Compass\MongoDBCompass.exe") },
            Icon("Compass.png", "Compass.ico"), "MongoDB Compass"));
        Add(list, Lnk("lens", "Lens", "Development", "Lens", false,
            Icon("Lens.png", "Lens.ico"), "Lens", "OpenLens"));
        Add(list, Lnk("notepadpp", "Notepad++", "Development", "Notepad++", false,
            Icon("NotepadPP.png", "NotepadPP.ico"), "Notepad++"));
        Add(list, Lnk("teams", "Teams", "Communication", "", true,
            Icon("Teams.png", "Teams.ico"), "Microsoft Teams", "Teams"));
        Add(list, Lnk("termius", "Termius", "Communication", "Termius", true,
            Icon("Termius.png", "Termius.ico"), "Termius"));
        Add(list, Lnk("cisco-vpn", "Cisco VPN", "Utilities", "", false,
            Icon("CiscoVPN.png"), "Cisco Secure Client"));
        Add(list, Lnk("company-portal", "Company Portal", "Utilities", "", false,
            Icon("CompanyPortal.png", "CompanyPortal.ico"), "Company Portal"));
        Add(list, Lnk("jira", "Jira", "Web", "Jira", false,
            Icon("Jira.png", "Jira.ico"), "Jira"));
        Add(list, Lnk("confluence", "Confluence", "Web", "Confluence", false,
            Icon("Confluence.png", "Confluence.ico"), "Confluence"));
    }

    private static void Add(List<CatalogApp> list, CatalogApp app)
    {
        if (app != null)
            list.Add(app);
    }

    private static void DiscoverOthers(List<CatalogApp> list)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == null)
                continue;
            if (!string.IsNullOrEmpty(list[i].DisplayName))
                names.Add(list[i].DisplayName);
            if (!string.IsNullOrEmpty(list[i].LnkOrExe))
                used.Add(list[i].LnkOrExe);
        }
        string[] roots = StartMenuRoots();
        int added = 0;
        for (int r = 0; r < roots.Length; r++)
        {
            if (added >= 60 || !Directory.Exists(roots[r]))
                continue;
            string[] files;
            try { files = Directory.GetFiles(roots[r], "*.lnk", SearchOption.AllDirectories); }
            catch { continue; }
            for (int i = 0; i < files.Length; i++)
            {
                if (added >= 60)
                    break;
                string name = Path.GetFileNameWithoutExtension(files[i]) ?? "";
                if (!KeepDiscovered(name) || names.Contains(name) || used.Contains(files[i]))
                    continue;
                names.Add(name);
                used.Add(files[i]);
                list.Add(new CatalogApp
                {
                    Id = "found-" + added.ToString(),
                    DisplayName = name,
                    Kind = "lnk",
                    LnkOrExe = files[i],
                    Group = "Other",
                    Jump = "",
                    Starter = false,
                    Installed = true
                });
                added++;
            }
        }
    }

    private static bool KeepDiscovered(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;
        string lower = name.ToLowerInvariant();
        if (lower.IndexOf("uninstall") >= 0)
            return false;
        if (lower.IndexOf("readme") >= 0 || lower.IndexOf("help") >= 0)
            return false;
        if (lower.IndexOf("license") >= 0 || lower.IndexOf("what's new") >= 0)
            return false;
        return true;
    }

    private static void SortByCategory(List<CatalogApp> list)
    {
        list.Sort(delegate(CatalogApp a, CatalogApp b)
        {
            int c = CategoryRank(a == null ? "" : a.Group).CompareTo(CategoryRank(b == null ? "" : b.Group));
            if (c != 0)
                return c;
            string an = a == null || a.DisplayName == null ? "" : a.DisplayName;
            string bn = b == null || b.DisplayName == null ? "" : b.DisplayName;
            return string.Compare(an, bn, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static int CategoryRank(string category)
    {
        if (category == "Web") return 0;
        if (category == "Browsers") return 1;
        if (category == "Development") return 2;
        if (category == "Communication") return 3;
        if (category == "Utilities") return 4;
        return 5;
    }

    private static string[] StartMenuRoots()
    {
        return new string[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Microsoft\Windows\Start Menu\Programs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                @"Microsoft\Windows\Start Menu\Programs")
        };
    }

    private static CatalogApp Chrome(string id, string name, string category, string jump, bool starter, string url, string aumid, string titleMatch, string icon)
    {
        return new CatalogApp
        {
            Id = id,
            DisplayName = name,
            Kind = "chromeApp",
            ChromeUrl = url,
            Aumid = aumid,
            TitleMatch = titleMatch,
            IconPath = icon,
            LnkOrExe = Path.Combine(JumpDockDir, name + ".lnk"),
            Group = category,
            Jump = jump,
            Starter = starter,
            Installed = true
        };
    }

    private static CatalogApp Exe(string id, string name, string category, string jump, bool starter, string[] paths, string icon, params string[] names)
    {
        return new CatalogApp
        {
            Id = id,
            DisplayName = name,
            Kind = "exe",
            LnkOrExe = paths == null ? "" : string.Join("\n", paths),
            IconPath = icon,
            Group = category,
            Jump = jump,
            Starter = starter,
            TitleMatch = names == null ? name : string.Join("\n", names)
        };
    }

    private static CatalogApp Lnk(string id, string name, string category, string jump, bool starter, string icon, params string[] names)
    {
        return new CatalogApp
        {
            Id = id,
            DisplayName = name,
            Kind = "lnk",
            LnkOrExe = "",
            IconPath = icon,
            Group = category,
            Jump = jump,
            Starter = starter,
            TitleMatch = names == null || names.Length == 0 ? name : string.Join("\n", names)
        };
    }

    private static void Resolve(CatalogApp app)
    {
        if (app == null)
            return;
        if (string.Equals(app.Kind, "chromeApp", StringComparison.OrdinalIgnoreCase))
        {
            string lnk = Path.Combine(JumpDockDir, app.DisplayName + ".lnk");
            string launcher = Path.Combine(JumpDockDir, "JumpDockLauncher.exe");
            app.LnkOrExe = lnk;
            if (!File.Exists(lnk) && File.Exists(launcher))
                EnsureChromeApp(app);
            app.Installed = File.Exists(lnk) || File.Exists(launcher);
            return;
        }
        string[] candidates = (app.LnkOrExe ?? "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        string found = null;
        for (int i = 0; i < candidates.Length; i++)
        {
            string p = candidates[i].Trim();
            if (File.Exists(p))
            {
                found = p;
                break;
            }
        }
        if (found == null)
            found = OwnShortcut(app);
        if (found == null)
            found = FindStartMenu(app.TitleMatch, app.DisplayName);
        app.LnkOrExe = found ?? (candidates.Length > 0 ? candidates[0] : "");
        app.Installed = !string.IsNullOrEmpty(found) && File.Exists(found);
    }

    private static string OwnShortcut(CatalogApp app)
    {
        string joined = app.DisplayName ?? "";
        if (!string.IsNullOrEmpty(app.TitleMatch))
            joined = joined + "\n" + app.TitleMatch;
        string[] names = joined.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < names.Length; i++)
        {
            string path = Path.Combine(JumpDockDir, names[i].Trim() + ".lnk");
            if (File.Exists(path))
                return path;
        }
        return null;
    }

    private static string FindStartMenu(string names, string displayName)
    {
        string joined = string.IsNullOrEmpty(names) ? displayName : names;
        if (string.IsNullOrEmpty(joined))
            return null;
        string[] wanted = joined.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        string[] roots = StartMenuRoots();
        string contains = null;
        for (int n = 0; n < wanted.Length; n++)
        {
            string want = wanted[n].Trim();
            if (want.Length == 0)
                continue;
            for (int r = 0; r < roots.Length; r++)
            {
                try
                {
                    if (!Directory.Exists(roots[r]))
                        continue;
                    foreach (string file in Directory.GetFiles(roots[r], "*.lnk", SearchOption.AllDirectories))
                    {
                        string fn = Path.GetFileNameWithoutExtension(file) ?? "";
                        if (string.Equals(fn, want, StringComparison.OrdinalIgnoreCase))
                            return file;
                        if (contains == null && fn.IndexOf(want, StringComparison.OrdinalIgnoreCase) >= 0 && KeepDiscovered(fn))
                            contains = file;
                    }
                }
                catch { }
            }
        }
        return contains;
    }

    private static string Icon(params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            string p = Path.Combine(JumpDockDir, "icons", names[i]);
            if (File.Exists(p))
                return p;
        }
        if (names.Length > 0)
            return Path.Combine(JumpDockDir, "icons", names[0]);
        return "";
    }

    private static string PreferIcon(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;
        if (File.Exists(path))
            return path;
        string ico = Path.ChangeExtension(path, ".ico");
        if (File.Exists(ico))
            return ico;
        return path;
    }

    private static string PreferPng(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;
        string png = Path.ChangeExtension(path, ".png");
        if (File.Exists(png))
            return png;
        if (File.Exists(path))
            return path;
        return path;
    }

    private static string ToStoragePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;
        try
        {
            string wa = JumpDockDir;
            if (path.StartsWith(wa, StringComparison.OrdinalIgnoreCase))
                return "%LOCALAPPDATA%\\JumpDock" + path.Substring(wa.Length);
            string desk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (path.StartsWith(desk, StringComparison.OrdinalIgnoreCase))
                return "%DESKTOP%" + path.Substring(desk.Length);
            string ad = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (path.StartsWith(ad, StringComparison.OrdinalIgnoreCase))
                return "%APPDATA%" + path.Substring(ad.Length);
            string pd = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (path.StartsWith(pd, StringComparison.OrdinalIgnoreCase))
                return "%PROGRAMDATA%" + path.Substring(pd.Length);
        }
        catch { }
        return path;
    }

    private static void WriteShortcut(string lnk, string target, string args, string workDir)
    {
        try
        {
            var t = Type.GetTypeFromProgID("WScript.Shell");
            if (t == null)
                return;
            object shell = Activator.CreateInstance(t);
            object sc = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
            Type st = sc.GetType();
            st.InvokeMember("TargetPath", BindingFlags.SetProperty, null, sc, new object[] { target });
            st.InvokeMember("Arguments", BindingFlags.SetProperty, null, sc, new object[] { args ?? "" });
            st.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc, new object[] { workDir ?? "" });
            st.InvokeMember("WindowStyle", BindingFlags.SetProperty, null, sc, new object[] { 1 });
            st.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
        }
        catch { }
    }
}
