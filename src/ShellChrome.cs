// Shared show, hide, and z-order for the dock and the side panel.
using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

internal enum ShellEdge
{
    Top,
    Bottom,
    Right
}

internal enum ShellView
{
    Rest,
    Reveal,
    Park
}

internal static class ShellChrome
{
    // Design units at 96 DPI; scaled via SetDipScale (Dpi/96 * Fit).
    private const int HoverStripDip = 24;
    private const int PastEdgeDip = 32;
    private const int CoverSlopDip = 8;
    private static int HoverStripPx = HoverStripDip;
    private static int PastEdgePx = PastEdgeDip;
    private static int CoverSlopPx = CoverSlopDip;
    private const int SW_SHOWMINIMIZED = 2;
    private const int SW_SHOWMAXIMIZED = 3;
    private const int DWMWA_CLOAKED = 14;
    private const uint GA_ROOT = 2;
    private const uint GW_HWNDNEXT = 2;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;

    private static readonly IntPtr HwndTopmost = new IntPtr(-1);
    private static readonly IntPtr HwndNotTopmost = new IntPtr(-2);
    private static readonly Guid ClsidImmersiveShell = new Guid("C2F03A33-21F5-47FA-B4BB-156362A2F239");
    private static readonly Guid ClsidPinnedApps = new Guid("B5A399E7-1C87-46B8-88E9-FC5747B171BD");
    private static readonly Guid ClsidDesktopManager = new Guid("AA509086-5CA9-4C25-8F95-589D3C07B48A");
    private static readonly Guid IidPinnedApps = new Guid("4CE81583-1E4C-4632-A621-07A53543148F");
    private static readonly Guid IidViewCollection = new Guid("1841C6D7-4F9D-42C0-AF41-8747538F10E5");
    private static readonly Guid WindowOnAllDesktops = new Guid("C2DDEA68-66F2-4CF9-8264-1BFD00FBBBAC");

    private static IApplicationViewCollection _views;
    private static IVirtualDesktopPinnedApps _pinnedApps;
    private static IVirtualDesktopManager _desktops;
    private static bool _comFailed;
    private static int _lastPinTick;
    private static uint _pidCache;
    private static int _pidKind;

    /// <summary>
    /// Scale edge-hover / cover slop strips for the current display (Dpi × Fit).
    /// Called from the dock on load and DPI/display change.
    /// </summary>
    internal static void SetDipScale(float dpi, float fit)
    {
        HoverStripPx = DisplayScale.Px(HoverStripDip, dpi, fit);
        PastEdgePx = DisplayScale.Px(PastEdgeDip, dpi, fit);
        CoverSlopPx = DisplayScale.Px(CoverSlopDip, dpi, fit);
        if (HoverStripPx < 12) HoverStripPx = 12;
        if (PastEdgePx < 16) PastEdgePx = 16;
        if (CoverSlopPx < 4) CoverSlopPx = 4;
    }

    internal static void Prepare(CreateParams cp)
    {
        cp.ExStyle |= 0x00000080;
        cp.ExStyle &= ~0x00000008;
        cp.ExStyle &= ~0x00040000;
    }

