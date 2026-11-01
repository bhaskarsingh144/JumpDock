// JumpDock — More apps panel (centered, Installed/External, QB-inspired chrome).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

public sealed class MoreAppsForm : Form
{
    private enum MoreTab
    {
        Installed = 0,
        External = 1
    }

    private static MoreTab _sessionTab = MoreTab.Installed;

    private readonly DockForm _host;
    private readonly DockConfig _cfg;
    private readonly string _configPath;
    private readonly int _dockIconSize;
    private readonly AppIconGrid _grid;
    private readonly Panel _shell;
    private readonly Panel _header;
    private readonly Label _title;
    private readonly Label _hint;
    private readonly Label _manage;
    private readonly Panel _tabs;
    private readonly Label _tabInstalled;
    private readonly Label _tabExternal;
    private readonly Panel _searchHost;
    private readonly TextBox _search;
    private readonly List<AppGridItem> _allItems = new List<AppGridItem>();
    private MoreTab _tab;
    private Color _pinAccent;
    private int _suspendClose;
    private int _cornerRad;
    private bool _searchFocused;

    private Point _restLocation;
    private bool _openAnimating;
    private Timer _openTimer;
    private int _openElapsed;
    private int _openFromY;
    private int _openToY;

    private Timer _tabTimer;
    private int _tabElapsed;
    private float _tabPillX;
    private float _tabPillFrom;
    private float _tabPillTo;
    private bool _tabAnimating;

