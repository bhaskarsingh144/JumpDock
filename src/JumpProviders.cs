// JumpDock — jump recent providers (GitHub Desktop, Compass, Lens, Termius).
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

public sealed class GitHubDesktopJumpProvider : IJumpRecentProvider
{
    public string DockName { get { return "GitHub Desktop"; } }

    public IList<JumpItem> Collect()
    {
        var list = new List<JumpItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GitHub Desktop");
        TryAddFromJson(list, seen, Path.Combine(root, "storage.json"));
        TryAddFromJson(list, seen, Path.Combine(root, "IndexedDB"));
        // Common Desktop path: %APPDATA%\GitHub Desktop\storage\... — also scan repo-info style files
        TryScanTextFiles(list, seen, root, 3);
        string local = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GitHubDesktop");
        TryScanTextFiles(list, seen, local, 3);
        return list;
    }

    private static void TryAddFromJson(List<JumpItem> list, HashSet<string> seen, string path)
    {
        if (!File.Exists(path))
            return;
        string text;
        try { text = File.ReadAllText(path); }
        catch { return; }
        foreach (Match m in Regex.Matches(text, @"[A-Za-z]:\\[^""\r\n]+", RegexOptions.IgnoreCase))
        {
            string p = CleanPath(m.Value);
            if (p == null || !Directory.Exists(p))
                continue;
            if (!Directory.Exists(Path.Combine(p, ".git")) && !File.Exists(Path.Combine(p, ".git")))
                continue;
            AddFolder(list, seen, p, 40);
        }
    }

