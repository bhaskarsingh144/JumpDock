// JumpDock — jump menu. Locked 2026-09-28.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

public sealed class JumpMenuForm : Form
{
    internal static readonly Color Bg = Color.FromArgb(255, 22, 24, 28);
    internal static readonly Color Border = Color.FromArgb(255, 68, 72, 80);
    internal static readonly Color RowHot = Color.FromArgb(255, 46, 50, 58);
    internal static readonly Color Fg = Color.FromArgb(255, 242, 244, 248);
    internal static readonly Color Muted = Color.FromArgb(255, 152, 158, 168);
    internal static readonly Color Accent = Color.FromArgb(255, 152, 196, 236);
    internal static readonly Color Line = Color.FromArgb(255, 48, 52, 60);
    internal static readonly Color ScrollTrack = Color.FromArgb(255, 30, 32, 36);
    internal static readonly Color ScrollThumb = Color.FromArgb(255, 82, 88, 98);
    internal static readonly Color ScrollThumbHot = Color.FromArgb(255, 122, 130, 142);

    private readonly string _name;
    private readonly string _lnk;
    private readonly string _icon;
    private readonly DockKind _kind;
    private readonly JumpListStore _store;
    private readonly JumpListView _list;
    private readonly JumpActionBar _actions;
    private readonly MenuMetrics _metrics;
    private readonly DockConfig _cfg;
    private int _suspendClose;

    public JumpMenuForm(string name, string lnk, string icon, JumpListStore store)
        : this(name, lnk, icon, store, DockDefaults.MenuScaleLarge, null)
    {
    }

    public JumpMenuForm(string name, string lnk, string icon, JumpListStore store, double menuScale)
        : this(name, lnk, icon, store, menuScale, null)
    {
    }

    public JumpMenuForm(string name, string lnk, string icon, JumpListStore store, double menuScale, DockConfig cfg)
    {
        _name = name ?? "";
        _lnk = lnk;
        _icon = icon;
        _store = store;
        _cfg = cfg;
        _kind = JumpLaunch.InferKind(_name, _lnk);
        _metrics = MenuMetrics.FromScale(menuScale);

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Border;
        AutoScaleMode = AutoScaleMode.None;
        KeyPreview = true;
        Padding = new Padding(1);
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

        var inner = new Panel { Dock = DockStyle.Fill, BackColor = Bg };

        _actions = new JumpActionBar
        {
            Dock = DockStyle.Top,
            HintText = JumpLaunch.KindHint(_kind),
            RefreshEnabled = JumpLists.CanRefresh(_name)
        };
        _actions.RefreshClick += delegate { DoRefresh(); };

        _list = new JumpListView(_store, _name, _metrics) { Dock = DockStyle.Fill, BackColor = Bg };
        _list.OpenItem += delegate(object s, JumpItemEventArgs e) { OpenItem(e.Item); };
        _list.Changed += delegate { Relayout(); };

        inner.Controls.Add(_list);
        inner.Controls.Add(_actions);
        Controls.Add(inner);

        KeyDown += delegate(object s, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
                Close();
        };
        Deactivate += delegate
        {
            if (_suspendClose != 0)
                return;
            var t = new System.Windows.Forms.Timer { Interval = 180 };
            t.Tick += delegate
            {
                t.Stop();
                t.Dispose();
                if (_suspendClose == 0 && !IsDisposed && Form.ActiveForm != this)
                    Close();
            };
            t.Start();
        };

        Width = Dip(_metrics.BaseWidth);
        _list.Reload();
        Relayout();
    }

    internal int Dip(int px)
    {
        int dpi = 96;
        try
        {
            if (IsHandleCreated)
                dpi = DeviceDpi;
        }
        catch { }
        if (dpi < 96)
            dpi = 96;
        return (int)Math.Round(px * dpi / 96.0);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Width = Dip(_metrics.BaseWidth);
        _list.Reload();
        Relayout();
    }

