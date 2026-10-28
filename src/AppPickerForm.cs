// JumpDock — Manage apps (large window; same icon/text size; no clipped chrome).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

public sealed class AppPickerForm : Form
{
    private readonly DockConfig _cfg;
    private readonly string _configPath;
    private readonly int _dockIconSize;
    private readonly AppIconGrid _grid;
    private readonly Panel _top;
    private readonly Label _header;
    private readonly Label _hint;
    private readonly Panel _footer;
    private readonly Button _apply;
    private readonly Button _cancel;
    private readonly bool _firstRun;

    public bool Applied { get; private set; }

    public AppPickerForm(DockConfig cfg, string configPath, bool firstRun, int dockIconSize)
    {
        _cfg = cfg ?? new DockConfig();
        _configPath = configPath;
        _firstRun = firstRun;
        _dockIconSize = dockIconSize > 0 ? dockIconSize : (_cfg.iconSize > 0 ? _cfg.iconSize : 96);
        Applied = false;

        Text = firstRun ? "Choose apps for JumpDock" : "Manage dock apps";
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = true;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.FromArgb(255, 28, 30, 34);
        ForeColor = Color.FromArgb(255, 236, 238, 242);
        Font = DialogUi.UiFont(9.5f, FontStyle.Regular);
        TopMost = true;

        _top = new Panel
        {
            Dock = DockStyle.Top,
            BackColor = BackColor,
            Padding = new Padding(0)
        };
        _header = new Label
        {
            Text = firstRun
                ? "Select apps to pin. Web apps use Chrome (Default profile)."
                : "Check apps to keep on the dock. Uncheck to remove.",
            ForeColor = Color.FromArgb(255, 200, 204, 210),
            Font = DialogUi.UiFont(9.5f, FontStyle.Regular),
            AutoSize = false,
            AutoEllipsis = false
        };
        _hint = new Label
        {
            Text = "Tap icons to toggle  ·  P = on dock",
            ForeColor = Color.FromArgb(255, 152, 158, 168),
            Font = DialogUi.UiFont(8.5f, FontStyle.Regular),
            AutoSize = false,
            AutoEllipsis = false
        };
        _top.Controls.Add(_header);
        _top.Controls.Add(_hint);

        _apply = DialogUi.PrimaryButton("Apply");
        _cancel = DialogUi.SecondaryButton(firstRun ? "Skip" : "Cancel");
        _apply.Click += delegate { DoApply(); };
        _cancel.Click += delegate
        {
            if (_firstRun)
            {
                _cfg.setupComplete = true;
                DockConfigStore.Save(_cfg, _configPath);
            }
            DialogResult = DialogResult.Cancel;
            Close();
        };
        _footer = DialogUi.MakeFooter(this, _apply, _cancel);

        _grid = new AppIconGrid { Dock = DockStyle.Fill, IconSize = _dockIconSize };
        _grid.CheckChanged += delegate { UpdateCount(); };

        // Dock order: Fill first, then chrome
        Controls.Add(_grid);
        Controls.Add(_footer);
        Controls.Add(_top);

        AcceptButton = _apply;
        CancelButton = _cancel;

        Load += delegate { Relayout(); LoadGrid(); };
        Shown += delegate { Relayout(); CenterToScreen(); };
        Resize += delegate { LayoutTop(); };
    }

    private void Relayout()
    {
        // Same icon / text size — window ~half of prior near-fullscreen size.
        DialogUi.SizeFooter(this, _footer, _apply, _cancel);
        LayoutTop();

        // ~45% of working area (half of the previous ~90%); chrome sizes unchanged
        ClientSize = DialogUi.LargeClient(this, 0.48, 0.50);
        MinimumSize = new Size(DialogUi.Px(this, 560), DialogUi.Px(this, 420));
        _grid.IconSize = _dockIconSize;
    }

    private void LayoutTop()
    {
        int padX = DialogUi.Px(this, 20);
        int padY = DialogUi.Px(this, 14);
        int gap = DialogUi.Px(this, 6);

        _header.Font = DialogUi.UiFont(9.5f, FontStyle.Regular);
        _hint.Font = DialogUi.UiFont(8.5f, FontStyle.Regular);

        int line1 = Math.Max(_header.Font.Height + DialogUi.Px(this, 4), DialogUi.Px(this, 20));
        int line2 = Math.Max(_hint.Font.Height + DialogUi.Px(this, 4), DialogUi.Px(this, 18));
        int topH = padY + line1 + gap + line2 + padY;
        _top.Height = topH;
        _top.MinimumSize = new Size(0, topH);

        int innerW = Math.Max(40, _top.ClientSize.Width - padX * 2);
        _header.SetBounds(padX, padY, innerW, line1);
        _hint.SetBounds(padX, padY + line1 + gap, innerW, line2);
    }

    private void LoadGrid()
    {
        var scan = AppCatalog.Scan();
        var items = new List<AppGridItem>();
        for (int i = 0; i < scan.Count; i++)
        {
            var a = scan[i];
            if (a == null || !a.Installed)
                continue;
            items.Add(MakeSelectable(a));
        }
        _grid.SetItems(items);
        UpdateCount();
    }

    private AppGridItem MakeSelectable(CatalogApp a)
    {
        bool on = AppCatalog.IsOnDock(_cfg, a.DisplayName);
        return new AppGridItem
        {
            App = a,
            Caption = a.DisplayName,
            SelectMode = true,
            ShowPin = true,
            Checked = on,
            Pinned = on
        };
    }

    private void UpdateCount()
    {
        int n = 0;
        var items = _grid.GetItems();
        for (int i = 0; i < items.Count; i++)
            if (items[i] != null && items[i].Checked) n++;
        _hint.Text = "Selected " + n + "  ·  Tap icons to toggle  ·  P = on dock";
    }

    private void DoApply()
    {
        var selected = new List<string>();
        var items = _grid.GetItems();
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null && items[i].Checked && items[i].App != null)
                selected.Add(items[i].App.Id);
        }
        AppCatalog.ApplySelection(_cfg, selected, _configPath);
        Applied = true;
        DialogResult = DialogResult.OK;
        Close();
    }
}
