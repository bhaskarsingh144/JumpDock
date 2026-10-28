// JumpDock.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
public static class AppRecents
{
    public static void Sync(JumpListStore store)
    {
        Sync(store, null);
    }

    public static void Sync(JumpListStore store, DockConfig cfg)
    {
        if (store == null)
            return;
        string[] names = JumpLists.AppHistoryNames();
        for (int i = 0; i < names.Length; i++)
        {
            try { store.ReplaceRecents(names[i], CollectFor(names[i])); }
            catch { }
        }
        try { JumpProviderRegistry.SyncHubPins(store, cfg); }
        catch { }
    }

    /// <summary>Collect recents for one dock tile (for Refresh). Does not mutate store.</summary>
    public static List<JumpItem> CollectFor(string name)
    {
        var list = new List<JumpItem>();
        if (string.IsNullOrEmpty(name))
            return list;
        try
        {
            if (string.Equals(name, "VS Code", StringComparison.OrdinalIgnoreCase))
                return VsCodeFolders() ?? list;
            if (string.Equals(name, "IntelliJ IDEA", StringComparison.OrdinalIgnoreCase))
                return IntelliJProjects() ?? list;
            if (string.Equals(name, "Notepad++", StringComparison.OrdinalIgnoreCase))
                return NotepadFiles() ?? list;
            if (string.Equals(name, "Chrome", StringComparison.OrdinalIgnoreCase))
                return ChromeUrls() ?? list;
            if (string.Equals(name, "File Explorer", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "Windows Explorer", StringComparison.OrdinalIgnoreCase))
            {
                var a = ExplorerItems() ?? new List<JumpItem>();
                var b = RecentFolderLnks() ?? new List<JumpItem>();
                a.AddRange(b);
                return a;
            }
            if (string.Equals(name, ChromeAppUrl.GitHubPr, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, ChromeAppUrl.Jira, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, ChromeAppUrl.Confluence, StringComparison.OrdinalIgnoreCase))
            {
                return CollectChromeApp(name) ?? list;
            }
            JumpProviderRegistry.EnsureBuilt();
            // Providers
            if (string.Equals(name, "GitHub Desktop", StringComparison.OrdinalIgnoreCase))
                return ToListSafe(new GitHubDesktopJumpProvider().Collect());
            if (string.Equals(name, "MongoDB Compass", StringComparison.OrdinalIgnoreCase))
                return ToListSafe(new MongoCompassJumpProvider().Collect());
            if (string.Equals(name, "Lens", StringComparison.OrdinalIgnoreCase))
                return ToListSafe(new LensJumpProvider().Collect());
            if (string.Equals(name, "Termius", StringComparison.OrdinalIgnoreCase))
                return ToListSafe(new TermiusJumpProvider().Collect());
        }
        catch { }
        return list;
    }

    private static List<JumpItem> CollectChromeApp(string name)
    {
        var hist = ChromeHistory.TryRead();
        Dictionary<string, List<JumpItem>> lists = null;
        if (hist != null && hist.Ok)
            lists = hist.Lists;
        else
            lists = ClassifyFromChromeFiles();
        if (lists == null)
            return new List<JumpItem>();
        List<JumpItem> items;
        if (!lists.TryGetValue(name, out items) || items == null)
            return new List<JumpItem>();
        return items;
    }

    private static List<JumpItem> ToListSafe(IList<JumpItem> items)
    {
        var list = new List<JumpItem>();
        if (items == null)
            return list;
        for (int i = 0; i < items.Count; i++)
            list.Add(items[i]);
        return list;
    }

    private static void MergeChromeApps(JumpListStore store)
    {
        var hist = ChromeHistory.TryRead();
        Dictionary<string, List<JumpItem>> lists = null;
        if (hist != null && hist.Ok)
            lists = hist.Lists;
        else
        {
            lists = ClassifyFromChromeFiles();
            if (lists == null)
                return;
        }
        Replace(store, ChromeAppUrl.GitHubPr, lists);
        Replace(store, ChromeAppUrl.Jira, lists);
        Replace(store, ChromeAppUrl.Confluence, lists);
    }

    private static void Replace(JumpListStore store, string name, Dictionary<string, List<JumpItem>> lists)
    {
        List<JumpItem> items;
        if (lists == null || !lists.TryGetValue(name, out items))
            items = new List<JumpItem>();
        store.ReplaceRecents(name, items);
    }