    internal static void ZOrder(IntPtr hwnd, bool topmost)
    {
        if (hwnd == IntPtr.Zero)
            return;
        IntPtr after = topmost ? HwndTopmost : HwndNotTopmost;
        SetWindowPos(hwnd, after, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

    internal static bool SwallowMinimize(ref Message m)
    {
        const int WM_SYSCOMMAND = 0x0112;
        const int SC_MINIMIZE = 0xF020;
        if (m.Msg != WM_SYSCOMMAND)
            return false;
        return (m.WParam.ToInt32() & 0xFFF0) == SC_MINIMIZE;
    }

    internal static void RestoreIfIconic(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return;
        if (IsIconic(hwnd) || !IsWindowVisible(hwnd))
            ShowWindow(hwnd, 9);
    }

    internal static void PinSoon(IntPtr hwnd)
    {
        int now = Environment.TickCount;
        if (_lastPinTick != 0 && unchecked(now - _lastPinTick) < 2000)
            return;
        _lastPinTick = now;
        Pin(hwnd);
    }

    internal static void Pin(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || _comFailed)
            return;
        try
        {
            EnsureCom();
            if (_desktops != null)
            {
                Guid all = WindowOnAllDesktops;
                try { _desktops.MoveWindowToDesktop(hwnd, ref all); }
                catch { }
            }
            if (_views == null || _pinnedApps == null)
                return;
            IApplicationView view;
            _views.GetViewForHwnd(hwnd, out view);
            if (view == null)
                return;
            if (!_pinnedApps.IsViewPinned(view))
                _pinnedApps.PinView(view);
            Marshal.ReleaseComObject(view);
        }
        catch
        {
        }
    }

    internal static ShellView Decide(ShellEdge edge, bool pointerOnUs, bool pressedOnUs, bool pinned)
    {
        try
        {
            bool fills = RealAppFillsPrimary();
            bool pull = pressedOnUs || pointerOnUs || PointerInEdge(edge);
            if (pinned)
                return fills ? ShellView.Reveal : ShellView.Rest;
            if (!fills)
                return ShellView.Rest;
            if (pull)
                return ShellView.Reveal;
            return ShellView.Park;
        }
        catch
        {
            return ShellView.Rest;
        }
    }

    private static void EnsureCom()
    {
        if (_desktops != null || _comFailed)
            return;
        try
        {
            _desktops = (IVirtualDesktopManager)Activator.CreateInstance(Type.GetTypeFromCLSID(ClsidDesktopManager));
        }
        catch
        {
            _desktops = null;
        }
        try
        {
            Type shellType = Type.GetTypeFromCLSID(ClsidImmersiveShell);
            IServiceProvider10 shell = (IServiceProvider10)Activator.CreateInstance(shellType);
            Guid viewSvc = IidViewCollection;
            Guid viewIid = IidViewCollection;
            _views = (IApplicationViewCollection)shell.QueryService(ref viewSvc, ref viewIid);
            Guid pinSvc = ClsidPinnedApps;
            Guid pinIid = IidPinnedApps;
            _pinnedApps = (IVirtualDesktopPinnedApps)shell.QueryService(ref pinSvc, ref pinIid);
            Marshal.ReleaseComObject(shell);
        }
        catch
        {
            _views = null;
            _pinnedApps = null;
        }
        if (_desktops == null && _pinnedApps == null)
            _comFailed = true;
    }

    private static bool PointerInEdge(ShellEdge edge)
    {
        Point pt = Control.MousePosition;
        Rectangle screen = Screen.PrimaryScreen.Bounds;
        if (edge == ShellEdge.Top)
            return pt.Y >= screen.Top - PastEdgePx && pt.Y <= screen.Top + HoverStripPx && pt.X >= screen.Left - PastEdgePx && pt.X <= screen.Right + PastEdgePx;
        if (edge == ShellEdge.Bottom)
            return pt.Y <= screen.Bottom + PastEdgePx && pt.Y >= screen.Bottom - HoverStripPx && pt.X >= screen.Left - PastEdgePx && pt.X <= screen.Right + PastEdgePx;
        return pt.X >= screen.Right - HoverStripPx && pt.X <= screen.Right + PastEdgePx && pt.Y >= screen.Top - PastEdgePx && pt.Y <= screen.Bottom + PastEdgePx;
    }

    private static bool RealAppFillsPrimary()
    {
        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return false;

        int guard = 0;
        while (hwnd != IntPtr.Zero && guard < 40)
        {
            guard++;
            if (IsCompanion(hwnd) || !IsWindowVisible(hwnd) || IsCloaked(hwnd))
            {
                hwnd = GetWindow(hwnd, GW_HWNDNEXT);
                continue;
            }
            IntPtr root = Root(hwnd);
            if (IsShellBackground(root) || IsMinimized(root))
            {
                hwnd = GetWindow(hwnd, GW_HWNDNEXT);
                continue;
            }
            if (FillsPrimaryScreen(root))
                return true;
            hwnd = GetWindow(hwnd, GW_HWNDNEXT);
        }
        return false;
    }

    private static bool FillsPrimaryScreen(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !IsWindowVisible(hwnd))
            return false;
        Screen screen = Screen.FromHandle(hwnd);
        if (screen == null || !screen.Primary)
            return false;
        WINDOWPLACEMENT place = new WINDOWPLACEMENT();
        place.length = Marshal.SizeOf(typeof(WINDOWPLACEMENT));
        if (!GetWindowPlacement(hwnd, ref place))
            return false;
        if (place.showCmd == SW_SHOWMINIMIZED)
            return false;
        if (place.showCmd == SW_SHOWMAXIMIZED)
            return true;
        RECT rect;
        if (!GetWindowRect(hwnd, out rect))
            return false;
        Rectangle bounds = screen.Bounds;
        return rect.Left <= bounds.Left + CoverSlopPx
            && rect.Top <= bounds.Top + CoverSlopPx
            && rect.Right >= bounds.Right - CoverSlopPx
            && rect.Bottom >= bounds.Bottom - CoverSlopPx;
    }

