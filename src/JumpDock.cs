// JumpDock.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Web.Script.Serialization;

internal static class JumpDock
{
    [DllImport("user32.dll")]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

    [STAThread]
    private static void Main(string[] args)
    {
        if (WantsSeed(args))
        {
            AppCatalog.RunSeed();
            return;
        }
        try { SetProcessDpiAwarenessContext(new IntPtr(-4)); }
        catch { }

        bool created;
        var mutex = new Mutex(true, @"Local\JumpDock", out created);
        if (!created)
            return;
        try
        {
            JumpPaths.MigrateFromWorkAppsIfNeeded();
            JumpPaths.EnsureLauncherBesideConfig();
            try
            {
                // Keep sign-in start if the old WorkApps shortcut existed, or always ensure after migrate.
                string legacyLnk = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                    "WorkApps Dock.lnk");
                if (File.Exists(legacyLnk) || !DockStartup.IsEnabled())
                {
                    if (File.Exists(legacyLnk))
                    {
                        try { File.Delete(legacyLnk); } catch { }
                    }
                    DockStartup.EnsureShortcut();
                }
            }
            catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new DockForm());
        }
        finally
        {
            try { mutex.ReleaseMutex(); }
            catch { }
            mutex.Dispose();
        }
    }

    private static bool WantsSeed(string[] args)
    {
        if (HasSeed(args))
            return true;
        return HasSeed(Environment.GetCommandLineArgs());
    }

    private static bool HasSeed(string[] args)
    {
        if (args == null)
            return false;
        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--seed", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}

public class DockForm : Form
{
    internal static readonly Color BarFill = Color.FromArgb(255, 32, 34, 38);
    internal static readonly Color BarLine = Color.FromArgb(255, 78, 82, 90);
    private static readonly Color Chroma = Color.FromArgb(255, 7, 3, 9);

    private readonly string _configPath;
    private readonly ToolTip _tip;
    private readonly ContextMenuStrip _menu;
    private readonly System.Windows.Forms.Timer _mouseTimer;
    private DockConfig _cfg;
    private DockLayout _layout;
    private JumpListStore _jumps;
    private JumpMenuForm _jump;
    private MoreAppsForm _more;
    private bool _shown;
    private int _fullY;
    private int _hideY;
    private bool _topEdge;
    private bool _firstRunPending;
    private bool _syncing;

    public DockForm()
    {
        _configPath = JumpPaths.DockJsonPath;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = false;
        AllowTransparency = true;
        BackColor = Chroma;
        TransparencyKey = Chroma;
        DoubleBuffered = true;
        Text = "JumpDock";
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);

        _tip = new ToolTip();
        _tip.ShowAlways = true;
        _tip.InitialDelay = 200;
        _tip.ReshowDelay = 0;
        _tip.AutoPopDelay = 8000;
        _tip.UseAnimation = false;
        _tip.UseFading = false;

        _menu = new ContextMenuStrip();
        _menu.Items.Add("Manage apps...", null, delegate { OpenAppPicker(); });
        _menu.Items.Add("Settings...", null, delegate { OpenSettings(); });
        _menu.Items.Add("Reload", null, delegate { Rebuild(); });
        _menu.Items.Add("Exit", null, delegate { Close(); });
        // Do not set Form.ContextMenuStrip — it steals right-click from every tile.

        AllowDrop = true;
        _jumps = new JumpListStore();
        LoadConfig();
        BuildUi();
        PlaceBar(false, false);

        _mouseTimer = new System.Windows.Forms.Timer { Interval = 80 };
        _mouseTimer.Tick += MouseTick;
        _mouseTimer.Start();

        if (_firstRunPending)
        {
            Shown += delegate
            {
                BeginInvoke(new Action(delegate
                {
                    if (_firstRunPending)
                    {
                        _firstRunPending = false;
                        OpenAppPicker(true);
                    }
                }));
            };
        }
    }

    protected override bool ShowWithoutActivation
    {
        get { return true; }
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            ShellChrome.Prepare(cp);
            return cp;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_mouseTimer != null) _mouseTimer.Dispose();
            if (_tip != null) _tip.Dispose();
            if (_menu != null) _menu.Dispose();
            ClearTiles();
        }
        base.Dispose(disposing);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Chroma);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var path = RoundPath(r, _layout != null ? _layout.Radius : 16))
        using (var fill = new SolidBrush(BarFill))
        using (var pen = new Pen(BarLine, 1f))
        {
            e.Graphics.FillPath(fill, path);
            e.Graphics.DrawPath(pen, path);
        }
    }

    private void Rebuild()
    {
        try
        {
            ClearTiles();
            LoadConfig();
            BuildUi();
            PlaceBar(_shown, false);
            Invalidate();
        }
        catch { }
    }

    private void ClearTiles()
    {
        for (int i = Controls.Count - 1; i >= 0; i--)
        {
            var c = Controls[i];
            Controls.RemoveAt(i);
            c.Dispose();
        }
    }

    private void LoadConfig()
    {
        _cfg = DockConfigStore.Load(_configPath);
        if (_cfg.items == null || _cfg.items.Count == 0)
            AppCatalog.SeedIfEmpty(_cfg, _configPath);
        if (!_cfg.setupComplete)
            _firstRunPending = true;
        try { WindowsJumpList.Sync(_jumps, _cfg.items, _cfg); }
        catch { }
        _topEdge = string.IsNullOrEmpty(_cfg.edge) || string.Equals(_cfg.edge, "top", StringComparison.OrdinalIgnoreCase);
        var b = Screen.PrimaryScreen.Bounds;
        int desired = _cfg.iconSize > 0 ? _cfg.iconSize : DockDefaults.DefaultIconSize;
        int barCount = VisibleBarCount();
        float dpi = DeviceDpi;
        if (dpi < 96f) dpi = 96f;
        DisplayProfile profile = DisplayScale.Measure(dpi);
        _layout = DockLayout.For(desired, _cfg.peek, barCount, b.Width, b.Height, profile);
        IconCache.Clear();
        ShellChrome.SetDipScale(profile.Dpi, profile.Fit);
        int shrunkMark = DisplayScale.Px(56, profile.Dpi, profile.Fit);
        if (_layout.Shrunk && _layout.Icon < shrunkMark)
            _tip.SetToolTip(this, "Dock crowded — open Settings to lower Max visible or hide pins via overflow.");
        else
            _tip.SetToolTip(this, "JumpDock");
        if (_topEdge)
        {
            _fullY = b.Top + _layout.ShowInset;
            _hideY = b.Top - _layout.BarH + _layout.Peek;
        }
        else
        {
            _fullY = b.Bottom - _layout.BarH - _layout.ShowInset;
            _hideY = b.Bottom - _layout.Peek;
        }
    }

    private int VisibleBarCount()
    {
        int n = _cfg.items != null ? _cfg.items.Count : 0;
        int max = _cfg.maxVisible > 0 ? _cfg.maxVisible : DockDefaults.DefaultMaxVisible;
        int visible = Math.Min(max, n);
        return visible + 1; // always include More apps tile
    }

    private void BuildUi()
    {
        int pad = _layout.BarPad;
        int gap = _layout.Gap;
        int tile = _layout.Tile;
        int x = pad;
        int y = (_layout.BarH - tile) / 2;
        int max = _cfg.maxVisible > 0 ? _cfg.maxVisible : DockDefaults.DefaultMaxVisible;
        int n = _cfg.items.Count;
        int visible = Math.Min(max, n);
        for (int i = 0; i < visible; i++)
        {
            var item = _cfg.items[i];
            string lnk = DockConfigStore.Expand(item.lnk);
            string icon = DockConfigStore.Expand(item.icon);
            var img = IconCache.GetOwned(item.name, icon, lnk, _layout.Icon);
            var tileCtrl = new IconTile(item.name, lnk, icon, img, tile, _layout.Icon, _layout.TileRadius, _tip, this, i, false);
            tileCtrl.Location = new Point(x, y);
            Controls.Add(tileCtrl);
            x += tile + gap;
        }
        // Always show More apps tile (Win11-style grid).
        var overflow = new IconTile("More", null, null, null, tile, _layout.Icon, _layout.TileRadius, _tip, this, -1, true);
        overflow.Location = new Point(x, y);
        Controls.Add(overflow);
        x += tile + gap;
        Width = Math.Max(80, x + pad - gap);
        Height = _layout.BarH;
    }

    private void OpenSettings()
    {
        using (var dlg = new DockSettingsForm(_cfg))
        {
            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;
            _cfg = dlg.Result;
            DockConfigStore.Save(_cfg, _configPath);
            Rebuild();
        }
    }

    internal void OpenAppPicker()
    {
        OpenAppPicker(false);
    }

    internal void OpenAppPicker(bool firstRun)
    {
        PlaceBar(true, false);
        int dockIcon = _layout != null && _layout.Icon > 0
            ? _layout.Icon
            : (_cfg != null && _cfg.iconSize > 0 ? _cfg.iconSize : 96);
        using (var dlg = new AppPickerForm(_cfg, _configPath, firstRun, dockIcon))
        {
            var r = dlg.ShowDialog(this);
            if (r == DialogResult.OK || dlg.Applied)
                ReloadFromDisk();
            else if (firstRun)
                ReloadFromDisk();
        }
    }

    internal void ReloadFromDisk()
    {
        try
        {
            AppCatalog.InvalidateScan();
            _cfg = DockConfigStore.Load(_configPath);
            Rebuild();
        }
        catch
        {
            Rebuild();
        }
    }

    /// <summary>Rebuild bar from in-memory config after pin/unpin (no catalog rescan).</summary>
    internal void RefreshPins()
    {
        try
        {
            Rebuild();
        }
        catch { }
    }

    internal void SaveConfigAndRebuild()
    {
        DockConfigStore.Save(_cfg, _configPath);
        Rebuild();
    }

    internal DockConfig Config
    {
        get { return _cfg; }
    }

    internal JumpListStore JumpStore
    {
        get { return _jumps; }
    }

    internal void ReorderItems(int fromIndex, int toIndex)
    {
        if (_cfg == null || _cfg.items == null)
            return;
        if (fromIndex < 0 || fromIndex >= _cfg.items.Count)
            return;
        if (toIndex < 0) toIndex = 0;
        if (toIndex >= _cfg.items.Count) toIndex = _cfg.items.Count - 1;
        if (fromIndex == toIndex)
            return;
        var item = _cfg.items[fromIndex];
        _cfg.items.RemoveAt(fromIndex);
        if (toIndex > fromIndex) toIndex--;
        _cfg.items.Insert(toIndex, item);
        SaveConfigAndRebuild();
    }

    internal void PromoteToDock(int itemIndex)
    {
        if (_cfg == null || _cfg.items == null)
            return;
        if (itemIndex < 0 || itemIndex >= _cfg.items.Count)
            return;
        var item = _cfg.items[itemIndex];
        _cfg.items.RemoveAt(itemIndex);
        _cfg.items.Insert(0, item);
        SaveConfigAndRebuild();
    }

    internal void ShowOverflowMenu(Control tile)
    {
        ShowMoreApps(tile);
    }

    internal void ShowMoreApps(Control tile)
    {
        if (_more != null && !_more.IsDisposed)
        {
            _more.Close();
            _more = null;
        }
        if (_jump != null && !_jump.IsDisposed)
        {
            _jump.Close();
            _jump = null;
        }
        PlaceBar(true, true);
        int dockIcon = _layout != null && _layout.Icon > 0
            ? _layout.Icon
            : (_cfg != null && _cfg.iconSize > 0 ? _cfg.iconSize : 96);
        var more = new MoreAppsForm(this, _cfg, _configPath, dockIcon);
        _more = more;
        more.FormClosed += delegate
        {
            if (_more == more)
                _more = null;
        };
        var screenPt = tile != null
            ? tile.PointToScreen(new Point(0, tile.Height))
            : new Point(Left, Bottom);
        var wa = Screen.FromControl(this).WorkingArea;
        more.EnsureSized();
        more.PlaceOnScreen(screenPt, wa);
        more.Show();
        more.PlaceOnScreen(screenPt, wa);
        if (more.IsHandleCreated)
            ShellChrome.ZOrder(more.Handle, true);
    }

    internal double MenuScale
    {
        get { return _cfg != null ? _cfg.menuScale : DockDefaults.MenuScaleLarge; }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ShellChrome.Pin(Handle);
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_DISPLAYCHANGE = 0x007E;
        const int WM_DPICHANGED = 0x02E0;
        if (ShellChrome.SwallowMinimize(ref m))
            return;
        base.WndProc(ref m);
        if (m.Msg == WM_DISPLAYCHANGE || m.Msg == WM_DPICHANGED)
        {
            if (IsHandleCreated && !IsDisposed)
                BeginInvoke(new Action(Rebuild));
        }
    }

    private void PlaceBar(bool show, bool topmost)
    {
        var wa = Screen.PrimaryScreen.Bounds;
        int left = show ? wa.Left + (wa.Width - Width) / 2 : -32000;
        int top = show ? _fullY : -32000;
        if (show && IsHandleCreated)
            ShellChrome.RestoreIfIconic(Handle);
        if (_shown != show || Left != left || Top != top || Opacity != 1.0)
        {
            _shown = show;
            Opacity = 1.0;
            Left = left;
            Top = top;
        }
        if (show && IsHandleCreated)
        {
            ShellChrome.ZOrder(Handle, topmost);
            if (_jump != null && !_jump.IsDisposed && _jump.IsHandleCreated)
                ShellChrome.ZOrder(_jump.Handle, topmost);
            if (_more != null && !_more.IsDisposed && _more.IsHandleCreated)
                ShellChrome.ZOrder(_more.Handle, topmost);
        }
    }

    private void MouseTick(object sender, EventArgs e)
    {
        if (_syncing || _layout == null)
            return;
        _syncing = true;
        try
        {
            Point pt = Control.MousePosition;
            bool jumpOpen = _jump != null && !_jump.IsDisposed && _jump.Visible;
            bool moreOpen = _more != null && !_more.IsDisposed && _more.Visible;
            bool pointerOnUs = Bounds.Contains(pt);
            if (jumpOpen && _jump.Bounds.Contains(pt))
                pointerOnUs = true;
            if (moreOpen && _more.Bounds.Contains(pt))
                pointerOnUs = true;
            if (_menu != null && _menu.Visible && _menu.Bounds.Contains(pt))
                pointerOnUs = true;
            bool pressing = (Control.MouseButtons & MouseButtons.Left) == MouseButtons.Left
                && Bounds.Contains(pt);
            ShellEdge edge = _topEdge ? ShellEdge.Top : ShellEdge.Bottom;
            ShellView view = ShellChrome.Decide(edge, pointerOnUs, pressing, false);
            if (view == ShellView.Park)
            {
                ClosePopups();
                PlaceBar(false, false);
            }
            else
                PlaceBar(true, view == ShellView.Reveal);
            if (IsHandleCreated)
                ShellChrome.PinSoon(Handle);
        }
        finally
        {
            _syncing = false;
        }
    }

    private void ClosePopups()
    {
        if (_menu != null)
            _menu.Close();
        if (_jump != null && !_jump.IsDisposed)
        {
            var jump = _jump;
            _jump = null;
            jump.Close();
        }
        if (_more != null && !_more.IsDisposed)
        {
            var more = _more;
            _more = null;
            more.Close();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
            _menu.Show(Cursor.Position);
        base.OnMouseUp(e);
    }

    internal void ShowJumpMenu(Control tile, string name, string lnk, string icon)
    {
        if (_jump != null && !_jump.IsDisposed)
        {
            _jump.Close();
            _jump = null;
        }
        PlaceBar(true, true);
        var jump = new JumpMenuForm(name, lnk, icon, _jumps, MenuScale, _cfg);
        _jump = jump;
        jump.FormClosed += delegate
        {
            if (_jump == jump)
                _jump = null;
        };
        var screenPt = tile.PointToScreen(new Point(0, 0));
        var wa = Screen.FromControl(this).WorkingArea;
        jump.PlaceOnScreen(new Point(screenPt.X, Bottom), wa);
        jump.Show();
        if (jump.IsHandleCreated)
            ShellChrome.ZOrder(jump.Handle, true);
    }

    internal static GraphicsPath RoundPath(Rectangle r, int rad)
    {
        var path = new GraphicsPath();
        int d = Math.Max(2, rad * 2);
        if (d > r.Width) d = r.Width;
        if (d > r.Height) d = r.Height;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal static class IconLoader
{
    private const int SIIGBF_BIGGERSIZEOK = 0x01;
    private const int SIIGBF_ICONONLY = 0x04;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(string pszPath, IntPtr pbc, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDc);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, int uStartScan, int cScanLines, byte[] lpvBits, ref BitmapInfo lpbmi, int uUsage);

    [ComImport]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(ShellSize cxcy, int flags, out IntPtr phbm);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ShellSize
    {
        public int cx;
        public int cy;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public short biPlanes;
        public short biBitCount;
        public int biCompression;
        public int biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public int biClrUsed;
        public int biClrImportant;
    }

    public static Bitmap Load(string name, string iconPath, string lnk, int size)
    {
        Bitmap bmp = null;
        try
        {
            bmp = FromIconPath(iconPath, size);
            if (IsValid(bmp))
                return Own(bmp, size);

            SafeDispose(bmp);
            bmp = FromShell(lnk, size);
            if (IsValid(bmp))
                return Own(bmp, size);

            SafeDispose(bmp);
            bmp = FromAssociated(lnk, size);
            if (IsValid(bmp))
                return Own(bmp, size);
        }
        catch { }
        SafeDispose(bmp);
        return LetterBadge(name, size);
    }

    private static Bitmap FromIconPath(string iconPath, int size)
    {
        if (string.IsNullOrEmpty(iconPath))
            return null;
        var pngSibling = Path.ChangeExtension(iconPath, ".png");
        if (!string.Equals(iconPath, pngSibling, StringComparison.OrdinalIgnoreCase) && File.Exists(pngSibling))
        {
            var fromPng = FromRasterFile(pngSibling, size);
            if (IsValid(fromPng))
                return fromPng;
            SafeDispose(fromPng);
        }
        if (!File.Exists(iconPath))
            return null;
        var ext = Path.GetExtension(iconPath);
        if (string.Equals(ext, ".png", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(ext, ".jpg", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(ext, ".jpeg", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(ext, ".bmp", StringComparison.OrdinalIgnoreCase))
            return FromRasterFile(iconPath, size);
        if (string.Equals(ext, ".ico", StringComparison.OrdinalIgnoreCase))
            return FromIcoFile(iconPath, size);
        return FromRasterFile(iconPath, size);
    }

    private static Bitmap FromRasterFile(string path, int size)
    {
        var bytes = File.ReadAllBytes(path);
        using (var ms = new MemoryStream(bytes, 0, bytes.Length, false, true))
        using (var img = Image.FromStream(ms, false, false))
            return Clone32(img, size);
    }

    private static Bitmap FromIcoFile(string path, int size)
    {
        var data = File.ReadAllBytes(path);
        var pngFrame = ExtractIcoPng(data);
        if (pngFrame != null)
        {
            using (var ms = new MemoryStream(pngFrame, false))
            using (var img = Image.FromStream(ms, false, false))
            {
                var bmp = Clone32(img, size);
                TrySavePng(path, img);
                return bmp;
            }
        }
        try
        {
            using (var ms = new MemoryStream(data, false))
            using (var ic = new Icon(ms))
            using (var b = ic.ToBitmap())
            {
                if (IsValid(b))
                    return Clone32(b, size);
            }
        }
        catch { }
        return null;
    }

    private static byte[] ExtractIcoPng(byte[] data)
    {
        if (data == null || data.Length < 22)
            return null;
        if (BitConverter.ToUInt16(data, 0) != 0)
            return null;
        if (BitConverter.ToUInt16(data, 2) != 1)
            return null;
        int count = BitConverter.ToUInt16(data, 4);
        int bestOffset = -1;
        int bestBytes = 0;
        int bestArea = -1;
        for (int i = 0; i < count; i++)
        {
            int e = 6 + i * 16;
            if (e + 16 > data.Length)
                break;
            int w = data[e];
            int h = data[e + 1];
            if (w == 0) w = 256;
            if (h == 0) h = 256;
            int nbytes = BitConverter.ToInt32(data, e + 8);
            int offset = BitConverter.ToInt32(data, e + 12);
            if (offset < 0 || nbytes < 24 || (long)offset + nbytes > data.Length)
                continue;
            if (data[offset] != 0x89 || data[offset + 1] != 0x50 || data[offset + 2] != 0x4E || data[offset + 3] != 0x47)
                continue;
            int area = w * h;
            if (area > bestArea)
            {
                bestArea = area;
                bestOffset = offset;
                bestBytes = nbytes;
            }
        }
        if (bestOffset < 0)
            return null;
        var frame = new byte[bestBytes];
        Buffer.BlockCopy(data, bestOffset, frame, 0, bestBytes);
        return frame;
    }

    private static void TrySavePng(string icoPath, Image img)
    {
        try
        {
            var png = Path.ChangeExtension(icoPath, ".png");
            if (File.Exists(png))
                return;
            using (var copy = Clone32(img, Math.Max(img.Width, 256)))
                copy.Save(png, ImageFormat.Png);
        }
        catch { }
    }

    private static Bitmap FromShell(string path, int size)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;
        object unk = null;
        IntPtr hbm = IntPtr.Zero;
        try
        {
            var iid = new Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b");
            int hr = SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out unk);
            if (hr != 0 || unk == null)
                return null;
            var factory = unk as IShellItemImageFactory;
            if (factory == null)
                return null;
            var sz = new ShellSize { cx = 256, cy = 256 };
            hr = factory.GetImage(sz, SIIGBF_ICONONLY | SIIGBF_BIGGERSIZEOK, out hbm);
            if (hr != 0 || hbm == IntPtr.Zero)
                return null;
            return FromHbitmapAlpha(hbm, size);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (hbm != IntPtr.Zero)
                DeleteObject(hbm);
            if (unk != null)
                Marshal.ReleaseComObject(unk);
        }
    }

    private static Bitmap FromHbitmapAlpha(IntPtr hbm, int destSize)
    {
        var bmi = new BitmapInfo();
        bmi.biSize = Marshal.SizeOf(typeof(BitmapInfo));
        IntPtr hdc = GetDC(IntPtr.Zero);
        try
        {
            GetDIBits(hdc, hbm, 0, 0, null, ref bmi, 0);
            int w = Math.Abs(bmi.biWidth);
            int h = Math.Abs(bmi.biHeight);
            if (w < 1 || h < 1)
            {
                using (var tmp = Image.FromHbitmap(hbm))
                    return Clone32(tmp, destSize);
            }
            bmi.biBitCount = 32;
            bmi.biCompression = 0;
            bmi.biHeight = -h;
            bmi.biSizeImage = w * h * 4;
            var bits = new byte[bmi.biSizeImage];
            GetDIBits(hdc, hbm, 0, h, bits, ref bmi, 0);
            using (var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
            {
                var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                Marshal.Copy(bits, 0, data.Scan0, bits.Length);
                bmp.UnlockBits(data);
                return Clone32(bmp, destSize);
            }
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, hdc);
        }
    }

    private static Bitmap FromAssociated(string path, int size)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;
        using (var ic = Icon.ExtractAssociatedIcon(path))
        {
            if (ic == null)
                return null;
            using (var b = ic.ToBitmap())
                return Clone32(b, size);
        }
    }

    private static Bitmap Own(Bitmap bmp, int size)
    {
        if (bmp.Width == size && bmp.Height == size && bmp.PixelFormat == PixelFormat.Format32bppArgb)
            return bmp;
        try
        {
            return Clone32(bmp, size);
        }
        finally
        {
            bmp.Dispose();
        }
    }

    private static Bitmap Clone32(Image src, int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            g.CompositingMode = CompositingMode.SourceOver;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(src, 0, 0, size, size);
        }
        return bmp;
    }

    private static bool IsValid(Bitmap b)
    {
        if (b == null || b.Width < 4 || b.Height < 4)
            return false;
        int w = b.Width;
        int h = b.Height;
        int step = Math.Max(1, Math.Min(w, h) / 24);
        int sample = 0;
        int opaque = 0;
        var colors = new HashSet<int>();
        for (int y = 0; y < h; y += step)
        {
            for (int x = 0; x < w; x += step)
            {
                var c = b.GetPixel(x, y);
                sample++;
                if (c.A > 24)
                {
                    opaque++;
                    colors.Add(c.ToArgb() & 0x00F8F8F8);
                }
            }
        }
        if (sample == 0 || opaque < Math.Max(3, sample / 25))
            return false;
        if (opaque > 20 && colors.Count > opaque * 0.85)
            return false;
        return true;
    }

    private static Bitmap LetterBadge(string name, int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(Color.Transparent);
            char c = '?';
            if (!string.IsNullOrEmpty(name))
            {
                foreach (var ch in name)
                {
                    if (char.IsLetterOrDigit(ch))
                    {
                        c = char.ToUpperInvariant(ch);
                        break;
                    }
                }
            }
            using (var font = new Font("Segoe UI Semibold", Math.Max(12, size * 0.46f), FontStyle.Bold, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(Color.FromArgb(235, 236, 240, 246)))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(c.ToString(), font, brush, new RectangleF(0, 1, size, size), sf);
            }
        }
        return bmp;
    }

    private static void SafeDispose(Image img)
    {
        if (img != null)
        {
            try { img.Dispose(); }
            catch { }
        }
    }
}