    public void PlaceOnScreen(Point dockBottomLeft, Rectangle work)
    {
        Relayout();
        int x = dockBottomLeft.X;
        int y = dockBottomLeft.Y + Dip(8);
        if (x + Width > work.Right)
            x = work.Right - Width - Dip(8);
        if (x < work.Left)
            x = work.Left + Dip(8);
        if (y + Height > work.Bottom)
            y = work.Bottom - Height - Dip(8);
        if (y < work.Top)
            y = work.Top + Dip(8);
        Location = new Point(x, y);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x00000080;
            cp.ExStyle |= 0x00000008;
            return cp;
        }
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (Width < 8 || Height < 8)
            return;
        using (var path = DockForm.RoundPath(new Rectangle(0, 0, Width, Height), Dip(10)))
        {
            var old = Region;
            Region = new Region(path);
            if (old != null)
                old.Dispose();
        }
    }

    private void Relayout()
    {
        _actions.Height = _actions.PreferredHeight;
        int listH = _list.ContentHeight;
        int maxList = Dip(_metrics.MaxList);
        int minList = Dip(120);
        int chromeH = _actions.Height;
        try
        {
            int workH = Screen.FromControl(this).WorkingArea.Height;
            int chrome = 2 + chromeH;
            int fit = workH - Dip(24) - chrome;
            if (fit > minList && maxList > fit)
                maxList = fit;
        }
        catch { }
        if (listH > maxList)
            listH = maxList;
        if (listH < minList)
            listH = minList;
        Height = 2 + chromeH + listH;
        _list.SyncBar();
    }

    private void DoRefresh()
    {
        if (!JumpLists.CanRefresh(_name))
            return;
        _suspendClose++;
        try
        {
            _actions.SetBusy(true, "Refreshing…");
            Application.DoEvents();
            JumpListRefresh.RefreshOne(_store, _cfg, _name);
            _list.Reload();
            Relayout();
            _actions.FlashTip("Jump list updated");
            _actions.SetBusy(false, null);
        }
        catch
        {
            _actions.FlashTip("Refresh failed");
            _actions.SetBusy(false, null);
        }
        finally
        {
            _suspendClose--;
        }
    }

    private void OpenItem(JumpItem item)
    {
        _suspendClose++;
        try
        {
            if (!JumpLaunch.OpenItem(_name, _lnk, _icon, item))
            {
                MessageBox.Show(this, "Could not open " + JumpLaunch.DisplayName(item), "JumpDock", MessageBoxButtons.OK, MessageBoxIcon.None);
                return;
            }
            _store.RecordOpen(_name, item);
            Close();
        }
        catch
        {
            MessageBox.Show(this, "Could not open " + JumpLaunch.DisplayName(item), "JumpDock", MessageBoxButtons.OK, MessageBoxIcon.None);
        }
        finally
        {
            _suspendClose--;
        }
    }
}

internal sealed class JumpItemEventArgs : EventArgs
{
    public JumpItem Item;
    public JumpItemEventArgs(JumpItem item) { Item = item; }
}

/// <summary>Compact icon toolbar: refresh + info (tooltips). No Open — dock click opens the app.</summary>
internal sealed class JumpActionBar : Control
{
    public event EventHandler RefreshClick;

    private readonly ToolTip _tip = new ToolTip { ShowAlways = true, InitialDelay = 280, ReshowDelay = 120, AutoPopDelay = 4000 };
    private string _hintText = "";
    private int _hot = -1; // 0 = refresh, 1 = info
    private bool _busy;
    private bool _refreshEnabled = true;
    private string _busyTip;
    private Timer _flashTimer;

