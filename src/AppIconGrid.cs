// JumpDock — icon grid; spacing and chrome aligned with More apps theme.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

public sealed class AppGridItem
{
    public CatalogApp App;
    public string Caption;
    /// <summary>Optional muted line under the caption (e.g. jump cue).</summary>
    public string Subcaption;
    public bool ShowPin;
    public bool Pinned;
    public bool Checked;
    public bool SelectMode;
    /// <summary>Non-clickable section header row spanning the grid width.</summary>
    public bool IsSection;
}

public sealed class AppIconGrid : Control
{
    public event EventHandler<AppGridItem> ItemClick;
    public event EventHandler<AppGridItem> PinClick;
    public event EventHandler<AppGridItem> CheckChanged;

    private readonly List<AppGridItem> _items = new List<AppGridItem>();
    private readonly List<Rectangle> _cells = new List<Rectangle>();
    private int _hot = -1;
    private int _scrollY;
    private int _maxScroll;
    private int _cellW;
    private int _cellH;
    private int _cols = 4;
    private int _iconSize = 96;
    private Color _accent = MoreAppsTheme.Accent;

    public Color AccentColor
    {
        get { return _accent; }
        set
        {
            _accent = value.IsEmpty ? MoreAppsTheme.Accent : value;
            Invalidate();
        }
    }

    public int IconSize
    {
        get { return _iconSize; }
        set
        {
            _iconSize = value < 32 ? 32 : (value > 128 ? 128 : value);
            LayoutCells();
            Invalidate();
        }
    }