public class IconTile : Panel
{
    public const string DockIndexFormat = "JumpDockIndex";
    private static readonly Color Idle = Color.FromArgb(255, 44, 46, 52);
    private static readonly Color Hover = Color.FromArgb(255, 62, 66, 74);
    private static readonly Color HoverLine = Color.FromArgb(255, 110, 116, 126);
    private readonly string _name;
    private readonly string _lnk;
    private readonly string _iconPath;
    private readonly Bitmap _img;
    private readonly int _icon;
    private readonly int _tileRadius;
    private readonly ToolTip _tip;
    private readonly DockForm _host;
    private readonly int _itemIndex;
    private readonly bool _overflow;
    private bool _hot;
    private bool _press;
    private Point _pressPt;
    private bool _dragged;
    private bool _dropHot;

    public int ItemIndex { get { return _itemIndex; } }
    public bool IsOverflow { get { return _overflow; } }
    public string AppName { get { return _name; } }
    public string AppLnk { get { return _lnk; } }
    public string AppIcon { get { return _iconPath; } }

    public IconTile(string name, string lnk, string iconPath, Bitmap img, int tile, int icon, int tileRadius, ToolTip tip, DockForm host, int itemIndex, bool overflow)
    {
        _name = name ?? "";
        _lnk = lnk;
        _iconPath = iconPath;
        _img = img;
        _icon = icon;
        _tileRadius = tileRadius > 0 ? tileRadius : 10;
        _tip = tip;
        _host = host;
        _itemIndex = itemIndex;
        _overflow = overflow;
        Size = new Size(tile, tile);
        Cursor = Cursors.Hand;
        BackColor = Color.Transparent;
        AllowDrop = true;
        AccessibleName = _name;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        string label;
        if (_overflow)
            label = "More apps (overflow)";
        else if (!string.IsNullOrEmpty(_lnk) && File.Exists(_lnk))
            label = _name;
        else
            label = _name + " (not installed)";
        if (_tip != null)
            _tip.SetToolTip(this, label);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _img != null)
            _img.Dispose();
        base.Dispose(disposing);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hot = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hot = false;
        _press = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && !_overflow)
        {
            _press = true;
            _pressPt = e.Location;
            _dragged = false;
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_press && !_overflow && e.Button == MouseButtons.Left && _itemIndex >= 0)
        {
            int dx = e.X - _pressPt.X;
            int dy = e.Y - _pressPt.Y;
            if (!_dragged && (dx * dx + dy * dy) >= 36)
            {
                _dragged = true;
                _press = false;
                DoDragDrop(_itemIndex.ToString(), DragDropEffects.Move);
            }
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right && _host != null)
        {
            if (_overflow)
            {
                _host.ShowOverflowMenu(this);
                return;
            }
            var host = _host;
            var tile = this;
            var name = _name;
            var lnk = _lnk;
            var icon = _iconPath;
            host.BeginInvoke(new Action(delegate { host.ShowJumpMenu(tile, name, lnk, icon); }));
            return;
        }
        base.OnMouseUp(e);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button != MouseButtons.Left || _dragged)
            return;
        if (_overflow)
        {
            if (_host != null)
                _host.ShowOverflowMenu(this);
            return;
        }
        if (string.IsNullOrEmpty(_lnk) || !File.Exists(_lnk))
        {
            ShowFail();
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo { FileName = _lnk, UseShellExecute = true });
        }
        catch
        {
            ShowFail();
        }
    }

    protected override void OnDragEnter(DragEventArgs drgevent)
    {
        if (AcceptDrop(drgevent))
        {
            drgevent.Effect = HasDockIndex(drgevent) ? DragDropEffects.Move : DragDropEffects.Copy;
            _dropHot = true;
            Invalidate();
        }
        else
            drgevent.Effect = DragDropEffects.None;
        base.OnDragEnter(drgevent);
    }

    protected override void OnDragOver(DragEventArgs drgevent)
    {
        if (AcceptDrop(drgevent))
            drgevent.Effect = HasDockIndex(drgevent) ? DragDropEffects.Move : DragDropEffects.Copy;
        else
            drgevent.Effect = DragDropEffects.None;
        base.OnDragOver(drgevent);
    }

    protected override void OnDragLeave(EventArgs e)
    {
        _dropHot = false;
        Invalidate();
        base.OnDragLeave(e);
    }

    protected override void OnDragDrop(DragEventArgs drgevent)
    {
        _dropHot = false;
        Invalidate();
        if (_host == null)
            return;
        if (HasDockIndex(drgevent) && !_overflow && _itemIndex >= 0)
        {
            try
            {
                int from = int.Parse(drgevent.Data.GetData(DataFormats.Text).ToString());
                _host.ReorderItems(from, _itemIndex);
            }
            catch { }
            return;
        }
        if (_overflow)
            return;
        TryOpenDropped(drgevent);
        base.OnDragDrop(drgevent);
    }

    private bool AcceptDrop(DragEventArgs e)
    {
        if (e == null || e.Data == null)
            return false;
        if (HasDockIndex(e) && !_overflow && _itemIndex >= 0)
            return true;
        if (_overflow)
            return false;
        var kind = JumpLaunch.InferKind(_name, _lnk);
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            return JumpLaunch.AllowsFolder(kind) || JumpLaunch.AllowsFile(kind);
        if (e.Data.GetDataPresent(DataFormats.Text) || e.Data.GetDataPresent(DataFormats.UnicodeText))
        {
            string t = GetTextData(e);
            return JumpLaunch.AllowsLink(kind) && LooksLikeUrl(t);
        }
        return false;
    }

    private static bool HasDockIndex(DragEventArgs e)
    {
        if (e == null || e.Data == null || !e.Data.GetDataPresent(DataFormats.Text))
            return false;
        string t = e.Data.GetData(DataFormats.Text) as string;
        int n;
        return !string.IsNullOrEmpty(t) && int.TryParse(t, out n);
    }

    private void TryOpenDropped(DragEventArgs e)
    {
        var kind = JumpLaunch.InferKind(_name, _lnk);
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length == 0)
                return;
            string path = files[0];
            JumpItem item;
            if (Directory.Exists(path))
            {
                if (!JumpLaunch.AllowsFolder(kind)) return;
                item = JumpLaunch.FromFolder(path);
            }
            else
            {
                if (!JumpLaunch.AllowsFile(kind)) return;
                item = JumpLaunch.FromFile(path);
            }
            try { JumpLaunch.OpenItem(_name, _lnk, _iconPath, item); }
            catch { }
            return;
        }
        string text = GetTextData(e);
        if (JumpLaunch.AllowsLink(kind) && LooksLikeUrl(text))
        {
            var item = JumpLaunch.FromUrl(text.Trim());
            try { JumpLaunch.OpenItem(_name, _lnk, _iconPath, item); }
            catch { }
        }
    }

    private static string GetTextData(DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.UnicodeText))
            return e.Data.GetData(DataFormats.UnicodeText) as string;
        if (e.Data.GetDataPresent(DataFormats.Text))
            return e.Data.GetData(DataFormats.Text) as string;
        return null;
    }

    private static bool LooksLikeUrl(string t)
    {
        if (string.IsNullOrEmpty(t)) return false;
        t = t.Trim();
        return t.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               t.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
               t.StartsWith("www.", StringComparison.OrdinalIgnoreCase);
    }

    private void ShowFail()
    {
        if (_tip == null)
            return;
        _tip.Show("Could not open " + _name, this, Width / 2, Height, 2200);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var path = DockForm.RoundPath(r, _tileRadius))
        {
            using (var b = new SolidBrush((_hot || _dropHot) ? Hover : Idle))
                e.Graphics.FillPath(b, path);
            if (_hot || _dropHot)
            {
                using (var pen = new Pen(HoverLine, 1f))
                    e.Graphics.DrawPath(pen, path);
            }
            if (_overflow)
            {
                using (var font = new Font("Segoe UI", Math.Max(10f, _icon / 4f), FontStyle.Bold))
                using (var brush = new SolidBrush(Color.FromArgb(255, 220, 224, 230)))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    e.Graphics.DrawString("...", font, brush, r, sf);
                }
            }
            else if (_img != null)
            {
                var old = e.Graphics.Save();
                e.Graphics.SetClip(path);
                int pad = Math.Max(4, (Width - _icon) / 2);
                int box = Width - pad * 2;
                int x = (Width - box) / 2;
                int y = (Height - box) / 2;
                e.Graphics.DrawImage(_img, x, y, box, box);
                e.Graphics.Restore(old);
            }
        }
    }
}