    public JumpActionBar()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Default;
    }

    public string HintText
    {
        get { return _hintText; }
        set { _hintText = value ?? ""; }
    }

    public bool RefreshEnabled
    {
        get { return _refreshEnabled; }
        set { _refreshEnabled = value; Invalidate(); }
    }

    public int PreferredHeight
    {
        get { return Dip(40); }
    }

    public void SetBusy(bool busy, string tip)
    {
        _busy = busy;
        _busyTip = tip;
        Invalidate();
    }

    public void FlashTip(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        _tip.Show(text, this, Dip(8), Height + Dip(2), 1600);
        if (_flashTimer != null)
        {
            _flashTimer.Stop();
            _flashTimer.Dispose();
        }
        _flashTimer = new Timer { Interval = 1700 };
        _flashTimer.Tick += delegate
        {
            _flashTimer.Stop();
            _flashTimer.Dispose();
            _flashTimer = null;
            try { _tip.Hide(this); } catch { }
        };
        _flashTimer.Start();
    }

    private int Dip(int px)
    {
        int dpi = 96;
        try
        {
            if (IsHandleCreated)
                dpi = DeviceDpi;
        }
        catch { }
        if (dpi < 96) dpi = 96;
        return (int)Math.Round(px * dpi / 96.0);
    }

    private Rectangle Btn(int index)
    {
        int pad = Dip(10);
        int gap = Dip(6);
        int s = Dip(28);
        int y = Math.Max(Dip(4), (Height - s) / 2);
        return new Rectangle(pad + index * (s + gap), y, s, s);
    }

    private int Hit(Point p)
    {
        if (Btn(0).Contains(p)) return 0;
        if (Btn(1).Contains(p)) return 1;
        return -1;
    }

    private string TipFor(int idx)
    {
        if (idx == 0)
        {
            if (!_refreshEnabled)
                return "No jump list for this app";
            return _busy && !string.IsNullOrEmpty(_busyTip) ? _busyTip : "Refresh jump list";
        }
        if (idx == 1)
            return string.IsNullOrEmpty(_hintText) ? "How links open" : _hintText;
        return "";
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int h = Hit(e.Location);
        if (h != _hot)
        {
            _hot = h;
            Cursor = (h == 0 && _refreshEnabled && !_busy) || h == 1 ? Cursors.Hand : Cursors.Default;
            string t = TipFor(h);
            if (h >= 0 && !string.IsNullOrEmpty(t))
                _tip.SetToolTip(this, t);
            else
                _tip.SetToolTip(this, "");
            Invalidate();
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hot = -1;
        Cursor = Cursors.Default;
        _tip.SetToolTip(this, "");
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        int h = Hit(e.Location);
        if (h == 0 && _refreshEnabled && !_busy && RefreshClick != null)
            RefreshClick(this, EventArgs.Empty);
        else if (h == 1)
            FlashTip(TipFor(1));
        base.OnMouseClick(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(JumpMenuForm.Bg);
        using (var pen = new Pen(JumpMenuForm.Line, 1f))
            e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);

        Color refreshColor = !_refreshEnabled || _busy ? JumpMenuForm.Muted : JumpMenuForm.Accent;
        DrawBtn(e.Graphics, 0, refreshColor, true);
        DrawBtn(e.Graphics, 1, JumpMenuForm.Muted, false);
    }

    private void DrawBtn(Graphics g, int index, Color iconColor, bool isRefresh)
    {
        var r = Btn(index);
        bool hot = _hot == index;
        if (hot)
        {
            using (var path = DockForm.RoundPath(r, Dip(6)))
            using (var b = new SolidBrush(JumpMenuForm.RowHot))
                g.FillPath(b, path);
        }
        if (isRefresh)
            DrawRefreshGlyph(g, r, iconColor);
        else
            DrawInfoGlyph(g, r, iconColor);
    }

    private void DrawRefreshGlyph(Graphics g, Rectangle r, Color c)
    {
        int m = Dip(6);
        var arc = new Rectangle(r.X + m, r.Y + m, r.Width - m * 2, r.Height - m * 2);
        using (var pen = new Pen(c, Math.Max(1.5f, Dip(2))))
        {
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            g.DrawArc(pen, arc, 40f, 260f);
        }
        // small arrow head at arc end
        float rad = (float)(arc.Width / 2.0);
        float cx = arc.X + rad;
        float cy = arc.Y + rad;
        double a = 40 * Math.PI / 180.0;
        float tipX = cx + rad * (float)Math.Cos(a);
        float tipY = cy + rad * (float)Math.Sin(a);
        using (var b = new SolidBrush(c))
        {
            PointF[] tri = new PointF[]
            {
                new PointF(tipX, tipY),
                new PointF(tipX - Dip(5), tipY - Dip(1)),
                new PointF(tipX - Dip(1), tipY + Dip(5))
            };
            g.FillPolygon(b, tri);
        }
    }

    private void DrawInfoGlyph(Graphics g, Rectangle r, Color c)
    {
        int m = Dip(6);
        var circle = new Rectangle(r.X + m, r.Y + m, r.Width - m * 2, r.Height - m * 2);
        using (var pen = new Pen(c, Math.Max(1.5f, Dip(2))))
            g.DrawEllipse(pen, circle);
        using (var font = new Font("Segoe UI", Math.Max(8f, r.Height * 0.38f), FontStyle.Bold, GraphicsUnit.Pixel))
        using (var b = new SolidBrush(c))
        {
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("i", font, b, circle, sf);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_flashTimer != null) { _flashTimer.Stop(); _flashTimer.Dispose(); _flashTimer = null; }
            if (_tip != null) _tip.Dispose();
        }
        base.Dispose(disposing);
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
    }
}