    private static void TryScanTextFiles(List<JumpItem> list, HashSet<string> seen, string root, int depth)
    {
        if (list.Count >= 40 || !Directory.Exists(root) || depth < 0)
            return;
        try
        {
            foreach (var file in Directory.GetFiles(root))
            {
                string ext = Path.GetExtension(file) ?? "";
                if (ext.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals(".log", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrEmpty(ext))
                {
                    TryAddFromJson(list, seen, file);
                    if (list.Count >= 40) return;
                }
            }
            if (depth == 0) return;
            foreach (var dir in Directory.GetDirectories(root))
            {
                string name = Path.GetFileName(dir) ?? "";
                if (name.Equals("Cache", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("GPUCache", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Code Cache", StringComparison.OrdinalIgnoreCase))
                    continue;
                TryScanTextFiles(list, seen, dir, depth - 1);
                if (list.Count >= 40) return;
            }
        }
        catch { }
    }

    private static void AddFolder(List<JumpItem> list, HashSet<string> seen, string path, int cap)
    {
        if (list.Count >= cap) return;
        path = path.TrimEnd('\\', '/');
        if (!seen.Add(path)) return;
        list.Add(new JumpItem
        {
            kind = "folder",
            title = Path.GetFileName(path),
            target = path,
            lastUsed = DateTime.UtcNow.ToString("o")
        });
    }

    private static string CleanPath(string p)
    {
        if (string.IsNullOrEmpty(p)) return null;
        p = p.Trim().TrimEnd(',', '"', '\'', '}', ']');
        if (p.Length < 4) return null;
        try
        {
            if (p.IndexOf('\\') < 0) return null;
            return Path.GetFullPath(p);
        }
        catch { return null; }
    }
}

public sealed class MongoCompassJumpProvider : IJumpRecentProvider
{
    public string DockName { get { return "MongoDB Compass"; } }

    public IList<JumpItem> Collect()
    {
        var list = new List<JumpItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MongoDB Compass");
        TryFile(list, seen, Path.Combine(baseDir, "Connections", "connections.json"));
        TryFile(list, seen, Path.Combine(baseDir, "Connections.json"));
        // Recents / favorite connection strings in various Compass versions
        TryScan(list, seen, baseDir, 2);
        string local = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "mongodb", "compass");
        TryScan(list, seen, local, 2);
        return list;
    }

    private static void TryScan(List<JumpItem> list, HashSet<string> seen, string root, int depth)
    {
        if (!Directory.Exists(root) || list.Count >= 40 || depth < 0)
            return;
        try
        {
            foreach (var file in Directory.GetFiles(root, "*.json"))
                TryFile(list, seen, file);
            if (depth == 0) return;
            foreach (var dir in Directory.GetDirectories(root))
            {
                string n = Path.GetFileName(dir) ?? "";
                if (n.IndexOf("Cache", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                TryScan(list, seen, dir, depth - 1);
            }
        }
        catch { }
    }

    private static void TryFile(List<JumpItem> list, HashSet<string> seen, string path)
    {
        if (!File.Exists(path) || list.Count >= 40)
            return;
        string text;
        try { text = File.ReadAllText(path); }
        catch { return; }
        foreach (Match m in Regex.Matches(text, "\"(?:favoriteName|name|connectionName)\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase))
        {
            string title = Unescape(m.Groups[1].Value);
            // look nearby for connection string
            int idx = m.Index;
            string window = text.Substring(Math.Max(0, idx - 200), Math.Min(800, text.Length - Math.Max(0, idx - 200)));
            Match cs = Regex.Match(window, "\"(?:connectionString|uri)\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
            string target = cs.Success ? Unescape(cs.Groups[1].Value) : title;
            AddConn(list, seen, title, target);
            if (list.Count >= 40) return;
        }
        foreach (Match m in Regex.Matches(text, "mongodb(?:\\+srv)?://[^\\s\"']+", RegexOptions.IgnoreCase))
        {
            string uri = m.Value.Trim().TrimEnd(',', '"', '\'');
            string title = ShortMongoTitle(uri);
            AddConn(list, seen, title, uri);
            if (list.Count >= 40) return;
        }
    }

    private static void AddConn(List<JumpItem> list, HashSet<string> seen, string title, string target)
    {
        if (string.IsNullOrEmpty(target) || !seen.Add(target))
            return;
        list.Add(new JumpItem
        {
            kind = "url",
            title = string.IsNullOrEmpty(title) ? target : title,
            target = target,
            lastUsed = DateTime.UtcNow.ToString("o")
        });
    }

    private static string ShortMongoTitle(string uri)
    {
        try
        {
            // hide credentials in title
            int at = uri.IndexOf('@');
            if (at > 0)
            {
                int scheme = uri.IndexOf("://");
                string host = uri.Substring(at + 1);
                int slash = host.IndexOf('/');
                if (slash >= 0) host = host.Substring(0, slash);
                int q = host.IndexOf('?');
                if (q >= 0) host = host.Substring(0, q);
                return host;
            }
        }
        catch { }
        if (uri.Length > 48) return uri.Substring(0, 48) + "...";
        return uri;
    }

    private static string Unescape(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.Replace("\\/", "/").Replace("\\\"", "\"").Replace("\\\\", "\\");
    }
}

public sealed class LensJumpProvider : IJumpRecentProvider
{
    public string DockName { get { return "Lens"; } }

    public IList<JumpItem> Collect()
    {
        var list = new List<JumpItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string[] roots = new string[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lens"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenLens"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mirantis Lens")
        };
        for (int i = 0; i < roots.Length; i++)
            Scan(list, seen, roots[i], 3);
        // kubeconfig contexts as fallback folders/files
        string kube = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kube", "config");
        if (File.Exists(kube))
            AddFile(list, seen, kube, "kubeconfig");
        return list;
    }

    private static void Scan(List<JumpItem> list, HashSet<string> seen, string root, int depth)
    {
        if (!Directory.Exists(root) || list.Count >= 40 || depth < 0)
            return;
        try
        {
            foreach (var file in Directory.GetFiles(root))
            {
                string name = Path.GetFileName(file) ?? "";
                if (name.IndexOf("kubeconfig", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("config", StringComparison.OrdinalIgnoreCase))
                    AddFile(list, seen, file, name);
                else if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    ExtractPaths(list, seen, file);
                if (list.Count >= 40) return;
            }
            foreach (var dir in Directory.GetDirectories(root))
            {
                string n = Path.GetFileName(dir) ?? "";
                if (n.IndexOf("Cache", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("blob_storage", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                if (n.IndexOf("kubeconfig", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("catalog", StringComparison.OrdinalIgnoreCase) >= 0)
                    AddFolder(list, seen, dir);
                Scan(list, seen, dir, depth - 1);
                if (list.Count >= 40) return;
            }
        }
        catch { }
    }

    private static void ExtractPaths(List<JumpItem> list, HashSet<string> seen, string file)
    {
        string text;
        try { text = File.ReadAllText(file); }
        catch { return; }
        foreach (Match m in Regex.Matches(text, "\"(?:name|context|cluster)\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase))
        {
            string title = m.Groups[1].Value;
            if (string.IsNullOrEmpty(title) || !seen.Add("lens:" + title))
                continue;
            list.Add(new JumpItem
            {
                kind = "file",
                title = title,
                target = file,
                lastUsed = DateTime.UtcNow.ToString("o")
            });
            if (list.Count >= 40) return;
        }
    }

    private static void AddFile(List<JumpItem> list, HashSet<string> seen, string path, string title)
    {
        if (!File.Exists(path) || !seen.Add(path)) return;
        list.Add(new JumpItem
        {
            kind = "file",
            title = string.IsNullOrEmpty(title) ? Path.GetFileName(path) : title,
            target = path,
            lastUsed = DateTime.UtcNow.ToString("o")
        });
    }

    private static void AddFolder(List<JumpItem> list, HashSet<string> seen, string path)
    {
        if (!Directory.Exists(path) || !seen.Add(path)) return;
        list.Add(new JumpItem
        {
            kind = "folder",
            title = Path.GetFileName(path),
            target = path,
            lastUsed = DateTime.UtcNow.ToString("o")
        });
    }
}

public sealed class TermiusJumpProvider : IJumpRecentProvider
{
    public string DockName { get { return "Termius"; } }

    public IList<JumpItem> Collect()
    {
        var list = new List<JumpItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string[] roots = new string[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Termius"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Termius")
        };
        for (int i = 0; i < roots.Length; i++)
            Scan(list, seen, roots[i], 3);
        return list;
    }

    private static void Scan(List<JumpItem> list, HashSet<string> seen, string root, int depth)
    {
        if (!Directory.Exists(root) || list.Count >= 40 || depth < 0)
            return;
        try
        {
            foreach (var file in Directory.GetFiles(root))
            {
                string ext = Path.GetExtension(file) ?? "";
                string name = Path.GetFileName(file) ?? "";
                if (ext.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
                    name.IndexOf("host", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("ssh", StringComparison.OrdinalIgnoreCase) >= 0)
                    ParseHosts(list, seen, file);
                if (list.Count >= 40) return;
            }
            foreach (var dir in Directory.GetDirectories(root))
            {
                string n = Path.GetFileName(dir) ?? "";
                if (n.IndexOf("Cache", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("GPU", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                Scan(list, seen, dir, depth - 1);
                if (list.Count >= 40) return;
            }
        }
        catch { }
    }

    private static void ParseHosts(List<JumpItem> list, HashSet<string> seen, string file)
    {
        byte[] buf;
        try { buf = File.ReadAllBytes(file); }
        catch { return; }
        // Termius may store SQLite/binary; extract readable host-like tokens.
        string text;
        try { text = Encoding.UTF8.GetString(buf); }
        catch { return; }
        foreach (Match m in Regex.Matches(text, "\"(?:label|group|title|hostname|address|host)\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase))
        {
            string title = m.Groups[1].Value.Trim();
            if (title.Length < 2 || title.Length > 80)
                continue;
            if (title.StartsWith("{") || title.IndexOf(' ') == 0)
                continue;
            string key = "termius:" + title;
            if (!seen.Add(key))
                continue;
            // Open Termius app (path open is not reliable); store as file pointing at Termius data for pin UX
            list.Add(new JumpItem
            {
                kind = "file",
                title = title,
                target = file,
                lastUsed = DateTime.UtcNow.ToString("o")
            });
            if (list.Count >= 40) return;
        }
        foreach (Match m in Regex.Matches(text, @"\b(?:\d{1,3}\.){3}\d{1,3}\b"))
        {
            string ip = m.Value;
            if (!seen.Add("termius-ip:" + ip))
                continue;
            list.Add(new JumpItem
            {
                kind = "file",
                title = ip,
                target = file,
                lastUsed = DateTime.UtcNow.ToString("o")
            });
            if (list.Count >= 40) return;
        }
    }
}