    public AppIconGrid()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = MoreAppsTheme.Bg;
        DoubleBuffered = true;
    }

    public void SetItems(IList<AppGridItem> items)
    {
        _items.Clear();
        if (items != null)
        {
            for (int i = 0; i < items.Count; i++)
                if (items[i] != null) _items.Add(items[i]);
        }
        _scrollY = 0;
        LayoutCells();
        Invalidate();
    }

    public IList<AppGridItem> GetItems()
    {
        return _items;
    }

    private int Dip(int px)
    {
        return DialogUi.Px(this, px);
    }

    private void LayoutCells()
    {
        _cells.Clear();
        int pad = Dip(16);
        int gap = Dip(14);
        int icon = _iconSize > 0 ? _iconSize : 96;
        int labelH = Dip(44);
        int cellPad = Dip(10);
        int headerH = Dip(24);
        _cellW = Math.Max(icon + cellPad * 2 + Dip(28), Dip(136));
        _cellH = icon + labelH + cellPad * 2 + Dip(6);
        int inner = Math.Max(1, Width - pad * 2);
        _cols = Math.Max(1, (inner + gap) / (_cellW + gap));
        if (_cols > 8) _cols = 8;

        int x0 = pad;
        int y = pad - _scrollY;
        int col = 0;
        int contentBottom = pad;

        for (int i = 0; i < _items.Count; i++)
        {
            AppGridItem item = _items[i];
            if (item != null && item.IsSection)
            {
                if (col > 0)
                {
                    y += _cellH + gap;
                    col = 0;
                }
                Rectangle hdr = new Rectangle(x0, y, inner, headerH);
                _cells.Add(hdr);
                y += headerH + gap;
                contentBottom = y + _scrollY;
                continue;
            }

            int cx = x0 + col * (_cellW + gap);
            int cy = y;
            _cells.Add(new Rectangle(cx, cy, _cellW, _cellH));
            col++;
            contentBottom = cy + _cellH + _scrollY;
            if (col >= _cols)
            {
                col = 0;
                y += _cellH + gap;
            }
        }

        int content = contentBottom + pad;
        _maxScroll = Math.Max(0, content - Height);
        if (_scrollY > _maxScroll) _scrollY = _maxScroll;
        if (_scrollY < 0) _scrollY = 0;
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutCells();
        Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (_maxScroll > 0)
        {
            _scrollY -= Math.Sign(e.Delta) * Math.Max(Dip(40), _cellH / 2);
            if (_scrollY < 0) _scrollY = 0;
            if (_scrollY > _maxScroll) _scrollY = _maxScroll;
            LayoutCells();
            Invalidate();
        }
        base.OnMouseWheel(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int idx = Hit(e.Location);
        if (idx != _hot)
        {
            _hot = idx;
            bool hand = idx >= 0 && idx < _items.Count && _items[idx] != null && !_items[idx].IsSection;
            Cursor = hand ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hot = -1;
        Cursor = Cursors.Default;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        int idx = Hit(e.Location);
        if (idx < 0 || idx >= _items.Count)
            return;
        var item = _items[idx];
        if (item == null || item.IsSection)
            return;
        var cell = _cells[idx];
        if (e.Button == MouseButtons.Left)
        {
            if (item.ShowPin || item.SelectMode)
            {
                if (PinRect(cell).Contains(e.Location))
                {
                    if (item.SelectMode)
                    {
                        item.Checked = !item.Checked;
                        if (CheckChanged != null) CheckChanged(this, item);
                    }
                    else if (PinClick != null)
                        PinClick(this, item);
                    Invalidate();
                    return;
                }
            }
            if (item.SelectMode)
            {
                item.Checked = !item.Checked;
                if (CheckChanged != null) CheckChanged(this, item);
                Invalidate();
                return;
            }
            if (ItemClick != null)
                ItemClick(this, item);
        }
        base.OnMouseClick(e);
    }

    private int Hit(Point p)
    {
        for (int i = 0; i < _cells.Count; i++)
            if (_cells[i].Contains(p))
                return i;
        return -1;
    }

    private Rectangle PinRect(Rectangle cell)
    {
        int s = Dip(20);
        return new Rectangle(cell.Right - s - Dip(6), cell.Top + Dip(6), s, s);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(BackColor);
        using (var nameFont = MoreAppsTheme.CaptionFont())
        using (var subFont = MoreAppsTheme.SubcaptionFont())
        using (var sectionFont = MoreAppsTheme.SectionFont())
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (i >= _cells.Count) break;
                var cell = _cells[i];
                if (cell.Bottom < 0 || cell.Top > Height)
                    continue;
                var item = _items[i];
                if (item == null) continue;

                if (item.IsSection)
                {
                    string title = item.Caption ?? "";
                    TextRenderer.DrawText(e.Graphics, title, sectionFont, cell, MoreAppsTheme.Muted,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                    continue;
                }

                bool hot = i == _hot;
                int rad = Dip(MoreAppsTheme.TileCornerDip);
                using (var path = DockForm.RoundPath(new Rectangle(cell.X, cell.Y, cell.Width - 1, cell.Height - 1), rad))
                {
                    using (var b = new SolidBrush(hot ? MoreAppsTheme.Hover : MoreAppsTheme.Raised))
                        e.Graphics.FillPath(b, path);
                    if (hot || (item.SelectMode && item.Checked) || item.Pinned)
                    {
                        using (var pen = new Pen(item.Checked || item.Pinned ? _accent : MoreAppsTheme.BorderSoft, 1.25f))
                            e.Graphics.DrawPath(pen, path);
                    }
                    int icon = _iconSize > 0 ? _iconSize : 96;
                    int ix = cell.X + (cell.Width - icon) / 2;
                    int iy = cell.Y + Dip(10);
                    DrawAppIcon(e.Graphics, item, ix, iy, icon);
                    string cap = item.Caption ?? (item.App != null ? item.App.DisplayName : "");
                    string sub = item.Subcaption ?? "";
                    int labelTop = iy + icon + Dip(6);
                    int labelW = cell.Width - Dip(10);
                    if (!string.IsNullOrEmpty(sub))
                    {
                        var tr = new Rectangle(cell.X + Dip(5), labelTop, labelW, Dip(18));
                        TextRenderer.DrawText(e.Graphics, cap, nameFont, tr, MoreAppsTheme.Text,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                        var sr = new Rectangle(cell.X + Dip(5), labelTop + Dip(16), labelW, Dip(14));
                        TextRenderer.DrawText(e.Graphics, sub, subFont, sr, MoreAppsTheme.Secondary,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                    }
                    else
                    {
                        var tr = new Rectangle(cell.X + Dip(5), labelTop, labelW, Dip(30));
                        TextRenderer.DrawText(e.Graphics, cap, nameFont, tr, MoreAppsTheme.Text,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak |
                            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                    }
                    if (item.ShowPin || item.SelectMode)
                    {
                        var pr = PinRect(cell);
                        bool on = item.SelectMode ? item.Checked : item.Pinned;
                        DrawPinBadge(e.Graphics, pr, on);
                    }
                }
            }
        }
    }

    private void DrawPinBadge(Graphics g, Rectangle pr, bool on)
    {
        using (var pb = new SolidBrush(on ? _accent : Color.FromArgb(200, 80, 84, 92)))
            g.FillEllipse(pb, pr);
        int cx = pr.X + pr.Width / 2;
        int cy = pr.Y + pr.Height / 2;
        if (on)
        {
            using (var ink = new SolidBrush(Color.FromArgb(255, 20, 22, 26)))
            {
                int r = Math.Max(2, pr.Width / 5);
                g.FillEllipse(ink, cx - r, cy - r - 1, r * 2, r * 2);
                Point[] tip = new Point[]
                {
                    new Point(cx - r / 2, cy + r / 2),
                    new Point(cx + r / 2, cy + r / 2),
                    new Point(cx, cy + r + r / 2)
                };
                g.FillPolygon(ink, tip);
            }
        }
        else
        {
            using (var pen = new Pen(MoreAppsTheme.Muted, Math.Max(1f, Dip(1))))
            {
                g.DrawEllipse(pen, pr.X + 3, pr.Y + 3, pr.Width - 7, pr.Height - 7);
                int arm = Math.Max(3, pr.Width / 5);
                g.DrawLine(pen, cx - arm, cy, cx + arm, cy);
                g.DrawLine(pen, cx, cy - arm, cx, cy + arm);
            }
        }
    }

    private void DrawAppIcon(Graphics g, AppGridItem item, int x, int y, int size)
    {
        if (item == null || item.App == null)
            return;
        string icon = item.App.IconPath;
        string lnk = item.App.LnkOrExe;
        if (!string.IsNullOrEmpty(lnk) && lnk.IndexOf('\n') >= 0)
            lnk = lnk.Split('\n')[0];
        try
        {
            Bitmap bmp = IconCache.Get(item.App.DisplayName, icon, lnk, size);
            if (bmp != null)
                g.DrawImage(bmp, x, y, size, size);
        }
        catch { }
    }
}