internal sealed class JumpListView : Control
{
    public event EventHandler<JumpItemEventArgs> OpenItem;
    public event EventHandler Changed;

    private readonly JumpListStore _store;
    private readonly string _name;
    private readonly List<JumpRow> _rows = new List<JumpRow>();
    private readonly Font _title;
    private readonly Font _sub;
    private readonly Font _head;
    private int _hot = -1;
    private int _pinW;
    private int _scrollY;
    private int _maxScroll;
    private bool _barOn;
    private bool _thumbHot;
    private bool _drag;
    private int _dragY;
    private int _dragScroll;

    public JumpListView(JumpListStore store, string name)
        : this(store, name, MenuMetrics.FromScale(DockDefaults.MenuScaleLarge))
    {
    }

    public JumpListView(JumpListStore store, string name, MenuMetrics metrics)
    {
        _store = store;
        _name = name;
        if (metrics == null)
            metrics = MenuMetrics.FromScale(DockDefaults.MenuScaleLarge);
        _title = metrics.MakeTitleFont();
        _sub = metrics.MakeSubFont();
        _head = metrics.MakeSectionFont();
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Disposed += delegate
        {
            _title.Dispose();
            _sub.Dispose();
            _head.Dispose();
        };
        MeasurePin();
    }

    private int Dip(int px)
    {
        int dpi = 96;
        try
        {
            if (IsHandleCreated)
                dpi = DeviceDpi;
        }
        catch { }
        if (dpi < 96)
            dpi = 96;
        return (int)Math.Round(px * dpi / 96.0);
    }

    private void MeasurePin()
    {
        _pinW = Dip(28);
    }

    private int ItemH()
    {
        int gap = Math.Max(Dip(4), _title.Height / 5);
        return Dip(10) + _title.Height + gap + _sub.Height + Dip(10);
    }

    private int HeadH()
    {
        return _head.Height + Dip(18);
    }

    private int EmptyH()
    {
        return _sub.Height + Dip(14);
    }

    private int PadX()
    {
        return Dip(16);
    }

    public int ContentHeight
    {
        get
        {
            int h = Dip(6);
            for (int i = 0; i < _rows.Count; i++)
                h += _rows[i].Height;
            h += Dip(6);
            return h;
        }
    }

    public void SyncBar()
    {
        SyncScroll();
        Invalidate();
    }

    public void Reload()
    {
        MeasurePin();
        int itemH = ItemH();
        int headH = HeadH();
        int emptyH = EmptyH();
        _rows.Clear();
        var list = _store.For(_name);
        _rows.Add(new JumpRow { Kind = JumpRowKind.Header, Text = "Pinned", Height = headH });
        if (list.pinned == null || list.pinned.Count == 0)
            _rows.Add(new JumpRow { Kind = JumpRowKind.Empty, Text = "No pins yet", Height = emptyH });
        else
        {
            for (int i = 0; i < list.pinned.Count; i++)
                _rows.Add(new JumpRow { Kind = JumpRowKind.Item, Item = list.pinned[i], Pinned = true, Height = itemH });
        }
        _rows.Add(new JumpRow { Kind = JumpRowKind.Header, Text = "Recent", Height = headH });
        int shown = 0;
        if (list.recent != null)
        {
            for (int i = 0; i < list.recent.Count; i++)
            {
                if (_store.IsPinned(_name, list.recent[i].target))
                    continue;
                _rows.Add(new JumpRow { Kind = JumpRowKind.Item, Item = list.recent[i], Pinned = false, Height = itemH });
                shown++;
            }
        }
        if (shown == 0)
            _rows.Add(new JumpRow { Kind = JumpRowKind.Empty, Text = "Nothing here yet", Height = emptyH });
        SyncScroll();
        Invalidate();
    }

    private int BarW()
    {
        return _barOn ? Dip(10) : 0;
    }