    public MoreAppsForm(DockForm host, DockConfig cfg, string configPath, int dockIconSize)
    {
        _host = host;
        _cfg = cfg;
        _configPath = configPath;
        _dockIconSize = dockIconSize > 0 ? dockIconSize : (cfg != null && cfg.iconSize > 0 ? cfg.iconSize : 96);
        _tab = _sessionTab;
        _pinAccent = DockAccent.FromConfig(cfg);
        if (_pinAccent.IsEmpty)
            _pinAccent = MoreAppsTheme.Accent;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = MoreAppsTheme.Surface;
        AutoScaleMode = AutoScaleMode.None;
        KeyPreview = true;
        Padding = new Padding(0);
        Opacity = 1.0;

        _shell = new Panel { Dock = DockStyle.Fill, BackColor = MoreAppsTheme.Bg };

        _header = new Panel
        {
            Dock = DockStyle.Top,
            BackColor = MoreAppsTheme.Bg
        };
        _title = new Label
        {
            Text = "More apps",
            ForeColor = MoreAppsTheme.Text,
            Font = MoreAppsTheme.TitleFont(),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        _hint = new Label
        {
            Text = "Installed apps on this PC · pin to add to dock",
            ForeColor = MoreAppsTheme.Secondary,
            Font = MoreAppsTheme.HintFont(),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        _manage = new Label
        {
            Text = "Settings",
            ForeColor = MoreAppsTheme.Accent,
            Font = MoreAppsTheme.Semibold(9.5f),
            AutoSize = true,
            Cursor = Cursors.Hand,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleCenter
        };
        _manage.Click += delegate
        {
            _suspendClose++;
            try
            {
                if (_host != null)
                    _host.OpenSettings(true);
                Close();
            }
            finally { _suspendClose--; }
        };
        _manage.MouseEnter += delegate
        {
            _manage.ForeColor = Color.FromArgb(255,
                Math.Min(255, MoreAppsTheme.Accent.R + 30),
                Math.Min(255, MoreAppsTheme.Accent.G + 20),
                Math.Min(255, MoreAppsTheme.Accent.B + 10));
        };
        _manage.MouseLeave += delegate { _manage.ForeColor = MoreAppsTheme.Accent; };

        _tabs = new Panel { BackColor = MoreAppsTheme.Chip };
        _tabInstalled = MakeTabLabel("Installed");
        _tabExternal = MakeTabLabel("External");
        _tabInstalled.Click += delegate { SetTab(MoreTab.Installed); };
        _tabExternal.Click += delegate { SetTab(MoreTab.External); };
        _tabs.Controls.Add(_tabInstalled);
        _tabs.Controls.Add(_tabExternal);
        _tabs.Paint += PaintTabs;
        _tabs.Resize += delegate { LayoutTabs(); SyncTabPillImmediate(); };

        _searchHost = new Panel
        {
            BackColor = MoreAppsTheme.Raised
        };
        _searchHost.Paint += PaintSearchHost;
        _search = new TextBox
        {
            BorderStyle = BorderStyle.None,
            BackColor = MoreAppsTheme.Raised,
            ForeColor = MoreAppsTheme.Text,
            Font = DialogUi.UiFont(9.5f, FontStyle.Regular)
        };
        try { _search.AutoCompleteMode = AutoCompleteMode.None; } catch { }
        _search.TextChanged += delegate { ApplyFilter(); };
        _search.GotFocus += delegate { _searchFocused = true; _searchHost.Invalidate(); };
        _search.LostFocus += delegate { _searchFocused = false; _searchHost.Invalidate(); };
        _searchHost.Controls.Add(_search);

        _header.Controls.Add(_title);
        _header.Controls.Add(_hint);
        _header.Controls.Add(_manage);
        _header.Controls.Add(_tabs);
        _header.Controls.Add(_searchHost);

        _grid = new AppIconGrid { Dock = DockStyle.Fill, IconSize = _dockIconSize };
        _grid.AccentColor = _pinAccent;
        _grid.BackColor = MoreAppsTheme.Bg;
        _grid.ItemClick += delegate(object s, AppGridItem item) { Launch(item); };
        _grid.PinClick += delegate(object s, AppGridItem item) { TogglePin(item); };

        _shell.Controls.Add(_grid);
        _shell.Controls.Add(_header);
        Controls.Add(_shell);

        KeyDown += delegate(object s, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                if (_search != null && !string.IsNullOrEmpty(_search.Text))
                {
                    _search.Text = "";
                    e.Handled = true;
                    return;
                }
                Close();
            }
        };
        Deactivate += delegate
        {
            if (_suspendClose != 0) return;
            var t = new Timer { Interval = 160 };
            t.Tick += delegate
            {
                t.Stop();
                t.Dispose();
                if (_suspendClose == 0 && !IsDisposed && Form.ActiveForm != this)
                    Close();
            };
            t.Start();
        };

        HandleCreated += delegate { Relayout(); };
        Load += delegate
        {
            Relayout();
            PaintTabState();
            SyncTabPillImmediate();
            ReloadGrid();
            try { _search.Focus(); } catch { }
        };
        Resize += delegate { LayoutChrome(); };
        Shown += delegate { BeginOpenAnimation(); };
    }

    private Label MakeTabLabel(string text)
    {
        return new Label
        {
            Text = text,
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand,
            BackColor = Color.Transparent,
            ForeColor = MoreAppsTheme.Muted,
            Font = MoreAppsTheme.TabFont(false)
        };
    }

    private void SetTab(MoreTab tab)
    {
        if (_tab == tab)
            return;
        _tab = tab;
        _sessionTab = tab;
        PaintTabState();
        ReloadGrid();
        BeginTabPillAnimation();
    }

    private void PaintTabState()
    {
        if (_tabInstalled == null || _tabExternal == null)
            return;
        bool installed = _tab == MoreTab.Installed;
        _tabInstalled.ForeColor = installed ? MoreAppsTheme.Text : MoreAppsTheme.Muted;
        _tabInstalled.Font = MoreAppsTheme.TabFont(installed);
        _tabExternal.ForeColor = !installed ? MoreAppsTheme.Text : MoreAppsTheme.Muted;
        _tabExternal.Font = MoreAppsTheme.TabFont(!installed);
        _hint.Text = installed
            ? "Installed apps on this PC · pin to add to dock"
            : "Web app tiles · pin to add to dock";
        if (_tabs != null)
            _tabs.Invalidate();
    }

    private void SyncTabPillImmediate()
    {
        Label active = _tab == MoreTab.Installed ? _tabInstalled : _tabExternal;
        if (active == null) return;
        _tabPillX = active.Left;
        _tabPillFrom = _tabPillX;
        _tabPillTo = _tabPillX;
        _tabAnimating = false;
        if (_tabs != null) _tabs.Invalidate();
    }

    private void BeginTabPillAnimation()
    {
        Label active = _tab == MoreTab.Installed ? _tabInstalled : _tabExternal;
        if (active == null) return;
        _tabPillFrom = _tabPillX;
        _tabPillTo = active.Left;
        _tabElapsed = 0;
        _tabAnimating = true;
        if (_tabTimer != null)
        {
            _tabTimer.Stop();
            _tabTimer.Dispose();
        }
        _tabTimer = new Timer { Interval = 16 };
        _tabTimer.Tick += delegate
        {
            _tabElapsed += 16;
            double t = Math.Min(1.0, _tabElapsed / (double)MoreAppsTheme.TabAnimMs);
            double e = MoreAppsTheme.Ease(t);
            _tabPillX = (float)(_tabPillFrom + (_tabPillTo - _tabPillFrom) * e);
            _tabs.Invalidate();
            if (t >= 1.0)
            {
                _tabAnimating = false;
                _tabPillX = _tabPillTo;
                _tabTimer.Stop();
                _tabTimer.Dispose();
                _tabTimer = null;
            }
        };
        _tabTimer.Start();
    }

    private void PaintTabs(object sender, PaintEventArgs e)
    {
        if (_tabs == null) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        int rad = DialogUi.Px(this, MoreAppsTheme.ChipCornerDip);
        using (var path = DockForm.RoundPath(new Rectangle(0, 0, Math.Max(1, _tabs.Width - 1), Math.Max(1, _tabs.Height - 1)), rad))
        {
            using (var b = new SolidBrush(MoreAppsTheme.Chip))
                e.Graphics.FillPath(b, path);
            using (var pen = new Pen(MoreAppsTheme.Border, 1f))
                e.Graphics.DrawPath(pen, path);
        }

        Label measure = _tabInstalled;
        if (measure == null) return;
        int pad = DialogUi.Px(this, 3);
        int pillW = Math.Max(20, measure.Width);
        int pillH = Math.Max(18, _tabs.ClientSize.Height - pad * 2);
        int pillX = (int)Math.Round(_tabPillX);
        if (!_tabAnimating)
        {
            Label active = _tab == MoreTab.Installed ? _tabInstalled : _tabExternal;
            if (active != null) pillX = active.Left;
        }
        Rectangle r = new Rectangle(pillX + 2, pad, pillW - 4, pillH);
        if (r.Width < 4 || r.Height < 4) return;
        using (var path = DockForm.RoundPath(r, DialogUi.Px(this, 6)))
        {
            using (var fill = new SolidBrush(MoreAppsTheme.Selected))
                e.Graphics.FillPath(fill, path);
            using (var pen = new Pen(MoreAppsTheme.Accent, 1.5f))
                e.Graphics.DrawPath(pen, path);
        }
    }

    private void PaintSearchHost(object sender, PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        int rad = Math.Max(4, _searchHost.Height / 2);
        using (var path = DockForm.RoundPath(new Rectangle(0, 0, Math.Max(1, _searchHost.Width - 1), Math.Max(1, _searchHost.Height - 1)), rad))
        {
            using (var b = new SolidBrush(MoreAppsTheme.Raised))
                e.Graphics.FillPath(b, path);
            using (var pen = new Pen(_searchFocused ? MoreAppsTheme.Accent : MoreAppsTheme.BorderSoft, _searchFocused ? 1.5f : 1f))
                e.Graphics.DrawPath(pen, path);
        }
    }

    private void LayoutTabs()
    {
        if (_tabs == null || _tabInstalled == null || _tabExternal == null)
            return;
        int pad = DialogUi.Px(this, 3);
        int half = Math.Max(20, (_tabs.ClientSize.Width - pad * 2) / 2);
        int h = Math.Max(20, _tabs.ClientSize.Height - pad * 2);
        _tabInstalled.SetBounds(pad, pad, half, h);
        _tabExternal.SetBounds(pad + half, pad, Math.Max(20, _tabs.ClientSize.Width - pad - half - pad), h);
        _tabInstalled.BringToFront();
        _tabExternal.BringToFront();
    }

    public void EnsureSized()
    {
        if (!IsHandleCreated)
            CreateHandle();
        Relayout();
    }

    private void Relayout()
    {
        _cornerRad = DialogUi.Px(this, MoreAppsTheme.CornerDip);
        int inset = _cornerRad + DialogUi.Px(this, 2);
        _shell.Dock = DockStyle.None;
        _shell.Location = new Point(inset, inset);
        // Larger centered panel (~0.72 × 0.78 work area, floor ~860×600 design).
        Size = DialogUi.LargeClient(this, 0.72, 0.78);
        if (ClientSize.Width < DialogUi.Px(this, 860))
        {
            var work = DialogUi.Work(this);
            int w = Math.Min(DialogUi.Px(this, 900), Math.Max(DialogUi.Px(this, 720), (int)(work.Width * 0.62)));
            int h = Math.Min(DialogUi.Px(this, 680), Math.Max(DialogUi.Px(this, 520), (int)(work.Height * 0.70)));
            ClientSize = new Size(w, h);
        }
        _shell.Size = new Size(
            Math.Max(40, ClientSize.Width - inset * 2),
            Math.Max(40, ClientSize.Height - inset * 2));

        _title.Font = MoreAppsTheme.TitleFont();
        _hint.Font = MoreAppsTheme.HintFont();
        _manage.Font = MoreAppsTheme.Semibold(9.5f);
        _search.Font = DialogUi.UiFont(9.5f, FontStyle.Regular);
        _title.AutoSize = true;
        _hint.AutoSize = true;
        _manage.AutoSize = true;

        int headH = DialogUi.Px(this, 12) + _title.PreferredHeight + DialogUi.Px(this, 4) +
                    DialogUi.Px(this, 32) + DialogUi.Px(this, 8) + DialogUi.Px(this, 34) + DialogUi.Px(this, 10);
        _header.Height = Math.Max(DialogUi.Px(this, 88), headH);
        _grid.IconSize = _dockIconSize;
        LayoutChrome();
        ApplyRegion();
        ApplySearchRegion();
    }

    private void LayoutChrome()
    {
        if (_header == null || _manage == null || _title == null || _shell == null || _search == null || _tabs == null || _searchHost == null)
            return;
        int inset = _cornerRad > 0 ? _cornerRad + DialogUi.Px(this, 2) : DialogUi.Px(this, 16);
        if (_shell.Dock == DockStyle.None)
        {
            _shell.Location = new Point(inset, inset);
            _shell.Size = new Size(
                Math.Max(40, ClientSize.Width - inset * 2),
                Math.Max(40, ClientSize.Height - inset * 2));
        }
        int pad = DialogUi.Px(this, 16);
        int y = DialogUi.Px(this, 10);
        int mw = _manage.PreferredWidth + DialogUi.Px(this, 4);
        int mh = _manage.PreferredHeight;
        _title.Location = new Point(pad, y);
        int mx = Math.Max(pad, _header.ClientSize.Width - mw - pad);
        _manage.Location = new Point(Math.Max(pad, mx), y + Math.Max(0, (_title.PreferredHeight - mh) / 2));
        _manage.BringToFront();
        y += _title.PreferredHeight + DialogUi.Px(this, 2);
        _hint.Location = new Point(pad, y);
        y += Math.Max(_hint.PreferredHeight, DialogUi.Px(this, 14)) + DialogUi.Px(this, 8);
        int tabH = DialogUi.Px(this, 32);
        _tabs.SetBounds(pad, y, Math.Max(40, _header.ClientSize.Width - pad * 2), tabH);
        LayoutTabs();
        y += tabH + DialogUi.Px(this, 8);
        int searchH = DialogUi.Px(this, 34);
        _searchHost.SetBounds(pad, y, Math.Max(40, _header.ClientSize.Width - pad * 2), searchH);
        int sPad = DialogUi.Px(this, 12);
        _search.SetBounds(sPad, (searchH - DialogUi.Px(this, 20)) / 2,
            Math.Max(20, _searchHost.ClientSize.Width - sPad * 2), DialogUi.Px(this, 20));
        ApplySearchRegion();
    }

    private void ApplySearchRegion()
    {
        if (_searchHost == null || _searchHost.Width < 8 || _searchHost.Height < 8)
            return;
        int rad = Math.Max(4, _searchHost.Height / 2);
        using (var path = DockForm.RoundPath(new Rectangle(0, 0, _searchHost.Width, _searchHost.Height), rad))
        {
            var old = _searchHost.Region;
            _searchHost.Region = new Region(path);
            if (old != null) old.Dispose();
        }
    }

    private void ApplyRegion()
    {
        if (Width < 8 || Height < 8) return;
        int rad = _cornerRad > 0 ? _cornerRad : DialogUi.Px(this, MoreAppsTheme.CornerDip);
        using (var path = DockForm.RoundPath(new Rectangle(0, 0, Width, Height), rad))
        {
            var old = Region;
            Region = new Region(path);
            if (old != null) old.Dispose();
        }
    }

    /// <summary>Center on working area; keep below the dock bar.</summary>
    public void PlaceOnScreen(Point near, Rectangle work)
    {
        if (Width < 80) Relayout();
        int dockClear = DialogUi.Px(this, 56);
        int x = work.Left + Math.Max(0, (work.Width - Width) / 2);
        int y = work.Top + Math.Max(0, (work.Height - Height) / 2);
        int minY = work.Top + dockClear;
        if (y < minY) y = minY;
        if (y + Height > work.Bottom)
            y = Math.Max(minY, work.Bottom - Height);
        if (x + Width > work.Right)
            x = Math.Max(work.Left, work.Right - Width);
        if (x < work.Left) x = work.Left;
        _restLocation = new Point(x, y);
        Location = _restLocation;
    }

    private void BeginOpenAnimation()
    {
        if (_openAnimating) return;
        int slide = DialogUi.Px(this, MoreAppsTheme.OpenSlideDip);
        _openToY = _restLocation.Y;
        _openFromY = _restLocation.Y - slide;
        Location = new Point(_restLocation.X, _openFromY);
        _openElapsed = 0;
        _openAnimating = true;
        if (_openTimer != null)
        {
            _openTimer.Stop();
            _openTimer.Dispose();
        }
        _openTimer = new Timer { Interval = 16 };
        _openTimer.Tick += delegate
        {
            _openElapsed += 16;
            double t = Math.Min(1.0, _openElapsed / (double)MoreAppsTheme.OpenAnimMs);
            double e = MoreAppsTheme.Ease(t);
            int y = (int)Math.Round(_openFromY + (_openToY - _openFromY) * e);
            Location = new Point(_restLocation.X, y);
            if (t >= 1.0)
            {
                _openAnimating = false;
                Location = _restLocation;
                _openTimer.Stop();
                _openTimer.Dispose();
                _openTimer = null;
            }
        };
        _openTimer.Start();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        LayoutChrome();
        ApplyRegion();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        int rad = _cornerRad > 0 ? _cornerRad : DialogUi.Px(this, MoreAppsTheme.CornerDip);
        using (var path = DockForm.RoundPath(new Rectangle(0, 0, Width - 1, Height - 1), rad))
        using (var pen = new Pen(MoreAppsTheme.Border, 1f))
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.DrawPath(pen, path);
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (_openTimer != null) { _openTimer.Stop(); _openTimer.Dispose(); _openTimer = null; }
        if (_tabTimer != null) { _tabTimer.Stop(); _tabTimer.Dispose(); _tabTimer = null; }
        base.OnFormClosed(e);
    }

    private void ReloadGrid()
    {
        var scan = AppCatalog.ScanCached();
        _allItems.Clear();
        int max = _cfg != null && _cfg.maxVisible > 0 ? _cfg.maxVisible : DockDefaults.DefaultMaxVisible;
        var onDock = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var overflow = new List<AppGridItem>();
        bool wantExternal = _tab == MoreTab.External;

        if (_cfg != null && _cfg.items != null)
        {
            for (int i = 0; i < _cfg.items.Count; i++)
            {
                if (_cfg.items[i] != null && !string.IsNullOrEmpty(_cfg.items[i].name))
                    onDock.Add(_cfg.items[i].name);
            }
            for (int i = max; i < _cfg.items.Count; i++)
            {
                var di = _cfg.items[i];
                if (di == null) continue;
                var cat = AppCatalog.FindByName(scan, di.name);
                if (cat == null)
                {
                    cat = new CatalogApp
                    {
                        Id = "name:" + di.name,
                        DisplayName = di.name,
                        Kind = "lnk",
                        LnkOrExe = DockConfigStore.Expand(di.lnk),
                        IconPath = DockConfigStore.Expand(di.icon),
                        Installed = true,
                        Group = AppCategories.Other,
                        Origin = "curated"
                    };
                }
                bool isExt = AppCatalog.IsExternal(cat);
                if (wantExternal != isExt)
                    continue;
                overflow.Add(MakeTile(cat, true));
            }
        }

        // Only show On dock when there is overflow — reclaim space otherwise.
        if (overflow.Count > 0)
        {
            _allItems.Add(new AppGridItem { IsSection = true, Caption = "On dock" });
            for (int i = 0; i < overflow.Count; i++)
                _allItems.Add(overflow[i]);
        }

        var byGroup = new Dictionary<string, List<AppGridItem>>(StringComparer.OrdinalIgnoreCase);
        var groupOrder = new List<string>();
        for (int i = 0; i < scan.Count; i++)
        {
            var cat = scan[i];
            if (cat == null || string.IsNullOrEmpty(cat.DisplayName))
                continue;
            bool isExt = AppCatalog.IsExternal(cat);
            if (wantExternal)
            {
                if (!isExt)
                    continue;
            }
            else
            {
                if (isExt || !cat.Installed)
                    continue;
            }

            if (onDock.Contains(cat.DisplayName))
            {
                int idx = IndexOfName(cat.DisplayName);
                if (idx >= 0 && idx < max) continue;
                if (idx >= max) continue;
            }

            string group = string.IsNullOrEmpty(cat.Group) ? AppCategories.Other : cat.Group;
            if (wantExternal)
                group = "Web apps";
            else
                group = AppCategories.Resolve(cat.DisplayName, cat.LnkOrExe, group);

            List<AppGridItem> bucket;
            if (!byGroup.TryGetValue(group, out bucket))
            {
                bucket = new List<AppGridItem>();
                byGroup[group] = bucket;
                groupOrder.Add(group);
            }
            bucket.Add(MakeTile(cat, onDock.Contains(cat.DisplayName)));
        }

        groupOrder.Sort(CompareGroupNames);
        int availableCount = 0;
        for (int g = 0; g < groupOrder.Count; g++)
        {
            List<AppGridItem> bucket = byGroup[groupOrder[g]];
            if (bucket == null || bucket.Count == 0)
                continue;
            bucket.Sort(CompareJumpThenName);
            _allItems.Add(new AppGridItem { IsSection = true, Caption = groupOrder[g] });
            for (int i = 0; i < bucket.Count; i++)
            {
                _allItems.Add(bucket[i]);
                availableCount++;
            }
        }

        if (availableCount == 0)
        {
            _allItems.Add(new AppGridItem
            {
                IsSection = true,
                Caption = wantExternal
                    ? "Web app tiles appear here"
                    : "No other installed apps found"
            });
        }

        ApplyFilter();
    }

    private static AppGridItem MakeTile(CatalogApp cat, bool pinned)
    {
        string jump = JumpLists.LabelOf(cat != null ? cat.DisplayName : null);
        return new AppGridItem
        {
            App = cat,
            Caption = cat != null ? cat.DisplayName : "",
            Subcaption = string.IsNullOrEmpty(jump) ? "" : jump,
            ShowPin = true,
            Pinned = pinned,
            SelectMode = false
        };
    }

    private static int CompareGroupNames(string a, string b)
    {
        int c = AppCategories.Rank(a).CompareTo(AppCategories.Rank(b));
        if (c != 0) return c;
        return string.Compare(a ?? "", b ?? "", StringComparison.OrdinalIgnoreCase);
    }

    private static int CompareJumpThenName(AppGridItem a, AppGridItem b)
    {
        bool ja = a != null && a.App != null && JumpLists.Supports(a.App.DisplayName);
        bool jb = b != null && b.App != null && JumpLists.Supports(b.App.DisplayName);
        if (ja != jb)
            return ja ? -1 : 1;
        string na = a != null ? (a.Caption ?? "") : "";
        string nb = b != null ? (b.Caption ?? "") : "";
        return string.Compare(na, nb, StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyFilter()
    {
        string q = _search != null ? (_search.Text ?? "").Trim() : "";
        if (q.Length == 0)
        {
            _grid.SetItems(_allItems);
            return;
        }
        var filtered = new List<AppGridItem>();
        string section = null;
        for (int i = 0; i < _allItems.Count; i++)
        {
            AppGridItem it = _allItems[i];
            if (it == null) continue;
            if (it.IsSection)
            {
                section = it.Caption;
                continue;
            }
            string cap = it.Caption ?? "";
            string sub = it.Subcaption ?? "";
            if (cap.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0 &&
                sub.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            if (section != null)
            {
                filtered.Add(new AppGridItem { IsSection = true, Caption = section });
                section = null;
            }
            filtered.Add(it);
        }
        _grid.SetItems(filtered);
    }

    private int IndexOfName(string name)
    {
        if (_cfg == null || _cfg.items == null) return -1;
        for (int i = 0; i < _cfg.items.Count; i++)
        {
            if (_cfg.items[i] != null &&
                string.Equals(_cfg.items[i].name, name, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    private void Launch(AppGridItem item)
    {
        if (item == null || item.App == null || item.IsSection) return;
        if (string.Equals(item.App.Kind, "chromeApp", StringComparison.OrdinalIgnoreCase))
            AppCatalog.EnsureChromeApp(item.App);
        string lnk = item.App.LnkOrExe;
        if (!string.IsNullOrEmpty(lnk) && lnk.IndexOf('\n') >= 0)
            lnk = lnk.Split('\n')[0];
        try
        {
            if (!string.IsNullOrEmpty(lnk) && File.Exists(lnk))
                Process.Start(new ProcessStartInfo { FileName = lnk, UseShellExecute = true });
        }
        catch { }
    }

    private void TogglePin(AppGridItem item)
    {
        if (item == null || item.App == null || item.IsSection || _cfg == null) return;
        AppCatalog.TogglePin(_cfg, item.App, _configPath);
        if (_host != null)
            _host.RefreshPins();
        ReloadGrid();
    }
}
