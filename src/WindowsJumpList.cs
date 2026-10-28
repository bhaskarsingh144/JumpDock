// JumpDock.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text.RegularExpressions;
using STATSTG = System.Runtime.InteropServices.ComTypes.STATSTG;

public static class WindowsJumpList
{
    [DllImport("ole32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int StgOpenStorage(string pwcsName, IntPtr pstgPriority, uint grfMode, IntPtr snbExclude, uint reserved, out IStorage ppstgOpen);

    [ComImport, Guid("0000000b-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IStorage
    {
        void CreateStream(string n, uint m, uint a, uint b, out IStream s);
        void OpenStream(string n, IntPtr r1, uint m, uint r2, out IStream s);
        void CreateStorage(string n, uint m, uint a, uint b, out IStorage s);
        void OpenStorage(string n, IStorage p, uint m, IntPtr snb, uint r, out IStorage s);
        void CopyTo(uint c, IntPtr i, IntPtr snb, IStorage d);
        void MoveElementTo(string n, IStorage d, string nn, uint f);
        void Commit(uint f);
        void Revert();
        void EnumElements(uint a, IntPtr b, uint c, out IEnumSTATSTG e);
        void DestroyElement(string n);
        void RenameElement(string o, string nn);
        void SetElementTimes(string n, IntPtr c, IntPtr a, IntPtr m);
        void SetClass(ref Guid clsid);
        void SetStateBits(uint b, uint mask);
        void Stat(out STATSTG s, uint f);
    }

    [ComImport, Guid("0000000d-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IEnumSTATSTG
    {
        [PreserveSig]
        int Next(uint celt, out STATSTG rgelt, out uint pceltFetched);
        void Skip(uint celt);
        void Reset();
        void Clone(out IEnumSTATSTG ppenum);
    }

    private const uint STGM_READ_EXCLUSIVE = 0x00000010;

    public static void Sync(JumpListStore store, List<DockItem> dockItems)
    {
        Sync(store, dockItems, null);
    }

    public static void Sync(JumpListStore store, List<DockItem> dockItems, DockConfig cfg)
    {
        if (store == null || dockItems == null)
            return;
        var buckets = HarvestAll(dockItems);
        foreach (var kv in buckets)
            store.MergeRecent(kv.Key, kv.Value);
        // Do not ClearRecent("Chrome") before AppRecents — an empty harvest used to
        // wipe pins/recents on every Reload. AppRecents.ReplaceRecents only applies
        // when CollectFor returns items.
        try { AppRecents.Sync(store, cfg); }
        catch { }
        store.TrimRecent("Chrome", 40);
        store.TrimRecent("File Explorer", 40);
        store.TrimRecent("VS Code", 40);
        store.TrimRecent("Notepad++", 40);
        store.TrimRecent("IntelliJ IDEA", 60);
        store.TrimRecent("GitHub Desktop", 40);
        store.TrimRecent("MongoDB Compass", 40);
        store.TrimRecent("Lens", 40);
        store.TrimRecent("Termius", 40);
        store.TrimRecent("Cursor", 40);
    }

    /// <summary>
    /// Collect Windows destination items for one dock tile (Cursor, etc.).
    /// Does not mutate the store. Empty list on failure — callers must not wipe.
    /// </summary>
    public static List<JumpItem> CollectFor(string dockName)
    {
        var list = new List<JumpItem>();
        if (string.IsNullOrEmpty(dockName))
            return list;
        var items = new List<DockItem>();
        items.Add(new DockItem { name = dockName });
        var buckets = HarvestAll(items);
        List<JumpItem> found;
        if (buckets.TryGetValue(dockName, out found) && found != null)
        {
            for (int i = 0; i < found.Count; i++)
                list.Add(found[i]);
        }
        return list;
    }

    private static Dictionary<string, List<JumpItem>> HarvestAll(List<DockItem> dockItems)
    {
        var buckets = new Dictionary<string, List<JumpItem>>(StringComparer.OrdinalIgnoreCase);
        if (dockItems != null)
        {
            foreach (var it in dockItems)
            {
                if (it != null && !string.IsNullOrEmpty(it.name) && !buckets.ContainsKey(it.name))
                    buckets[it.name] = new List<JumpItem>();
            }
        }
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"Microsoft\Windows\Recent\AutomaticDestinations");
        if (Directory.Exists(dir))
        {
            foreach (var file in Directory.GetFiles(dir, "*.automaticDestinations-ms"))
            {
                try { HarvestFile(file, buckets); }
                catch { }
            }
        }
        var custom = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"Microsoft\Windows\Recent\CustomDestinations");
        if (Directory.Exists(custom))
        {
            foreach (var file in Directory.GetFiles(custom, "*.customDestinations-ms"))
            {
                try { HarvestRaw(file, buckets); }
                catch { }
            }
        }
        return buckets;
    }

    private static void HarvestRaw(string path, Dictionary<string, List<JumpItem>> buckets)
    {
        byte[] buf;
        try { buf = File.ReadAllBytes(path); }
        catch { return; }
        string name = Path.GetFileName(path) ?? "";
        bool explorer = name.StartsWith("f01b4d95cf55d32a", StringComparison.OrdinalIgnoreCase);
        foreach (var url in JumpStrings.HttpUrls(buf))
        {
            if (name.IndexOf("chrome", StringComparison.OrdinalIgnoreCase) >= 0)
                AddTo(buckets, "Chrome", "url", url);
        }
        if (explorer)
        {
            foreach (var p in JumpStrings.WinPaths(buf))
                AddTo(buckets, "File Explorer", Directory.Exists(p) ? "folder" : "file", p);
        }
    }

    private static void AddTo(Dictionary<string, List<JumpItem>> buckets, string dock, string kind, string target)
    {
        List<JumpItem> list;
        if (!buckets.TryGetValue(dock, out list))
            return;
        if (string.Equals(kind, "url", StringComparison.OrdinalIgnoreCase))
        {
            target = JumpStrings.CleanUrl(target);
            if (target == null)
                return;
            string classified = ChromeAppUrl.Classify(target);
            if (classified != null)
            {
                dock = classified;
                target = ChromeAppUrl.Canonical(target);
            }
            else if (string.Equals(dock, "Chrome", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(dock, "Edge", StringComparison.OrdinalIgnoreCase))
            {
                /* stay on browser tile */
            }
            if (!buckets.TryGetValue(dock, out list))
                return;
            list.Add(new JumpItem
            {
                kind = "url",
                title = ChromeAppUrl.Classify(target) != null
                    ? ChromeAppUrl.Title(target, null)
                    : JumpLaunch.DisplayNameFromTarget("url", target),
                target = target
            });
            return;
        }
        if (!buckets.TryGetValue(dock, out list))
            return;
        list.Add(new JumpItem
        {
            kind = kind,
            title = JumpLaunch.DisplayNameFromTarget(kind, target),
            target = target
        });
    }

    private static void HarvestFile(string path, Dictionary<string, List<JumpItem>> buckets)
    {
        var copy = Path.Combine(Path.GetTempPath(), "wadest_" + Path.GetFileName(path));
        File.Copy(path, copy, true);
        IStorage stg;
        if (StgOpenStorage(copy, IntPtr.Zero, STGM_READ_EXCLUSIVE, IntPtr.Zero, 0, out stg) != 0)
            return;
        IEnumSTATSTG enm = null;
        try
        {
            stg.EnumElements(0, IntPtr.Zero, 0, out enm);
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
                return;
            var shell = Activator.CreateInstance(shellType);
            int taken = 0;
            int seen = 0;
            bool sawChrome = false;
            bool sawEdge = false;
            bool explorerFile = (Path.GetFileName(path) ?? "").StartsWith("f01b4d95cf55d32a", StringComparison.OrdinalIgnoreCase);
            var extraUrls = new List<string>();
            var extraPaths = new List<string>();
            while (taken < 120 && seen < 250)
            {
                STATSTG s;
                uint fetched;
                if (enm.Next(1, out s, out fetched) != 0 || fetched == 0)
                    break;
                IStream stm;
                try { stg.OpenStream(s.pwcsName, IntPtr.Zero, STGM_READ_EXCLUSIVE, 0, out stm); }
                catch { continue; }
                byte[] buf;
                try { buf = ReadStream(stm, (int)Math.Min(s.cbSize, 800000)); }
                finally { Marshal.ReleaseComObject(stm); }
                if (buf == null || buf.Length < 8)
                    continue;
                if (s.pwcsName == "DestList")
                {
                    extraUrls.AddRange(JumpStrings.HttpUrls(buf));
                    extraPaths.AddRange(JumpStrings.WinPaths(buf));
                    continue;
                }
                if (buf[0] != 0x4C)
                    continue;
                seen++;
                extraUrls.AddRange(JumpStrings.HttpUrls(buf));
                string target, args, desc;
                if (!ParseLnk(shellType, shell, buf, out target, out args, out desc))
                    continue;
                string dockName, itemKind, itemTarget, title;
                if (!MapItem(target, args, desc, out dockName, out itemKind, out itemTarget, out title))
                    continue;
                if (string.Equals(dockName, "Chrome", StringComparison.OrdinalIgnoreCase))
                    sawChrome = true;
                if (string.Equals(dockName, "Edge", StringComparison.OrdinalIgnoreCase))
                    sawEdge = true;
                List<JumpItem> list;
                if (!buckets.TryGetValue(dockName, out list))
                    continue;
                list.Add(new JumpItem { kind = itemKind, title = title, target = itemTarget });
                taken++;
            }
            string urlDock = null;
            if (sawChrome)
                urlDock = "Chrome";
            else if (sawEdge)
                urlDock = "Edge";
            if (explorerFile)
            {
                foreach (var pth in extraPaths)
                    AddTo(buckets, "File Explorer", Directory.Exists(pth) ? "folder" : "file", pth);
            }
            else if (urlDock != null)
            {
                foreach (var url in extraUrls)
                    AddTo(buckets, urlDock, "url", url);
            }
        }
        finally
        {
            if (enm != null) Marshal.ReleaseComObject(enm);
            Marshal.ReleaseComObject(stg);
            try { File.Delete(copy); } catch { }
        }
    }

    private static bool ParseLnk(Type shellType, object shell, byte[] buf, out string target, out string args, out string desc)
    {
        target = args = desc = "";
        var tmp = Path.Combine(Path.GetTempPath(), "jdjl_" + Guid.NewGuid().ToString("N") + ".lnk");
        try
        {
            File.WriteAllBytes(tmp, buf);
            var sc = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { tmp });
            var st = sc.GetType();
            target = st.InvokeMember("TargetPath", BindingFlags.GetProperty, null, sc, null) as string ?? "";
            args = st.InvokeMember("Arguments", BindingFlags.GetProperty, null, sc, null) as string ?? "";
            desc = st.InvokeMember("Description", BindingFlags.GetProperty, null, sc, null) as string ?? "";
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            try { File.Delete(tmp); } catch { }
        }
    }

    private static bool MapItem(string target, string args, string desc, out string dockName, out string kind, out string itemTarget, out string title)
    {
        dockName = kind = itemTarget = title = null;
        target = target ?? "";
        args = args ?? "";
        desc = desc ?? "";
        var exe = Path.GetFileName(target) ?? "";

        if (ContainsExe(exe, "idea64.exe") || ContainsExe(exe, "idea.exe"))
            dockName = "IntelliJ IDEA";
        else if (ContainsExe(exe, "Cursor.exe"))
            dockName = "Cursor";
        else if (ContainsExe(exe, "Code.exe"))
            dockName = "VS Code";
        else if (ContainsExe(exe, "chrome.exe"))
            dockName = "Chrome";
        else if (exe.IndexOf("msedge", StringComparison.OrdinalIgnoreCase) >= 0)
            dockName = "Edge";
        else if (exe.IndexOf("notepad++", StringComparison.OrdinalIgnoreCase) >= 0)
            dockName = "Notepad++";
        else if (exe.IndexOf("GitHubDesktop", StringComparison.OrdinalIgnoreCase) >= 0)
            dockName = "GitHub Desktop";
        else if (exe.IndexOf("MongoDBCompass", StringComparison.OrdinalIgnoreCase) >= 0)
            dockName = "MongoDB Compass";
        else if (ContainsExe(exe, "Lens.exe"))
            dockName = "Lens";
        else if (ContainsExe(exe, "explorer.exe"))
            dockName = "File Explorer";
        else if (!string.IsNullOrEmpty(target) && !target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                 (Directory.Exists(target) || File.Exists(target)))
        {
            dockName = "File Explorer";
            kind = Directory.Exists(target) ? "folder" : "file";
            itemTarget = target;
            title = JumpLaunch.DisplayNameFromTarget(kind, itemTarget);
            return true;
        }
        else
            return false;

        string pathFromArgs = ExtractPathArg(args);
        string urlFromArgs = ExtractUrl(args);
        if (!string.IsNullOrEmpty(urlFromArgs))
        {
            kind = "url";
            itemTarget = ChromeAppUrl.Canonical(urlFromArgs);
            string extra = ChromeAppUrl.Classify(urlFromArgs);
            title = extra != null
                ? ChromeAppUrl.Title(itemTarget, desc)
                : JumpLaunch.DisplayNameFromTarget("url", itemTarget);
            if (extra != null)
                dockName = extra;
            return true;
        }
        if (!string.IsNullOrEmpty(pathFromArgs) && (Directory.Exists(pathFromArgs) || File.Exists(pathFromArgs)))
        {
            bool dir = Directory.Exists(pathFromArgs);
            kind = dir ? "folder" : "file";
            itemTarget = pathFromArgs;
            title = JumpLaunch.DisplayNameFromTarget(kind, itemTarget);
            return true;
        }
        if (!string.IsNullOrEmpty(target) && !target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            if (Directory.Exists(target) || File.Exists(target))
            {
                bool dir = Directory.Exists(target);
                kind = dir ? "folder" : "file";
                itemTarget = target;
                title = JumpLaunch.DisplayNameFromTarget(kind, itemTarget);
                return true;
            }
        }
        return false;
    }

    private static bool ContainsExe(string file, string name)
    {
        return string.Equals(file, name, StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractUrl(string args)
    {
        var m = Regex.Match(args ?? "", @"https?://[^\s""]+", RegexOptions.IgnoreCase);
        return m.Success ? m.Value.TrimEnd('"', ',', '.') : null;
    }

    private static string ExtractPathArg(string args)
    {
        if (string.IsNullOrEmpty(args))
            return null;
        var uri = Regex.Match(args, @"file:///([^""\s]+)", RegexOptions.IgnoreCase);
        if (uri.Success)
        {
            try
            {
                var raw = "file:///" + uri.Groups[1].Value;
                var u = new Uri(Uri.UnescapeDataString(raw));
                return u.LocalPath;
            }
            catch { }
        }
        var quoted = Regex.Match(args, "\"([A-Za-z]:\\\\[^\"]+)\"");
        if (quoted.Success)
            return quoted.Groups[1].Value;
        var bare = Regex.Match(args, @"([A-Za-z]:\\[^\s""]+)");
        return bare.Success ? bare.Groups[1].Value : null;
    }

    private static byte[] ReadStream(IStream stm, int size)
    {
        var buf = new byte[size];
        int got = 0;
        var tmp = new byte[8192];
        IntPtr pRead = Marshal.AllocHGlobal(8);
        try
        {
            while (got < size)
            {
                int ask = Math.Min(tmp.Length, size - got);
                stm.Read(tmp, ask, pRead);
                int n = Marshal.ReadInt32(pRead);
                if (n <= 0)
                    break;
                Buffer.BlockCopy(tmp, 0, buf, got, n);
                got += n;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(pRead);
        }
        if (got == size)
            return buf;
        var slim = new byte[got];
        Buffer.BlockCopy(buf, 0, slim, 0, got);
        return slim;
    }
}