    private void SyncScroll()
    {
        int content = ContentHeight;
        int view = Height;
        if (view < 1)
            view = 1;
        if (content <= view)
        {
            _barOn = false;
            _maxScroll = 0;
            _scrollY = 0;
            _drag = false;
        }
        else
        {
            _barOn = true;
            _maxScroll = content - view;
            if (_scrollY > _maxScroll)
                _scrollY = _maxScroll;
            if (_scrollY < 0)
                _scrollY = 0;
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        MeasurePin();
        SyncScroll();
        Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (_maxScroll > 0)
        {
            SetScrollY(_scrollY - Math.Sign(e.Delta) * ItemH() * 2);
            Invalidate();
        }
        base.OnMouseWheel(e);
    }

    private void SetScrollY(int y)
    {
        if (y < 0) y = 0;
        if (y > _maxScroll) y = _maxScroll;
        _scrollY = y;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_drag)
        {
            int dy = e.Y - _dragY;
            int track = Math.Max(1, Height - Dip(12));
            int range = ThumbRange();
            if (range > 0)
                SetScrollY(_dragScroll + (int)(dy * (double)_maxScroll / range));
            Invalidate();
            return;
        }
        bool hotThumb = _barOn && ThumbRect().Contains(e.Location);
        if (hotThumb != _thumbHot)
        {
            _thumbHot = hotThumb;
            Invalidate();
        }
        if (_barOn && e.X >= Width - BarW())
        {
            Cursor = Cursors.Default;
            return;
        }
        int idx = HitRow(e.Location);
        if (idx != _hot)
        {
            _hot = idx;
            Invalidate();
        }
        Cursor = (idx >= 0 && _rows[idx].Kind == JumpRowKind.Item) ? Cursors.Hand : Cursors.Default;
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hot = -1;
        _thumbHot = false;
        Cursor = Cursors.Default;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _drag = false;
        Capture = false;
        base.OnMouseUp(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
            return;
        if (_barOn && e.X >= Width - BarW())
        {
            var thumb = ThumbRect();
            if (thumb.Contains(e.Location))
            {
                _drag = true;
                _dragY = e.Y;
                _dragScroll = _scrollY;
                Capture = true;
            }
            else
            {
                int page = Math.Max(ItemH() * 4, Height * 3 / 4);
                if (e.Y < thumb.Y)
                    SetScrollY(_scrollY - page);
                else
                    SetScrollY(_scrollY + page);
                Invalidate();
            }
            return;
        }
        int idx = HitRow(e.Location);
        if (idx < 0)
            return;
        var row = _rows[idx];
        if (row.Kind != JumpRowKind.Item)
            return;
        int pinLeft = ListWidth() - _pinW - PadX();
        if (e.X >= pinLeft)
        {
            if (row.Pinned)
                _store.Unpin(_name, row.Item.target);
            else
                _store.Pin(_name, row.Item);
            Reload();
            if (Changed != null)
                Changed(this, EventArgs.Empty);
            return;
        }
        if (OpenItem != null)
            OpenItem(this, new JumpItemEventArgs(row.Item));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(JumpMenuForm.Bg);
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        int y = Dip(6) - ScrollY();
        int w = ListWidth();
        for (int i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            var bounds = new Rectangle(0, y, w, row.Height);
            if (bounds.Bottom >= 0 && bounds.Top <= Height)
                PaintRow(g, row, bounds, i == _hot);
            y += row.Height;
        }
        PaintScroll(g);
    }

    private void PaintScroll(Graphics g)
    {
        if (!_barOn)
            return;
        int w = BarW();
        var track = new Rectangle(Width - w, 0, w, Height);
        using (var br = new SolidBrush(JumpMenuForm.ScrollTrack))
            g.FillRectangle(br, track);
        var thumb = ThumbRect();
        using (var path = RoundRect(thumb, Dip(3)))
        using (var br = new SolidBrush(_thumbHot || _drag ? JumpMenuForm.ScrollThumbHot : JumpMenuForm.ScrollThumb))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillPath(br, path);
        }
    }

    private int ThumbRange()
    {
        return Math.Max(1, Height - Dip(12) - ThumbH());
    }

    private int ThumbH()
    {
        int view = Math.Max(1, Height);
        int content = ContentHeight;
        int h = (int)(view * (double)view / Math.Max(view, content));
        int min = Dip(28);
        if (h < min) h = min;
        if (h > Height - Dip(16))
            h = Height - Dip(16);
        return h;
    }

