// JumpDock — icon grid; icon box matches dock icon size.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

public sealed class AppGridItem
{
    public CatalogApp App;
    public string Caption;
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
    private Color _accent = Color.FromArgb(255, 152, 196, 236);

    private static readonly Color Idle = Color.FromArgb(255, 44, 46, 52);
    private static readonly Color Hover = Color.FromArgb(255, 62, 66, 74);
    private static readonly Color HoverLine = Color.FromArgb(255, 110, 116, 126);
    private static readonly Color Fg = Color.FromArgb(255, 242, 244, 248);
    private static readonly Color Muted = Color.FromArgb(255, 152, 158, 168);

    public Color AccentColor
    {
        get { return _accent; }
        set
        {
            _accent = value.IsEmpty ? Color.FromArgb(255, 152, 196, 236) : value;
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
        BackColor = Color.FromArgb(255, 22, 24, 28);
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
        int pad = Dip(12);
        int gap = Dip(10);
        int icon = _iconSize > 0 ? _iconSize : 96;
        int labelH = Dip(32);
        int cellPad = Dip(8);
        int headerH = Dip(22);
        _cellW = Math.Max(icon + cellPad * 2 + Dip(24), Dip(128));
        _cellH = icon + labelH + cellPad * 2 + Dip(4);
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
        return new Rectangle(cell.Right - s - Dip(4), cell.Top + Dip(4), s, s);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(BackColor);
        using (var nameFont = DialogUi.UiFont(8.5f, FontStyle.Regular))
        using (var sectionFont = DialogUi.UiFont(8.5f, FontStyle.Bold))
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
                    TextRenderer.DrawText(e.Graphics, title, sectionFont, cell, Muted,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                    continue;
                }

                bool hot = i == _hot;
                using (var path = DockForm.RoundPath(new Rectangle(cell.X, cell.Y, cell.Width - 1, cell.Height - 1), Dip(10)))
                {
                    using (var b = new SolidBrush(hot ? Hover : Idle))
                        e.Graphics.FillPath(b, path);
                    if (hot || (item.SelectMode && item.Checked) || item.Pinned)
                    {
                        using (var pen = new Pen(item.Checked || item.Pinned ? _accent : HoverLine, 1f))
                            e.Graphics.DrawPath(pen, path);
                    }
                    int icon = _iconSize > 0 ? _iconSize : 96;
                    int ix = cell.X + (cell.Width - icon) / 2;
                    int iy = cell.Y + Dip(8);
                    DrawAppIcon(e.Graphics, item, ix, iy, icon);
                    string cap = item.Caption ?? (item.App != null ? item.App.DisplayName : "");
                    var tr = new Rectangle(cell.X + Dip(4), iy + icon + Dip(4), cell.Width - Dip(8), Dip(28));
                    TextRenderer.DrawText(e.Graphics, cap, nameFont, tr, Fg,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak |
                        TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
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
        using (var pb = new SolidBrush(on ? _accent : Color.FromArgb(180, 80, 84, 92)))
            g.FillEllipse(pb, pr);
        int cx = pr.X + pr.Width / 2;
        int cy = pr.Y + pr.Height / 2;
        if (on)
        {
            // Filled pin: head + short stem
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
            // Outline circle + plus
            using (var pen = new Pen(Muted, Math.Max(1f, Dip(1))))
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
