// JumpDock.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

internal static class JumpDockLauncher
{
    private const int SW_MAXIMIZE = 3;
    private const uint GPS_READWRITE = 2;
    private static readonly Guid IID_IPropertyStore = new Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");
    private static readonly Guid PKEY_FMT = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");
    private static readonly PROPERTYKEY PKEY_AppUserModel_ID = new PROPERTYKEY { fmtid = PKEY_FMT, pid = 5 };
    private static readonly PROPERTYKEY PKEY_AppUserModel_RelaunchCommand = new PROPERTYKEY { fmtid = PKEY_FMT, pid = 2 };
    private static readonly PROPERTYKEY PKEY_AppUserModel_RelaunchDisplayNameResource = new PROPERTYKEY { fmtid = PKEY_FMT, pid = 4 };
    private static readonly PROPERTYKEY PKEY_AppUserModel_RelaunchIconResource = new PROPERTYKEY { fmtid = PKEY_FMT, pid = 3 };

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            var name = GetArg(args, "--name");
            var url = GetArg(args, "--url");
            var icon = GetArg(args, "--icon");
            var aumid = GetArg(args, "--aumid");
            var titleMatch = GetArg(args, "--title-match");
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(url) || string.IsNullOrEmpty(aumid))
                return 1;

            var chrome = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
            if (!File.Exists(chrome))
                return 2;

            var exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var relaunch = "\"" + exe + "\" --name \"" + name + "\" --url \"" + url + "\" --icon \"" + icon + "\" --aumid \"" + aumid + "\"";
            if (!string.IsNullOrEmpty(titleMatch))
                relaunch += " --title-match \"" + titleMatch + "\"";

            var before = SnapshotAppWindows();
            var psi = new ProcessStartInfo
            {
                FileName = chrome,
                Arguments = "--profile-directory=Default --start-maximized --app=\"" + url + "\"",
                UseShellExecute = false
            };
            Process.Start(psi);

            var targets = new List<IntPtr>();
            var end = DateTime.UtcNow.AddSeconds(20);
            while (DateTime.UtcNow < end)
            {
                Thread.Sleep(250);
                foreach (var h in FindNewAppWindows(before))
                {
                    if (string.IsNullOrEmpty(titleMatch) || TitleContains(h, titleMatch))
                    {
                        if (!targets.Contains(h))
                            targets.Add(h);
                    }
                }
                if (targets.Count > 0)
                    break;
            }
            if (targets.Count == 0 && !string.IsNullOrEmpty(titleMatch))
                targets.AddRange(FindByTitle(titleMatch));
            if (targets.Count == 0)
                return 3;

            for (int i = 0; i < 12; i++)
            {
                foreach (var t in targets)
                {
                    StampWindow(t, aumid, relaunch, name, icon);
                    ShowWindow(t, SW_MAXIMIZE);
                }
                Thread.Sleep(400);
            }
            return 0;
        }
        catch
        {
            return 9;
        }
    }

    private static string GetArg(string[] args, string key)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return "";
    }

    private static HashSet<long> SnapshotAppWindows()
    {
        var set = new HashSet<long>();
        EnumWindows((h, l) =>
        {
            if (IsAppWindow(h))
                set.Add(h.ToInt64());
            return true;
        }, IntPtr.Zero);
        return set;
    }

    private static List<IntPtr> FindNewAppWindows(HashSet<long> before)
    {
        var list = new List<IntPtr>();
        EnumWindows((h, l) =>
        {
            if (!IsAppWindow(h))
                return true;
            if (before.Contains(h.ToInt64()))
                return true;
            list.Add(h);
            return true;
        }, IntPtr.Zero);
        return list;
    }

    private static bool TitleContains(IntPtr h, string needle)
    {
        var title = new StringBuilder(512);
        GetWindowText(h, title, title.Capacity);
        return title.ToString().IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static List<IntPtr> FindByTitle(string needle)
    {
        var list = new List<IntPtr>();
        EnumWindows((h, l) =>
        {
            if (!IsAppWindow(h))
                return true;
            var title = new StringBuilder(512);
            GetWindowText(h, title, title.Capacity);
            if (title.ToString().IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                list.Add(h);
            return true;
        }, IntPtr.Zero);
        return list;
    }

    private static bool IsAppWindow(IntPtr h)
    {
        if (!IsWindowVisible(h))
            return false;
        var cls = new StringBuilder(256);
        GetClassName(h, cls, cls.Capacity);
        if (cls.ToString() != "Chrome_WidgetWin_1")
            return false;
        var title = new StringBuilder(512);
        GetWindowText(h, title, title.Capacity);
        var t = title.ToString();
        if (string.IsNullOrWhiteSpace(t))
            return false;
        if (t.IndexOf(" - Google Chrome", StringComparison.OrdinalIgnoreCase) >= 0)
            return false;
        uint pid;
        GetWindowThreadProcessId(h, out pid);
        try
        {
            var p = Process.GetProcessById((int)pid);
            if (!string.Equals(p.ProcessName, "chrome", StringComparison.OrdinalIgnoreCase))
                return false;
        }
        catch
        {
            return false;
        }
        return true;
    }

    private static void StampWindow(IntPtr hwnd, string aumid, string relaunch, string name, string icon)
    {
        IPropertyStore store;
        var iid = IID_IPropertyStore;
        int hr = SHGetPropertyStoreForWindow(hwnd, ref iid, out store);
        if (hr != 0 || store == null)
            return;
        try
        {
            SetString(store, PKEY_AppUserModel_ID, aumid);
            SetString(store, PKEY_AppUserModel_RelaunchCommand, relaunch);
            SetString(store, PKEY_AppUserModel_RelaunchDisplayNameResource, name);
            if (!string.IsNullOrEmpty(icon) && File.Exists(icon))
                SetString(store, PKEY_AppUserModel_RelaunchIconResource, icon);
            store.Commit();
        }
        finally
        {
            Marshal.ReleaseComObject(store);
        }
    }

    private static void SetString(IPropertyStore store, PROPERTYKEY key, string value)
    {
        var pv = new PROPVARIANT();
        pv.vt = 31;
        pv.pszVal = Marshal.StringToCoTaskMemUni(value);
        try
        {
            store.SetValue(ref key, ref pv);
        }
        finally
        {
            Marshal.FreeCoTaskMem(pv.pszVal);
        }
    }

    private delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumProc cb, IntPtr l);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr h);

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr h, StringBuilder s, int n);

    [DllImport("user32.dll")]
    private static extern int GetClassName(IntPtr h, StringBuilder s, int n);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr h, int n);

    [DllImport("shell32.dll")]
    private static extern int SHGetPropertyStoreForWindow(IntPtr hwnd, ref Guid riid, out IPropertyStore ppv);

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PROPERTYKEY
    {
        public Guid fmtid;
        public uint pid;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PROPVARIANT
    {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(8)] public IntPtr pszVal;
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        uint GetCount(out uint cProps);
        uint GetAt(uint iProp, out PROPERTYKEY pkey);
        uint GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
        uint SetValue(ref PROPERTYKEY key, ref PROPVARIANT pv);
        uint Commit();
    }
}