    private Rectangle ThumbRect()
    {
        int w = BarW();
        int pad = Dip(2);
        int h = ThumbH();
        int y = Dip(6);
        if (_maxScroll > 0)
            y += (int)(_scrollY * (double)ThumbRange() / _maxScroll);
        return new Rectangle(Width - w + pad, y, Math.Max(4, w - pad * 2), h);
    }

    private static GraphicsPath RoundRect(Rectangle r, int rad)
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

    private void PaintRow(Graphics g, JumpRow row, Rectangle bounds, bool hot)
    {
        int pad = PadX();
        var flags = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.PreserveGraphicsClipping;

        if (row.Kind == JumpRowKind.Header)
        {
            TextRenderer.DrawText(g, row.Text, _head, new Rectangle(pad, bounds.Y, bounds.Width - pad * 2, bounds.Height), JumpMenuForm.Muted, flags);
            return;
        }
        if (row.Kind == JumpRowKind.Empty)
        {
            TextRenderer.DrawText(g, row.Text, _sub, new Rectangle(pad, bounds.Y, bounds.Width - pad * 2, bounds.Height), JumpMenuForm.Muted, flags);
            return;
        }

        if (hot)
        {
            using (var br = new SolidBrush(JumpMenuForm.RowHot))
                g.FillRectangle(br, new Rectangle(Dip(8), bounds.Y + Dip(2), bounds.Width - Dip(16), bounds.Height - Dip(4)));
        }

        bool ok = JumpLaunch.TargetLooksOk(row.Item);
        string title = JumpLaunch.DisplayName(row.Item);
        string sub = JumpLaunch.DisplaySub(row.Item);
        int gap = Math.Max(Dip(4), _title.Height / 5);
        int textLeft = pad;
        int pinLeft = bounds.Width - pad - _pinW;
        int textW = pinLeft - Dip(8) - textLeft;
        if (textW < Dip(48))
            textW = Dip(48);

        int titleTop = bounds.Y + Dip(8);
        var titleRect = new Rectangle(textLeft, titleTop, textW, _title.Height);
        var subRect = new Rectangle(textLeft, titleTop + _title.Height + gap, textW, _sub.Height);

        var old = g.Clip;
        g.SetClip(titleRect);
        TextRenderer.DrawText(g, title, _title, titleRect, ok ? JumpMenuForm.Fg : JumpMenuForm.Muted, flags);
        g.Clip = old;
        if (!string.IsNullOrEmpty(sub) && !string.Equals(sub, title, StringComparison.OrdinalIgnoreCase))
        {
            g.SetClip(subRect);
            TextRenderer.DrawText(g, sub, _sub, subRect, JumpMenuForm.Muted, flags);
            g.Clip = old;
        }

        DrawPin(g, new Rectangle(pinLeft, bounds.Y, _pinW, bounds.Height), row.Pinned, hot);
    }

    private void DrawPin(Graphics g, Rectangle cell, bool pinned, bool hot)
    {
        if (!pinned && !hot)
            return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int cx = cell.X + cell.Width / 2;
        int cy = cell.Y + cell.Height / 2;
        int r = Math.Max(3, Dip(5));
        var head = new Rectangle(cx - r, cy - r - Dip(3), r * 2, r * 2);
        using (var pen = new Pen(JumpMenuForm.Accent, Math.Max(1.2f, Dip(2) / 2f)))
        using (var br = new SolidBrush(JumpMenuForm.Accent))
        {
            if (pinned)
                g.FillEllipse(br, head);
            g.DrawEllipse(pen, head);
            g.DrawLine(pen, cx, head.Bottom - 1, cx, cy + Dip(8));
        }
    }

    private int ListWidth()
    {
        return Width - BarW();
    }

    private int ScrollY()
    {
        return _scrollY;
    }

    private int HitRow(Point p)
    {
        if (_barOn && p.X >= Width - BarW())
            return -1;
        int y = Dip(6) - ScrollY();
        for (int i = 0; i < _rows.Count; i++)
        {
            int h = _rows[i].Height;
            if (p.Y >= y && p.Y < y + h)
                return i;
            y += h;
        }
        return -1;
    }
}

internal enum JumpRowKind
{
    Header,
    Empty,
    Item
}

internal sealed class JumpRow
{
    public JumpRowKind Kind;
    public string Text;
    public JumpItem Item;
    public bool Pinned;
    public int Height;
}
