// JumpDock — config directory and one-time migrate from WorkApps.
using System;
using System.IO;
using System.Windows.Forms;

public static class JumpPaths
{
    public const string AppFolderName = "JumpDock";
    public const string LegacyFolderName = "WorkApps";

    public static string ConfigDir
    {
        get
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppFolderName);
        }
    }

    public static string LegacyConfigDir
    {
        get
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                LegacyFolderName);
        }
    }

    public static string DockJsonPath
    {
        get { return Path.Combine(ConfigDir, "dock.json"); }
    }

    public static string JumpListsPath
    {
        get { return Path.Combine(ConfigDir, "jumplists.json"); }
    }

    /// <summary>
    /// If JumpDock has no dock.json yet, copy config/icons/lnks from WorkApps (or C:\WorkApps).
    /// Always fills missing tile icons from the bundled repo/install icons folder.
    /// </summary>
    public static void MigrateFromWorkAppsIfNeeded()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            if (!File.Exists(DockJsonPath))
            {
                string src = ResolveLegacySource();
                if (!string.IsNullOrEmpty(src) && Directory.Exists(src) &&
                    File.Exists(Path.Combine(src, "dock.json")))
                {
                    CopyIfExists(Path.Combine(src, "dock.json"), DockJsonPath);
                    CopyIfExists(Path.Combine(src, "jumplists.json"), JumpListsPath);
                    RewriteLegacyPathsInFile(DockJsonPath);

                    string srcIcons = Path.Combine(src, "icons");
                    if (Directory.Exists(srcIcons))
                        CopyMissingFiles(srcIcons, Path.Combine(ConfigDir, "icons"));

                    foreach (string lnk in Directory.GetFiles(src, "*.lnk"))
                    {
                        string name = Path.GetFileName(lnk);
                        if (string.IsNullOrEmpty(name))
                            continue;
                        string dest = Path.Combine(ConfigDir, name);
                        if (!File.Exists(dest))
                            File.Copy(lnk, dest, false);
                    }
                }
            }

            EnsureBundledIcons();
        }
        catch { }
    }

    /// <summary>
    /// Copy any missing icons from install/repo icons into %LOCALAPPDATA%\JumpDock\icons.
    /// Never overwrites icons the user already has.
    /// </summary>
    public static void EnsureBundledIcons()
    {
        try
        {
            string dst = Path.Combine(ConfigDir, "icons");
            Directory.CreateDirectory(dst);
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates = new string[]
            {
                Path.Combine(exeDir, "icons"),
                Path.Combine(exeDir, "..", "icons")
            };
            for (int i = 0; i < candidates.Length; i++)
            {
                string repoIcons = candidates[i];
                try { repoIcons = Path.GetFullPath(repoIcons); }
                catch { continue; }
                if (!Directory.Exists(repoIcons))
                    continue;
                CopyMissingFiles(repoIcons, dst);
            }
        }
        catch { }
    }

    private static void CopyMissingFiles(string srcDir, string dstDir)
    {
        Directory.CreateDirectory(dstDir);
        foreach (string file in Directory.GetFiles(srcDir))
        {
            string name = Path.GetFileName(file);
            if (string.IsNullOrEmpty(name))
                continue;
            string dest = Path.Combine(dstDir, name);
            if (!File.Exists(dest))
                File.Copy(file, dest, false);
        }
    }

    private static string ResolveLegacySource()
    {
        string local = LegacyConfigDir;
        if (File.Exists(Path.Combine(local, "dock.json")))
            return local;
        if (File.Exists(@"C:\WorkApps\dock.json"))
            return @"C:\WorkApps";
        return local;
    }

    private static void CopyIfExists(string src, string dst)
    {
        if (!File.Exists(src))
            return;
        string dir = Path.GetDirectoryName(dst);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        File.Copy(src, dst, false);
    }

    private static void RewriteLegacyPathsInFile(string path)
    {
        if (!File.Exists(path))
            return;
        try
        {
            string text = File.ReadAllText(path);
            string next = text.Replace("%LOCALAPPDATA%\\WorkApps", "%LOCALAPPDATA%\\JumpDock");
            next = next.Replace("%LOCALAPPDATA%/WorkApps", "%LOCALAPPDATA%/JumpDock");
            next = next.Replace(@"\WorkApps\", @"\JumpDock\");
            if (next != text)
                File.WriteAllText(path, next);
        }
        catch { }
    }

    /// <summary>Copy launcher next to config when running from repo bin\.</summary>
    public static void EnsureLauncherBesideConfig()
    {
        try
        {
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string srcLauncher = Path.Combine(exeDir, "JumpDockLauncher.exe");
            if (!File.Exists(srcLauncher))
                return;
            Directory.CreateDirectory(ConfigDir);
            string dst = Path.Combine(ConfigDir, "JumpDockLauncher.exe");
            File.Copy(srcLauncher, dst, true);
        }
        catch { }
    }
}