    private static Dictionary<string, List<JumpItem>> ClassifyFromChromeFiles()
    {
        string home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"Google\Chrome\User Data\Default");
        if (!Directory.Exists(home))
            return null;
        var raw = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectChromeFileUrls(raw, seen, Path.Combine(home, "Bookmarks"));
        CollectChromeBinUrls(raw, seen, Path.Combine(home, "Top Sites"));
        CollectChromeBinUrls(raw, seen, Path.Combine(home, "Shortcuts"));
        var lists = new Dictionary<string, List<JumpItem>>(StringComparer.OrdinalIgnoreCase);
        lists[ChromeAppUrl.GitHubPr] = new List<JumpItem>();
        lists[ChromeAppUrl.Jira] = new List<JumpItem>();
        lists[ChromeAppUrl.Confluence] = new List<JumpItem>();
        foreach (var url in raw)
        {
            string dock = ChromeAppUrl.Classify(url);
            if (dock == null)
                continue;
            string canon = ChromeAppUrl.Canonical(url);
            List<JumpItem> list;
            if (!lists.TryGetValue(dock, out list) || list.Count >= ChromeHistory.CapPerApp)
                continue;
            bool dup = false;
            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i].target, canon, StringComparison.OrdinalIgnoreCase))
                {
                    dup = true;
                    break;
                }
            }
            if (dup)
                continue;
            list.Add(new JumpItem
            {
                kind = "url",
                title = ChromeAppUrl.Title(canon, null),
                target = canon
            });
        }
        return lists;
    }

    private static void CollectChromeFileUrls(List<string> raw, HashSet<string> seen, string path)
    {
        string json = ReadText(path);
        if (string.IsNullOrEmpty(json))
            return;
        foreach (Match m in Regex.Matches(json, "\"url\"\\s*:\\s*\"(https?://[^\"]+)\"", RegexOptions.IgnoreCase))
        {
            string url = JumpStrings.CleanUrl(m.Groups[1].Value);
            if (url != null && seen.Add(url))
                raw.Add(url);
        }
    }

    private static void CollectChromeBinUrls(List<string> raw, HashSet<string> seen, string path)
    {
        if (!File.Exists(path))
            return;
        byte[] buf;
        try { buf = File.ReadAllBytes(path); }
        catch { return; }
        foreach (var url in JumpStrings.HttpUrls(buf))
        {
            if (url != null && seen.Add(url))
                raw.Add(url);
        }
    }

    private static void Merge(JumpListStore store, string name, List<JumpItem> items)
    {
        if (items != null && items.Count > 0)
            store.MergeRecent(name, items);
    }

    private static List<JumpItem> VsCodeFolders()
    {
        var list = new List<JumpItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string json = ReadText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"Code\User\globalStorage\storage.json"));
        if (string.IsNullOrEmpty(json))
            return list;
        foreach (Match m in Regex.Matches(json, "file:///[A-Za-z0-9%._\\-~/]+", RegexOptions.IgnoreCase))
        {
            string path = FileUriToPath(m.Value.TrimEnd('\\', '"'));
            AddPath(list, seen, path, 40);
        }
        return list;
    }

    private static List<JumpItem> IntelliJProjects()
    {
        var list = new List<JumpItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JetBrains");
        if (!Directory.Exists(root))
            return list;
        foreach (var file in Directory.GetFiles(root, "recentProjects.xml", SearchOption.AllDirectories))
        {
            if (file.IndexOf("IntelliJIdea", StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            string xml = ReadText(file);
            if (string.IsNullOrEmpty(xml))
                continue;
            foreach (Match m in Regex.Matches(xml, "<entry key=\"([^\"]+)\""))
            {
                string path = ExpandIdeaPath(m.Groups[1].Value);
                AddPath(list, seen, path, 60);
            }
        }
        return list;
    }

    private static List<JumpItem> NotepadFiles()
    {
        var list = new List<JumpItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Notepad++");
        AddFilesFromXml(list, seen, Path.Combine(dir, "config.xml"));
        AddFilesFromXml(list, seen, Path.Combine(dir, "session.xml"));
        return list;
    }

    private static void AddFilesFromXml(List<JumpItem> list, HashSet<string> seen, string xmlPath)
    {
        string xml = ReadText(xmlPath);
        if (string.IsNullOrEmpty(xml))
            return;
        foreach (Match m in Regex.Matches(xml, "filename=\"([^\"]+)\""))
        {
            string path = m.Groups[1].Value;
            if (path.IndexOf(':') < 0)
                continue;
            AddPath(list, seen, path, 40);
        }
    }

    private static List<JumpItem> ChromeUrls()
    {
        var list = new List<JumpItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"Google\Chrome\User Data\Default");
        AddChromeUrlsFromText(list, seen, Path.Combine(home, "Bookmarks"));
        ScanChromeBinary(list, seen, Path.Combine(home, "Top Sites"));
        ScanChromeBinary(list, seen, Path.Combine(home, "Shortcuts"));
        if (list.Count > 40)
            list.RemoveRange(40, list.Count - 40);
        return list;
    }

    private static void AddChromeUrlsFromText(List<JumpItem> list, HashSet<string> seen, string path)
    {
        string json = ReadText(path);
        if (string.IsNullOrEmpty(json))
            return;
        foreach (Match m in Regex.Matches(json, "\"url\"\\s*:\\s*\"(https?://[^\"]+)\"", RegexOptions.IgnoreCase))
            AddUrl(list, seen, m.Groups[1].Value, 50);
    }

    private static void ScanChromeBinary(List<JumpItem> list, HashSet<string> seen, string path)
    {
        if (!File.Exists(path) || list.Count >= 50)
            return;
        byte[] buf;
        try { buf = File.ReadAllBytes(path); }
        catch { return; }
        foreach (var url in JumpStrings.HttpUrls(buf))
            AddUrl(list, seen, url, 50);
    }

    private static List<JumpItem> ExplorerItems()
    {
        var list = new List<JumpItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string desk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string downs = Path.Combine(home, "Downloads");
        AddPath(list, seen, docs, 20);
        AddPath(list, seen, desk, 20);
        AddPath(list, seen, downs, 20);
        AddPath(list, seen, Path.Combine(docs, "FergMain"), 20);
        AddPath(list, seen, Path.Combine(docs, "Docs", "Guides"), 20);
        return list;
    }

    private static List<JumpItem> RecentFolderLnks()
    {
        var list = new List<JumpItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"Microsoft\Windows\Recent");
        if (!Directory.Exists(dir))
            return list;
        Type shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType == null)
            return list;
        object shell = Activator.CreateInstance(shellType);
        int n = 0;
        foreach (var file in Directory.GetFiles(dir, "*.lnk"))
        {
            if (n > 80)
                break;
            n++;
            try
            {
                var sc = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { file });
                var st = sc.GetType();
                string target = st.InvokeMember("TargetPath", BindingFlags.GetProperty, null, sc, null) as string;
                if (string.IsNullOrEmpty(target))
                    continue;
                if (Directory.Exists(target))
                    AddPath(list, seen, target, 40);
            }
            catch { }
        }
        return list;
    }

    private static void AddPath(List<JumpItem> list, HashSet<string> seen, string path, int cap)
    {
        if (list.Count >= cap || string.IsNullOrEmpty(path))
            return;
        path = path.Trim().Trim('"');
        if (!File.Exists(path) && !Directory.Exists(path))
            return;
        string key;
        try { key = Path.GetFullPath(path); }
        catch { key = path; }
        if (!seen.Add(key))
            return;
        bool dir = Directory.Exists(key);
        list.Add(new JumpItem
        {
            kind = dir ? "folder" : "file",
            title = JumpLaunch.DisplayNameFromTarget(dir ? "folder" : "file", key),
            target = key
        });
    }

    private static void AddUrl(List<JumpItem> list, HashSet<string> seen, string url, int cap)
    {
        url = JumpStrings.CleanUrl(url);
        if (url == null || list.Count >= cap)
            return;
        if (ChromeAppUrl.Classify(url) != null)
            return;
        if (!seen.Add(url))
            return;
        list.Add(new JumpItem
        {
            kind = "url",
            title = JumpLaunch.DisplayNameFromTarget("url", url),
            target = url
        });
    }

    private static string ExpandIdeaPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return path.Replace("$USER_HOME$", home).Replace('/', Path.DirectorySeparatorChar);
    }

    private static string FileUriToPath(string uri)
    {
        try
        {
            if (string.IsNullOrEmpty(uri))
                return null;
            uri = Uri.UnescapeDataString(uri.Replace("\\/", "/"));
            if (uri.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
            {
                string rest = uri.Substring(8).Replace('/', Path.DirectorySeparatorChar);
                return rest;
            }
            return new Uri(uri).LocalPath;
        }
        catch
        {
            return null;
        }
    }

    private static string ReadText(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;
            return File.ReadAllText(path);
        }
        catch
        {
            return null;
        }
    }
}

