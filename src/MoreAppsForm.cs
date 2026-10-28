// JumpDock — More apps panel (inset past rounded corners; icons = dock size).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

public sealed class MoreAppsForm : Form
{
    private readonly DockForm _host;
    private readonly DockConfig _cfg;
    private readonly string _configPath;
    private readonly int _dockIconSize;
    private readonly AppIconGrid _grid;
    private readonly Panel _shell;
    private readonly Panel _header;
    private readonly Label _title;
    private readonly Label _manage;
    private int _suspendClose;
    private int _cornerRad;

    public MoreAppsForm(DockForm host, DockConfig cfg, string configPath, int dockIconSize)
    {
        _host = host;
        _cfg = cfg;
        _configPath = configPath;
        _dockIconSize = dockIconSize > 0 ? dockIconSize : (cfg != null && cfg.iconSize > 0 ? cfg.iconSize : 96);

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.FromArgb(255, 32, 34, 38);
        AutoScaleMode = AutoScaleMode.None;
        KeyPreview = true;
        // Outer pad keeps chrome inside the rounded Region (prevents half-clipped Manage).
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
        // Label link — AutoSize so "Manage" is never clipped inside a too-small Button.
        _manage = new Label
        {
            Text = "Manage",
            ForeColor = Color.FromArgb(255, 152, 196, 236),
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
                    _host.OpenAppPicker();
                Close();
            }
            finally { _suspendClose--; }
        };
        _manage.MouseEnter += delegate { _manage.ForeColor = Color.FromArgb(255, 190, 220, 250); };
        _manage.MouseLeave += delegate { _manage.ForeColor = Color.FromArgb(255, 152, 196, 236); };

        _header.Controls.Add(_title);
        _header.Controls.Add(_manage);

        _grid = new AppIconGrid { Dock = DockStyle.Fill, IconSize = _dockIconSize };
        _grid.ItemClick += delegate(object s, AppGridItem item) { Launch(item); };
        _grid.PinClick += delegate(object s, AppGridItem item) { TogglePin(item); };

        _shell.Controls.Add(_grid);
        _shell.Controls.Add(_header);
        Controls.Add(_shell);

        KeyDown += delegate(object s, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) Close();
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
            ReloadGrid();
        };
        Resize += delegate { LayoutChrome(); };
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
        // Inset shell so nothing sits in the rounded corner (Region clips there).
        int inset = _cornerRad + DialogUi.Px(this, 2);
        _shell.Dock = DockStyle.None;
        _shell.Location = new Point(inset, inset);
        Size = DialogUi.FitClient(this, 640, 520, 0.55, 0.70);
        _shell.Size = new Size(
            Math.Max(40, ClientSize.Width - inset * 2),
            Math.Max(40, ClientSize.Height - inset * 2));

        _title.Font = DialogUi.UiFont(10.5f, FontStyle.Regular);
        _manage.Font = DialogUi.UiFont(9.5f, FontStyle.Regular);
        // Force AutoSize measure after font change
        _title.AutoSize = true;
        _manage.AutoSize = true;

        int headH = Math.Max(_title.PreferredHeight, _manage.PreferredHeight) + DialogUi.Px(this, 16);
        _header.Height = Math.Max(DialogUi.Px(this, 36), headH);
        _grid.IconSize = _dockIconSize;
        LayoutChrome();
        ApplyRegion();
    }

    private void LayoutChrome()
    {
        if (_header == null || _manage == null || _title == null || _shell == null)
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
        int th = _title.PreferredHeight;
        int mh = _manage.PreferredHeight;
        int mw = _manage.PreferredWidth + DialogUi.Px(this, 4);
        _title.Location = new Point(pad, Math.Max(0, (_header.Height - th) / 2));
        int mx = Math.Max(pad, _header.ClientSize.Width - mw - pad);
        // Keep Manage left of title if overlap (narrow width)
        if (mx < _title.Right + pad)
            mx = Math.Min(_header.ClientSize.Width - mw - pad, _title.Right + pad);
        _manage.Location = new Point(Math.Max(pad, mx), Math.Max(0, (_header.Height - mh) / 2));
        _manage.BringToFront();
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
        DialogUi.PlaceNear(this, near, work);
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
        // Soft border inside the rounded region
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
        var scan = AppCatalog.Scan();
        var items = new List<AppGridItem>();
        int max = _cfg != null && _cfg.maxVisible > 0 ? _cfg.maxVisible : DockDefaults.DefaultMaxVisible;
        var onDock = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
                        Group = "dock"
                    };
                }
                items.Add(new AppGridItem
                {
                    App = cat,
                    Caption = cat.DisplayName,
                    ShowPin = true,
                    Pinned = true,
                    SelectMode = false
                });
            }
        }
        for (int i = 0; i < scan.Count; i++)
        {
            var cat = scan[i];
            if (cat == null || !cat.Installed) continue;
            if (onDock.Contains(cat.DisplayName))
            {
                int idx = IndexOfName(cat.DisplayName);
                if (idx >= 0 && idx < max) continue;
                if (idx >= max) continue;
            }
            items.Add(new AppGridItem
            {
                App = cat,
                Caption = cat.DisplayName,
                ShowPin = true,
                Pinned = onDock.Contains(cat.DisplayName),
                SelectMode = false
            });
        }
        _grid.SetItems(items);
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
        if (item == null || item.App == null) return;
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
        if (item == null || item.App == null || _cfg == null) return;
        AppCatalog.TogglePin(_cfg, item.App, _configPath);
        if (_host != null)
            _host.ReloadFromDisk();
        ReloadGrid();
    }
}
