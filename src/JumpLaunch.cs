// JumpDock.
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;

public enum DockKind
{
    ChromeApp,
    Browser,
    FolderApp,
    FileApp,
    App
}

public static class JumpLaunch
{
    public static DockKind InferKind(string name, string lnk)
    {
        var n = name ?? "";
        var p = (lnk ?? "").ToLowerInvariant();
        if (string.Equals(n, "GitHub PR", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(n, "Jira", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(n, "Confluence", StringComparison.OrdinalIgnoreCase) ||
            p.IndexOf("workappslauncher", StringComparison.OrdinalIgnoreCase) >= 0)
            return DockKind.ChromeApp;
        if (string.Equals(n, "Chrome", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(n, "Edge", StringComparison.OrdinalIgnoreCase) ||
            p.EndsWith("chrome.exe") || p.IndexOf("msedge", StringComparison.OrdinalIgnoreCase) >= 0)
            return DockKind.Browser;
        if (string.Equals(n, "Cursor", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(n, "VS Code", StringComparison.OrdinalIgnoreCase) ||
            n.IndexOf("IntelliJ", StringComparison.OrdinalIgnoreCase) >= 0 ||
            string.Equals(n, "File Explorer", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(n, "Windows Explorer", StringComparison.OrdinalIgnoreCase) ||
            p.IndexOf("explorer.exe", StringComparison.OrdinalIgnoreCase) >= 0)
            return DockKind.FolderApp;
        if (string.Equals(n, "Teams", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(n, "Company Portal", StringComparison.OrdinalIgnoreCase))
            return DockKind.App;
        return DockKind.FileApp;
    }

    public static string KindHint(DockKind kind)
    {
        switch (kind)
        {
            case DockKind.ChromeApp:
                return "Links open in this app window.";
            case DockKind.Browser:
                return "Links open in this browser.";
            case DockKind.FolderApp:
                return "Folders and files open in this app.";
            case DockKind.FileApp:
                return "Files and folders open in this app.";
            default:
                return "Open the app from here.";
        }
    }

    public static bool AllowsFolder(DockKind kind)
    {
        return kind == DockKind.FolderApp || kind == DockKind.FileApp;
    }

    public static bool AllowsFile(DockKind kind)
    {
        return kind == DockKind.FolderApp || kind == DockKind.FileApp;
    }

    public static bool AllowsLink(DockKind kind)
    {
        return kind == DockKind.ChromeApp || kind == DockKind.Browser;
    }

    public static JumpItem FromFolder(string path)
    {
        path = path.Trim().Trim('"');
        return new JumpItem
        {
            kind = "folder",
            title = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            target = path
        };
    }

    public static JumpItem FromFile(string path)
    {
        path = path.Trim().Trim('"');
        return new JumpItem
        {
            kind = "file",
            title = Path.GetFileName(path),
            target = path
        };
    }

    public static JumpItem FromUrl(string url)
    {
        url = (url ?? "").Trim();
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            url = "https://" + url;
        return new JumpItem { kind = "url", title = DisplayNameFromTarget("url", url), target = url };
    }

    public static string DisplayName(JumpItem item)
    {
        if (item == null)
            return "";
        if (ChromeAppUrl.HasStoredLabel(item.title, item.target))
            return item.title.Trim();
        if (item.target != null && item.target.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return ChromeAppUrl.Title(item.target, item.title);
        return DisplayNameFromTarget(item.kind, item.target);
    }

    public static string DisplaySub(JumpItem item)
    {
        if (item == null || string.IsNullOrEmpty(item.target))
            return "";
        return item.target.Trim();
    }

    public static string DisplayNameFromTarget(string kind, string target)
    {
        if (string.IsNullOrEmpty(target))
            return "";
        var t = target.Trim();
        if (string.Equals(kind, "url", StringComparison.OrdinalIgnoreCase) || t.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            if (ChromeAppUrl.Classify(t) != null)
                return ChromeAppUrl.Title(t, null);
            try
            {
                var u = new Uri(t);
                var segs = u.AbsolutePath.Trim('/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (segs.Length > 0)
                    return Uri.UnescapeDataString(segs[segs.Length - 1]);
                return u.Host;
            }
            catch
            {
                return t;
            }
        }
        try
        {
            var p = t.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var name = Path.GetFileName(p);
            return string.IsNullOrEmpty(name) ? p : name;
        }
        catch
        {
            return t;
        }
    }

    public static bool TargetLooksOk(JumpItem item)
    {
        if (item == null || string.IsNullOrEmpty(item.target))
            return false;
        if (string.Equals(item.kind, "url", StringComparison.OrdinalIgnoreCase) ||
            item.target.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return true;
        try
        {
            return File.Exists(item.target) || Directory.Exists(item.target);
        }
        catch
        {
            return false;
        }
    }

    public static bool OpenApp(string lnk)
    {
        if (string.IsNullOrEmpty(lnk) || !File.Exists(lnk))
            return false;
        Process.Start(new ProcessStartInfo { FileName = lnk, UseShellExecute = true });
        return true;
    }

    public static bool OpenItem(string name, string lnk, string icon, JumpItem item)
    {
        if (item == null || string.IsNullOrEmpty(item.target))
            return false;
        var kind = InferKind(name, lnk);
        string target = item.target.Trim();
        if (string.Equals(name, "MongoDB Compass", StringComparison.OrdinalIgnoreCase) &&
            target.StartsWith("mongodb", StringComparison.OrdinalIgnoreCase))
            return OpenWithExeArg(lnk, target);
        if (string.Equals(name, "Termius", StringComparison.OrdinalIgnoreCase))
            return OpenApp(lnk);
        if (string.Equals(name, "Lens", StringComparison.OrdinalIgnoreCase) &&
            !Directory.Exists(target) &&
            !(File.Exists(target) && (target.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) ||
                                      target.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
                                      string.Equals(Path.GetFileName(target), "config", StringComparison.OrdinalIgnoreCase))))
            return OpenApp(lnk);
        if (string.Equals(item.kind, "url", StringComparison.OrdinalIgnoreCase) ||
            target.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return OpenUrl(kind, name, lnk, icon, target);
        return OpenPath(kind, lnk, target);
    }

    private static bool OpenWithExeArg(string lnk, string arg)
    {
        string exe = ResolveExe(lnk);
        if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
            return OpenApp(lnk);
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                Arguments = Quote(arg),
                UseShellExecute = false
            });
            return true;
        }
        catch
        {
            return OpenApp(lnk);
        }
    }

    private static bool OpenUrl(DockKind kind, string name, string lnk, string icon, string url)
    {
        if (kind == DockKind.ChromeApp)
            return OpenChromeApp(lnk, url);
        if (kind == DockKind.Browser)
            return OpenBrowser(lnk, url);
        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        return true;
    }

    private static bool OpenChromeApp(string lnk, string url)
    {
        try
        {
            string launcher = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JumpDock", "JumpDockLauncher.exe");
            string args = "";
            string target = launcher;
            string workDir = Path.GetDirectoryName(launcher);
            if (!string.IsNullOrEmpty(lnk) && File.Exists(lnk) &&
                string.Equals(Path.GetExtension(lnk), ".lnk", StringComparison.OrdinalIgnoreCase))
            {
                string t, a, d;
                if (ReadShortcut(lnk, out t, out a, out d))
                {
                    if (!string.IsNullOrEmpty(t) && File.Exists(t))
                        target = t;
                    args = a ?? "";
                    if (!string.IsNullOrEmpty(d))
                        workDir = d;
                }
            }
            if (File.Exists(target))
            {
                args = SetFlag(args, "--url", url);
                args = StripFlag(args, "--title-match");
                if (string.IsNullOrEmpty(GetFlag(args, "--name")))
                    args = SetFlag(args, "--name", "WorkApp");
                if (string.IsNullOrEmpty(GetFlag(args, "--aumid")))
                    args = SetFlag(args, "--aumid", "Ferguson.JumpDock.Pinned");
                Process.Start(new ProcessStartInfo
                {
                    FileName = target,
                    Arguments = args,
                    WorkingDirectory = workDir ?? "",
                    UseShellExecute = false
                });
                return true;
            }
            return OpenChromeAppDirect(url);
        }
        catch
        {
            try { return OpenChromeAppDirect(url); }
            catch { return false; }
        }
    }

    private static bool OpenChromeAppDirect(string url)
    {
        string chrome = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
        if (!File.Exists(chrome) || string.IsNullOrEmpty(url))
            return false;
        Process.Start(new ProcessStartInfo
        {
            FileName = chrome,
            Arguments = "--profile-directory=Default --start-maximized --app=" + Quote(url),
            UseShellExecute = false
        });
        return true;
    }

    private static bool OpenBrowser(string lnk, string url)
    {
        string exe;
        if (!string.IsNullOrEmpty(lnk) && lnk.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(lnk))
            exe = lnk;
        else
            exe = ResolveExe(lnk);
        if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            return true;
        }
        Process.Start(new ProcessStartInfo
        {
            FileName = exe,
            Arguments = "--profile-directory=Default " + Quote(url),
            UseShellExecute = false
        });
        return true;
    }

    private static bool OpenPath(DockKind kind, string lnk, string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
            return false;
        string exe = ResolveExe(lnk);
        if (kind == DockKind.FolderApp || kind == DockKind.FileApp)
        {
            if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = Quote(path),
                    UseShellExecute = false
                });
                return true;
            }
        }
        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        return true;
    }

    public static string ResolveExe(string lnk)
    {
        if (string.IsNullOrEmpty(lnk))
            return null;
        if (lnk.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(lnk))
            return lnk;
        if (string.Equals(Path.GetExtension(lnk), ".lnk", StringComparison.OrdinalIgnoreCase) && File.Exists(lnk))
        {
            string t, a, d;
            if (ReadShortcut(lnk, out t, out a, out d) && !string.IsNullOrEmpty(t) && File.Exists(t))
                return t;
        }
        return File.Exists(lnk) ? lnk : null;
    }

    public static bool ReadShortcut(string lnk, out string target, out string args, out string workDir)
    {
        target = null;
        args = null;
        workDir = null;
        try
        {
            var t = Type.GetTypeFromProgID("WScript.Shell");
            if (t == null)
                return false;
            var shell = Activator.CreateInstance(t);
            var sc = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
            var st = sc.GetType();
            target = st.InvokeMember("TargetPath", BindingFlags.GetProperty, null, sc, null) as string;
            args = st.InvokeMember("Arguments", BindingFlags.GetProperty, null, sc, null) as string;
            workDir = st.InvokeMember("WorkingDirectory", BindingFlags.GetProperty, null, sc, null) as string;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string Quote(string s)
    {
        if (string.IsNullOrEmpty(s))
            return "\"\"";
        if (s.IndexOf(' ') < 0 && s.IndexOf('\t') < 0)
            return s;
        return "\"" + s.Replace("\"", "\\\"") + "\"";
    }

    private static string GetFlag(string args, string flag)
    {
        if (string.IsNullOrEmpty(args))
            return null;
        var m = Regex.Match(args, Regex.Escape(flag) + "\\s+(\"[^\"]*\"|\\S+)");
        if (!m.Success)
            return null;
        var v = m.Groups[1].Value;
        if (v.Length >= 2 && v[0] == '"')
            v = v.Substring(1, v.Length - 2);
        return v;
    }

    private static string SetFlag(string args, string flag, string value)
    {
        args = args ?? "";
        var quoted = "\"" + (value ?? "").Replace("\"", "\\\"") + "\"";
        var pattern = Regex.Escape(flag) + "\\s+(\"[^\"]*\"|\\S+)";
        if (Regex.IsMatch(args, pattern))
            return Regex.Replace(args, pattern, flag + " " + quoted);
        if (args.Length > 0 && !char.IsWhiteSpace(args[args.Length - 1]))
            args += " ";
        return args + flag + " " + quoted;
    }

    private static string StripFlag(string args, string flag)
    {
        if (string.IsNullOrEmpty(args))
            return args;
        return Regex.Replace(args, "\\s*" + Regex.Escape(flag) + "\\s+(\"[^\"]*\"|\\S+)", "").Trim();
    }
}
