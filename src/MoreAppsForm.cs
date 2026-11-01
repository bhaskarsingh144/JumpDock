// JumpDock — More apps panel (Installed / External; inset past rounded corners).
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

    /// <summary>Session-only last tab (survives reopen within the same process).</summary>
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
    private readonly TextBox _search;
    private readonly List<AppGridItem> _allItems = new List<AppGridItem>();
    private MoreTab _tab;
    private Color _accent;
    private int _suspendClose;
    private int _cornerRad;

    public MoreAppsForm(DockForm host, DockConfig cfg, string configPath, int dockIconSize)
    {
        _host = host;
        _cfg = cfg;
        _configPath = configPath;
        _dockIconSize = dockIconSize > 0 ? dockIconSize : (cfg != null && cfg.iconSize > 0 ? cfg.iconSize : 96);
        _tab = _sessionTab;
        _accent = DockAccent.FromConfig(cfg);

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.FromArgb(255, 32, 34, 38);
        AutoScaleMode = AutoScaleMode.None;
        KeyPreview = true;
        Padding = new Padding(0);

        _shell = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(255, 22, 24, 28) };

        _header = new Panel
        {
            Dock = DockStyle.Top,
            BackColor = Color.FromArgb(255, 22, 24, 28)
        };
        _title = new Label
        {
            Text = "More apps",
            ForeColor = Color.FromArgb(255, 242, 244, 248),
            Font = DialogUi.UiFont(10.5f, FontStyle.Regular),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        _hint = new Label
        {
            Text = "Click to open · pin to add to dock",
            ForeColor = Color.FromArgb(255, 152, 158, 168),
            Font = DialogUi.UiFont(8.5f, FontStyle.Regular),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        _manage = new Label
        {
            Text = "Settings",
            ForeColor = _accent,
            Font = DialogUi.UiFont(9.5f, FontStyle.Regular),
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
                Math.Min(255, _accent.R + 40),
                Math.Min(255, _accent.G + 40),
                Math.Min(255, _accent.B + 40));
        };
        _manage.MouseLeave += delegate { _manage.ForeColor = _accent; };

        _tabs = new Panel
        {
            BackColor = Color.FromArgb(255, 34, 36, 42)
        };
        _tabInstalled = MakeTabLabel("Installed");
        _tabExternal = MakeTabLabel("External");
        _tabInstalled.Click += delegate { SetTab(MoreTab.Installed); };
        _tabExternal.Click += delegate { SetTab(MoreTab.External); };
        _tabs.Controls.Add(_tabInstalled);
        _tabs.Controls.Add(_tabExternal);
        _tabs.Paint += PaintTabs;
        _tabs.Resize += delegate { LayoutTabs(); };

        _search = new TextBox
        {
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(255, 40, 44, 50),
            ForeColor = Color.FromArgb(255, 236, 238, 242),
            Font = DialogUi.UiFont(9.5f, FontStyle.Regular)
        };
        try { _search.AutoCompleteMode = AutoCompleteMode.None; } catch { }
        _search.TextChanged += delegate { ApplyFilter(); };

        _header.Controls.Add(_title);
        _header.Controls.Add(_hint);
        _header.Controls.Add(_manage);
        _header.Controls.Add(_tabs);
        _header.Controls.Add(_search);

        _grid = new AppIconGrid { Dock = DockStyle.Fill, IconSize = _dockIconSize };
        _grid.AccentColor = _accent;
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
            var t = new Timer { Interval = 180 };
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
            ReloadGrid();
            try { _search.Focus(); } catch { }
        };
        Resize += delegate { LayoutChrome(); };
    }

    private Label MakeTabLabel(string text)
    {
        return new Label
        {
            Text = text,
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand,
            BackColor = Color.Transparent,
            ForeColor = Color.FromArgb(255, 152, 158, 168),
            Font = DialogUi.UiFont(9f, FontStyle.Regular)
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
    }

    private void PaintTabState()
    {
        if (_tabInstalled == null || _tabExternal == null)
            return;
        Color muted = Color.FromArgb(255, 152, 158, 168);
        Color fg = Color.FromArgb(255, 242, 244, 248);
        bool installed = _tab == MoreTab.Installed;
        _tabInstalled.ForeColor = installed ? fg : muted;
        _tabInstalled.Font = DialogUi.UiFont(9f, installed ? FontStyle.Bold : FontStyle.Regular);
        _tabExternal.ForeColor = !installed ? fg : muted;
        _tabExternal.Font = DialogUi.UiFont(9f, !installed ? FontStyle.Bold : FontStyle.Regular);
        _hint.Text = installed
            ? "Installed apps on this PC · pin to add to dock"
            : "Web app tiles · pin to add to dock";
        if (_tabs != null)
            _tabs.Invalidate();
    }

    private void PaintTabs(object sender, PaintEventArgs e)
    {
        if (_tabs == null) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        int rad = DialogUi.Px(this, 8);
        using (var path = DockForm.RoundPath(new Rectangle(0, 0, Math.Max(1, _tabs.Width - 1), Math.Max(1, _tabs.Height - 1)), rad))
        {
            using (var b = new SolidBrush(Color.FromArgb(255, 34, 36, 42)))
                e.Graphics.FillPath(b, path);
            using (var pen = new Pen(Color.FromArgb(255, 56, 60, 68), 1f))
                e.Graphics.DrawPath(pen, path);
        }
        Label active = _tab == MoreTab.Installed ? _tabInstalled : _tabExternal;
        if (active == null) return;
        Rectangle r = active.Bounds;
        r.Inflate(-2, -2);
        if (r.Width < 4 || r.Height < 4) return;
        using (var path = DockForm.RoundPath(r, DialogUi.Px(this, 6)))
        {
            using (var fill = new SolidBrush(Color.FromArgb(55, _accent.R, _accent.G, _accent.B)))
                e.Graphics.FillPath(fill, path);
            using (var pen = new Pen(Color.FromArgb(160, _accent.R, _accent.G, _accent.B), 1f))
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
        _cornerRad = DialogUi.Px(this, 14);
        int inset = _cornerRad + DialogUi.Px(this, 2);
        _shell.Dock = DockStyle.None;
        _shell.Location = new Point(inset, inset);
        Size = DialogUi.FitClient(this, 640, 560, 0.55, 0.72);
        _shell.Size = new Size(
            Math.Max(40, ClientSize.Width - inset * 2),
            Math.Max(40, ClientSize.Height - inset * 2));

        _title.Font = DialogUi.UiFont(10.5f, FontStyle.Regular);
        _hint.Font = DialogUi.UiFont(8.5f, FontStyle.Regular);
        _manage.Font = DialogUi.UiFont(9.5f, FontStyle.Regular);
        _search.Font = DialogUi.UiFont(9.5f, FontStyle.Regular);
        _title.AutoSize = true;
        _hint.AutoSize = true;
        _manage.AutoSize = true;

        int headH = DialogUi.Px(this, 16) + _title.PreferredHeight + DialogUi.Px(this, 2) +
                    _hint.PreferredHeight + DialogUi.Px(this, 8) + DialogUi.Px(this, 30) +
                    DialogUi.Px(this, 8) + DialogUi.Px(this, 26) + DialogUi.Px(this, 10);
        _header.Height = Math.Max(DialogUi.Px(this, 108), headH);
        _grid.IconSize = _dockIconSize;
        LayoutChrome();
        ApplyRegion();
    }

    private void LayoutChrome()
    {
        if (_header == null || _manage == null || _title == null || _shell == null || _search == null || _tabs == null)
            return;
        int inset = _cornerRad > 0 ? _cornerRad + DialogUi.Px(this, 2) : DialogUi.Px(this, 16);
        if (_shell.Dock == DockStyle.None)
        {
            _shell.Location = new Point(inset, inset);
            _shell.Size = new Size(
                Math.Max(40, ClientSize.Width - inset * 2),
                Math.Max(40, ClientSize.Height - inset * 2));
        }
        int pad = DialogUi.Px(this, 8);
        int y = pad;
        int mw = _manage.PreferredWidth + DialogUi.Px(this, 4);
        int mh = _manage.PreferredHeight;
        _title.Location = new Point(pad, y);
        int mx = Math.Max(pad, _header.ClientSize.Width - mw - pad);
        _manage.Location = new Point(Math.Max(pad, mx), y);
        _manage.BringToFront();
        y += Math.Max(_title.PreferredHeight, mh) + DialogUi.Px(this, 2);
        _hint.Location = new Point(pad, y);
        y += _hint.PreferredHeight + DialogUi.Px(this, 8);
        int tabH = DialogUi.Px(this, 30);
        _tabs.SetBounds(pad, y, Math.Max(40, _header.ClientSize.Width - pad * 2), tabH);
        LayoutTabs();
        y += tabH + DialogUi.Px(this, 8);
        int searchH = DialogUi.Px(this, 26);
        _search.SetBounds(pad, y, Math.Max(40, _header.ClientSize.Width - pad * 2), searchH);
    }

    private void ApplyRegion()
    {
        if (Width < 8 || Height < 8) return;
        int rad = _cornerRad > 0 ? _cornerRad : DialogUi.Px(this, 14);
        using (var path = DockForm.RoundPath(new Rectangle(0, 0, Width, Height), rad))
        {
            var old = Region;
            Region = new Region(path);
            if (old != null) old.Dispose();
        }
    }

    public void PlaceOnScreen(Point near, Rectangle work)
    {
        if (Width < 80) Relayout();
        Point anchor = near;
        int clear = DialogUi.Px(this, 40);
        Rectangle screen = Screen.FromPoint(near).Bounds;
        if (near.X > screen.Right - Width - clear)
            anchor = new Point(Math.Max(work.Left, screen.Right - Width - clear), near.Y);
        DialogUi.PlaceNear(this, anchor, work);
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
        int rad = _cornerRad > 0 ? _cornerRad : DialogUi.Px(this, 14);
        using (var path = DockForm.RoundPath(new Rectangle(0, 0, Width - 1, Height - 1), rad))
        using (var pen = new Pen(Color.FromArgb(255, 68, 72, 80), 1f))
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.DrawPath(pen, path);
        }
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
                        Group = "dock",
                        Origin = "curated"
                    };
                }
                bool isExt = AppCatalog.IsExternal(cat);
                if (wantExternal != isExt)
                    continue;
                overflow.Add(MakeTile(cat, true));
            }
        }

        _allItems.Add(new AppGridItem { IsSection = true, Caption = "On dock" });
        if (overflow.Count == 0)
        {
            _allItems.Add(new AppGridItem
            {
                IsSection = true,
                Caption = "All pins fit on the bar"
            });
        }
        else
        {
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

            string group = string.IsNullOrEmpty(cat.Group) ? "Other" : cat.Group;
            if (wantExternal)
                group = "Web apps";
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
        int c = AppCatalog.CategoryRankPublic(a).CompareTo(AppCatalog.CategoryRankPublic(b));
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