internal static class JumpStrings
{
    public static string CleanUrl(string s)
    {
        if (string.IsNullOrEmpty(s))
            return null;
        s = s.Trim();
        int n = 0;
        while (n < s.Length && IsUrlChar(s[n]))
            n++;
        if (n < 12)
            return null;
        s = s.Substring(0, n);
        while (s.Length > 0 && "./,;)]}>\"'".IndexOf(s[s.Length - 1]) >= 0)
            s = s.Substring(0, s.Length - 1);
        Uri u;
        if (!Uri.TryCreate(s, UriKind.Absolute, out u))
            return null;
        if (u.Scheme != "http" && u.Scheme != "https")
            return null;
        if (string.IsNullOrEmpty(u.Host) || u.Host.IndexOf('.') < 0)
            return null;
        if (u.Host.IndexOf("google.com", StringComparison.OrdinalIgnoreCase) >= 0 &&
            u.AbsolutePath.IndexOf("/chrome", StringComparison.OrdinalIgnoreCase) >= 0)
            return null;
        return s;
    }

    public static List<string> HttpUrls(byte[] buf)
    {
        var list = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (buf == null)
            return list;
        for (int i = 0; i < buf.Length - 12; i++)
        {
            if (buf[i] == (byte)'h' && buf[i + 1] == (byte)'t' && buf[i + 2] == (byte)'t' && buf[i + 3] == (byte)'p')
            {
                int j = i;
                while (j < buf.Length && buf[j] >= 32 && buf[j] < 127)
                    j++;
                string raw = Encoding.ASCII.GetString(buf, i, j - i);
                string url = CleanUrl(raw);
                if (url != null && seen.Add(url))
                    list.Add(url);
                i = j;
            }
            else if (buf[i] == (byte)'h' && i + 1 < buf.Length && buf[i + 1] == 0 &&
                     i + 14 < buf.Length && buf[i + 2] == (byte)'t' && buf[i + 3] == 0)
            {
                var sb = new StringBuilder();
                int j = i;
                while (j + 1 < buf.Length)
                {
                    char c = (char)(buf[j] | (buf[j + 1] << 8));
                    if (c < 32 || c > 126)
                        break;
                    sb.Append(c);
                    j += 2;
                }
                string url = CleanUrl(sb.ToString());
                if (url != null && seen.Add(url))
                    list.Add(url);
                i = j;
            }
        }
        return list;
    }