    private static bool IsCompanion(IntPtr hwnd)
    {
        uint pid;
        GetWindowThreadProcessId(hwnd, out pid);
        if (pid == 0)
            return false;
        if (pid == (uint)Process.GetCurrentProcess().Id)
            return true;
        if (pid == _pidCache)
            return _pidKind == 1;
        _pidCache = pid;
        _pidKind = 0;
        try
        {
            string name = Process.GetProcessById((int)pid).ProcessName;
            if (name == "JumpDock" || name == "SidePanel")
                _pidKind = 1;
        }
        catch
        {
            _pidKind = 0;
        }
        return _pidKind == 1;
    }

    private static bool IsShellBackground(IntPtr hwnd)
    {
        string cls = ClassName(hwnd);
        return cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd";
    }

    private static bool IsMinimized(IntPtr hwnd)
    {
        WINDOWPLACEMENT place = new WINDOWPLACEMENT();
        place.length = Marshal.SizeOf(typeof(WINDOWPLACEMENT));
        if (IsIconic(hwnd))
            return true;
        if (!GetWindowPlacement(hwnd, ref place))
            return false;
        return place.showCmd == SW_SHOWMINIMIZED;
    }

    private static bool IsCloaked(IntPtr hwnd)
    {
        int cloaked;
        if (DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out cloaked, 4) != 0)
            return false;
        return cloaked != 0;
    }

    private static string ClassName(IntPtr hwnd)
    {
        StringBuilder cls = new StringBuilder(64);
        GetClassName(hwnd, cls, cls.Capacity);
        return cls.ToString();
    }

    private static IntPtr Root(IntPtr hwnd)
    {
        IntPtr root = GetAncestor(hwnd, GA_ROOT);
        if (root == IntPtr.Zero)
            return hwnd;
        return root;
    }

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPLACEMENT
    {
        public int length;
        public int flags;
        public int showCmd;
        public POINT ptMinPosition;
        public POINT ptMaxPosition;
        public RECT rcNormalPosition;
    }
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIInspectable)]
[Guid("372E1D3B-38D3-42E4-A15B-8AB2B178F513")]
internal interface IApplicationView
{
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("1841C6D7-4F9D-42C0-AF41-8747538F10E5")]
internal interface IApplicationViewCollection
{
    int GetViews(out IntPtr array);
    int GetViewsByZOrder(out IntPtr array);
    int GetViewsByAppUserModelId(string id, out IntPtr array);
    int GetViewForHwnd(IntPtr hwnd, out IApplicationView view);
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("A5CD92FF-29BE-454C-8D04-D82879FB3F1B")]
internal interface IVirtualDesktopManager
{
    bool IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow);
    Guid GetWindowDesktopId(IntPtr topLevelWindow);
    void MoveWindowToDesktop(IntPtr topLevelWindow, ref Guid desktopId);
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("4CE81583-1E4C-4632-A621-07A53543148F")]
internal interface IVirtualDesktopPinnedApps
{
    bool IsAppIdPinned(string appId);
    void PinAppID(string appId);
    void UnpinAppID(string appId);
    bool IsViewPinned(IApplicationView applicationView);
    void PinView(IApplicationView applicationView);
    void UnpinView(IApplicationView applicationView);
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("6D5140C1-7436-11CE-8034-00AA006009FA")]
internal interface IServiceProvider10
{
    [return: MarshalAs(UnmanagedType.IUnknown)]
    object QueryService(ref Guid service, ref Guid riid);
}