    public static List<string> WinPaths(byte[] buf)
    {
        var list = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (buf == null)
            return list;
        for (int i = 0; i + 6 < buf.Length; i += 2)
        {
            if (buf[i] < (byte)'A' || buf[i] > (byte)'Z' || buf[i + 1] != 0)
                continue;
            if (i + 5 >= buf.Length || buf[i + 2] != (byte)':' || buf[i + 3] != 0)
                continue;
            if (buf[i + 4] != (byte)'\\' || buf[i + 5] != 0)
                continue;
            var sb = new StringBuilder();
            int j = i;
            while (j + 1 < buf.Length)
            {
                char c = (char)(buf[j] | (buf[j + 1] << 8));
                if (c < 32 || c == '"' || c == '\0')
                    break;
                sb.Append(c);
                j += 2;
            }
            string path = sb.ToString().TrimEnd('\\');
            if (path.Length < 4 || !seen.Add(path))
            {
                i = j;
                continue;
            }
            try
            {
                if (Directory.Exists(path) || File.Exists(path))
                    list.Add(path);
            }
            catch { }
            i = j;
        }
        return list;
    }

    private static bool IsUrlChar(char c)
    {
        if (c >= 'A' && c <= 'Z') return true;
        if (c >= 'a' && c <= 'z') return true;
        if (c >= '0' && c <= '9') return true;
        return "-._~:/?#[]@!$&'()*+,;=%".IndexOf(c) >= 0;
    }
}
