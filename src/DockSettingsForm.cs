// JumpDock — Settings hub with left nav (Appearance / Dock / Apps / Chrome).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

public sealed class DockSettingsForm : Form
{
    private static readonly Color NavBack = Color.FromArgb(255, 22, 24, 28);
    private static readonly Color PageBack = Color.FromArgb(255, 28, 30, 34);
    private static readonly Color Muted = Color.FromArgb(255, 152, 158, 168);
    private static readonly Color FieldBack = Color.FromArgb(255, 40, 44, 50);

    private readonly Panel _shell;
    private readonly Panel _nav;
    private readonly Panel _content;
    private readonly Panel _footer;
    private readonly ListBox _navList;
    private readonly Panel[] _pages = new Panel[4];
    private readonly Label[] _pageTitles = new Label[4];
    private readonly Label[] _pageSubs = new Label[4];

    private readonly ComboBox _iconSize;
    private readonly ComboBox _moreIconSize;
    private readonly ComboBox _accent;
    private readonly ComboBox _menuScale;
    private readonly Label _lblIcon;
    private readonly Label _lblMore;
    private readonly Label _lblAccent;
    private readonly Label _lblMenu;

    private readonly ComboBox _edge;
    private readonly NumericUpDown _peek;
    private readonly NumericUpDown _maxVisible;
    private readonly CheckBox _startWithWindows;
    private readonly Label _monitorNote;
    private readonly Label _lblEdge;
    private readonly Label _lblPeek;
    private readonly Label _lblMax;

    private readonly Label _appsHint;
    private readonly ListBox _pinList;
    private readonly Button _pinUp;
    private readonly Button _pinDown;
    private readonly Button _pinRemove;
    private readonly Button _addCatalog;
    private readonly Button _addFile;
    private List<DockItem> _items;

    private readonly ListBox _hubList;
    private readonly TextBox _hubTitle;
    private readonly TextBox _hubUrl;
    private readonly Button _hubAdd;
    private readonly Button _hubRemove;
    private readonly Label _hubHint;
    private List<HubPin> _hubPins;

    private readonly Button _apply;
    private readonly Button _cancel;
    private readonly bool _openApps;
    private readonly bool _firstRunHint;
    private readonly Color _accentColor;
    private bool _sized;
    private int _page;

    public DockConfig Result { get; private set; }

    public DockSettingsForm(DockConfig current)
        : this(current, false, false)
    {
    }

    public DockSettingsForm(DockConfig current, bool selectAppsTab, bool firstRunHint)
    {
        var src = current ?? new DockConfig();
        DockDefaults.Normalize(src);
        _hubPins = CloneHubs(src.hubPins);
        _items = CloneItems(src.items);
        _openApps = selectAppsTab;
        _firstRunHint = firstRunHint;
        _accentColor = DockAccent.FromConfig(src);

        Text = "JumpDock Settings";
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = PageBack;
        ForeColor = Color.FromArgb(255, 236, 238, 242);
        Font = DialogUi.UiFont(9.5f, FontStyle.Regular);

        _apply = DialogUi.PrimaryButton("Apply");
        _cancel = DialogUi.SecondaryButton("Cancel");
        _cancel.DialogResult = DialogResult.Cancel;
        _apply.Click += delegate { DoApply(); };
        _footer = DialogUi.MakeFooter(this, _apply, _cancel);

        _shell = new Panel { Dock = DockStyle.Fill, BackColor = PageBack };
        _nav = new Panel { BackColor = NavBack };
        _content = new Panel { BackColor = PageBack };

        _navList = new ListBox
        {
            BorderStyle = BorderStyle.None,
            BackColor = NavBack,
            ForeColor = ForeColor,
            Font = DialogUi.UiFont(10f, FontStyle.Regular),
            DrawMode = DrawMode.OwnerDrawFixed,
            IntegralHeight = false,
            ItemHeight = 40
        };
        _navList.Items.AddRange(new object[] { "Appearance", "Dock", "Apps", "Chrome" });
        _navList.DrawItem += NavDrawItem;
        _navList.SelectedIndexChanged += delegate
        {
            if (_navList.SelectedIndex >= 0)
                ShowPage(_navList.SelectedIndex);
        };
        _nav.Controls.Add(_navList);

        for (int i = 0; i < 4; i++)
        {
            _pages[i] = new Panel { BackColor = PageBack, Visible = false };
            _pageTitles[i] = MakeLabel("");
            _pageTitles[i].Font = DialogUi.UiFont(14f, FontStyle.Regular);
            _pageTitles[i].ForeColor = ForeColor;
            _pageSubs[i] = MakeLabel("");
            _pageSubs[i].ForeColor = Muted;
            _pageSubs[i].Font = DialogUi.UiFont(8.5f, FontStyle.Regular);
            _pages[i].Controls.Add(_pageTitles[i]);
            _pages[i].Controls.Add(_pageSubs[i]);
            _content.Controls.Add(_pages[i]);
        }
        _pageTitles[0].Text = "Appearance";
        _pageSubs[0].Text = "Dock zoom, More apps density, accent, and jump menu size.";
        _pageTitles[1].Text = "Dock";
        _pageSubs[1].Text = "Edge, peek strip, how many pins fit on the bar.";
        _pageTitles[2].Text = "Apps";
        _pageSubs[2].Text = _firstRunHint
            ? "Starter apps are pinned. Add more here, or pin from More on the bar."
            : "Reorder pins here, or pin quickly from More on the bar.";
        _pageTitles[3].Text = "Chrome";
        _pageSubs[3].Text = "Hub links that appear in the Chrome jump menu.";

        // Appearance fields
        _lblIcon = MakeLabel("Dock icon size (zoom)");
        _iconSize = MakeCombo();
        _iconSize.Items.AddRange(new object[] { 72, 84, 96, 108, 120, 128 });
        SelectInt(_iconSize, src.iconSize, 96);

        _lblMore = MakeLabel("More apps icons");
        _moreIconSize = MakeCombo();
        _moreIconSize.Items.AddRange(new object[] { 48, 56, 64, 72, 84, 96 });
        SelectInt(_moreIconSize, src.moreIconSize > 0 ? src.moreIconSize : DockDefaults.DefaultMoreIconSize, 64);

        _lblAccent = MakeLabel("Accent");
        _accent = MakeCombo();
        _accent.Items.AddRange(new object[] { "blue", "teal", "amber", "gray" });
        SelectString(_accent, DockAccent.NormalizeKey(src.accent), "blue");

        _lblMenu = MakeLabel("Jump menu scale");
        _menuScale = MakeCombo();
        _menuScale.Items.Add("Normal (1.0)");
        _menuScale.Items.Add("Large (1.15)");
        _menuScale.SelectedIndex = src.menuScale < 1.07 ? 0 : 1;

        _pages[0].Controls.Add(_lblIcon);
        _pages[0].Controls.Add(_iconSize);
        _pages[0].Controls.Add(_lblMore);
        _pages[0].Controls.Add(_moreIconSize);
        _pages[0].Controls.Add(_lblAccent);
        _pages[0].Controls.Add(_accent);
        _pages[0].Controls.Add(_lblMenu);
        _pages[0].Controls.Add(_menuScale);

        // Dock
        _lblEdge = MakeLabel("Edge");
        _edge = MakeCombo();
        _edge.Items.AddRange(new object[] { "top", "bottom" });
        _edge.SelectedItem = string.Equals(src.edge, "bottom", StringComparison.OrdinalIgnoreCase) ? "bottom" : "top";

        _lblPeek = MakeLabel("Peek (design px)");
        _peek = MakeNum(0, 24, src.peek);

        _lblMax = MakeLabel("Max visible on bar");
        _maxVisible = MakeNum(8, 24, src.maxVisible);

        _monitorNote = MakeLabel("Monitor: Primary (only)");
        _monitorNote.ForeColor = Muted;
        _startWithWindows = new CheckBox
        {
            Text = "Start with Windows",
            ForeColor = ForeColor,
            Font = Font,
            AutoSize = true,
            Checked = DockStartup.IsEnabled(),
            BackColor = PageBack
        };

        _pages[1].Controls.Add(_lblEdge);
        _pages[1].Controls.Add(_edge);
        _pages[1].Controls.Add(_lblPeek);
        _pages[1].Controls.Add(_peek);
        _pages[1].Controls.Add(_lblMax);
        _pages[1].Controls.Add(_maxVisible);
        _pages[1].Controls.Add(_monitorNote);
        _pages[1].Controls.Add(_startWithWindows);

        // Apps
        _appsHint = MakeLabel("Tip: pin quickly from More on the bar.");
        _appsHint.ForeColor = Muted;
        _pinList = new ListBox
        {
            BackColor = FieldBack,
            ForeColor = ForeColor,
            Font = Font,
            BorderStyle = BorderStyle.FixedSingle,
            IntegralHeight = false
        };
        _pinUp = DialogUi.SecondaryButton("Up");
        _pinDown = DialogUi.SecondaryButton("Down");
        _pinRemove = DialogUi.SecondaryButton("Remove");
        _addCatalog = DialogUi.SecondaryButton("Add from catalog…");
        _addFile = DialogUi.SecondaryButton("Add app…");
        _pinUp.Click += delegate { PinMove(-1); };
        _pinDown.Click += delegate { PinMove(1); };
        _pinRemove.Click += delegate { PinRemove(); };
        _addCatalog.Click += delegate { PinAddCatalog(); };
        _addFile.Click += delegate { PinAddFile(); };
        RefreshPinList();

        _pages[2].Controls.Add(_appsHint);
        _pages[2].Controls.Add(_pinList);
        _pages[2].Controls.Add(_pinUp);
        _pages[2].Controls.Add(_pinDown);
        _pages[2].Controls.Add(_pinRemove);
        _pages[2].Controls.Add(_addCatalog);
        _pages[2].Controls.Add(_addFile);

        // Chrome
        _hubHint = MakeLabel("Add a title and URL, then Add.");
        _hubHint.ForeColor = Muted;
        _hubList = new ListBox
        {
            BackColor = FieldBack,
            ForeColor = ForeColor,
            Font = Font,
            BorderStyle = BorderStyle.FixedSingle,
            IntegralHeight = false
        };
        _hubTitle = MakeText();
        _hubUrl = MakeText();
        _hubAdd = DialogUi.SecondaryButton("Add");
        _hubRemove = DialogUi.SecondaryButton("Remove");
        _hubAdd.Click += delegate { HubAdd(); };
        _hubRemove.Click += delegate { HubRemove(); };
        RefreshHubList();

        _pages[3].Controls.Add(_hubHint);
        _pages[3].Controls.Add(_hubList);
        _pages[3].Controls.Add(_hubTitle);
        _pages[3].Controls.Add(_hubUrl);
        _pages[3].Controls.Add(_hubAdd);
        _pages[3].Controls.Add(_hubRemove);

        _shell.Controls.Add(_content);
        _shell.Controls.Add(_nav);
        Controls.Add(_shell);
        Controls.Add(_footer);

        AcceptButton = _apply;
        CancelButton = _cancel;
        Result = Clone(src);

        Load += delegate
        {
            Relayout();
            int start = _openApps ? 2 : 0;
            _navList.SelectedIndex = start;
            ShowPage(start);
        };
        Shown += delegate { Relayout(); };
        Resize += delegate { Relayout(); };
    }

    private void NavDrawItem(object sender, DrawItemEventArgs e)
    {
        if (e.Index < 0)
            return;
        bool sel = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        Color bg = sel ? _accentColor : NavBack;
        Color fg = sel ? Color.White : ForeColor;
        using (var b = new SolidBrush(bg))
            e.Graphics.FillRectangle(b, e.Bounds);
        string text = _navList.Items[e.Index].ToString();
        var flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
        TextRenderer.DrawText(e.Graphics, text, DialogUi.UiFont(10f, FontStyle.Regular),
            new Rectangle(e.Bounds.X + 16, e.Bounds.Y, e.Bounds.Width - 20, e.Bounds.Height), fg, flags);
    }

    private void ShowPage(int index)
    {
        if (index < 0 || index > 3)
            return;
        _page = index;
        for (int i = 0; i < 4; i++)
            _pages[i].Visible = i == index;
        LayoutPage(index);
        _navList.Invalidate();
    }

    private void Relayout()
    {
        DialogUi.SizeFooter(this, _footer, _apply, _cancel);
        if (WindowState == FormWindowState.Minimized)
            return;
        if (!_sized)
        {
            ClientSize = DialogUi.FitClient(this, 720, 560, 0.55, 0.68);
            MinimumSize = new Size(DialogUi.Px(this, 560), DialogUi.Px(this, 420));
            _sized = true;
        }

        int navW = DialogUi.Px(this, 168);
        _nav.SetBounds(0, 0, navW, _shell.ClientSize.Height);
        _content.SetBounds(navW, 0, Math.Max(40, _shell.ClientSize.Width - navW), _shell.ClientSize.Height);
        _navList.ItemHeight = DialogUi.Px(this, 40);
        _navList.SetBounds(0, DialogUi.Px(this, 12), _nav.ClientSize.Width, Math.Max(40, _nav.ClientSize.Height - DialogUi.Px(this, 24)));

        for (int i = 0; i < 4; i++)
            _pages[i].SetBounds(0, 0, _content.ClientSize.Width, _content.ClientSize.Height);
        LayoutPage(_page);
    }

    private void LayoutPage(int index)
    {
        Panel page = _pages[index];
        int pad = DialogUi.Px(this, 28);
        int y = pad;
        _pageTitles[index].Font = DialogUi.UiFont(14f, FontStyle.Regular);
        _pageTitles[index].Location = new Point(pad, y);
        y += _pageTitles[index].PreferredHeight + DialogUi.Px(this, 4);
        _pageSubs[index].Font = DialogUi.UiFont(8.5f, FontStyle.Regular);
        _pageSubs[index].MaximumSize = new Size(Math.Max(40, page.ClientSize.Width - pad * 2), 0);
        _pageSubs[index].AutoSize = true;
        _pageSubs[index].Location = new Point(pad, y);
        y += _pageSubs[index].PreferredHeight + DialogUi.Px(this, 20);

        if (index == 0)
        {
            LayoutFields(page, y, pad,
                new Label[] { _lblIcon, _lblMore, _lblAccent, _lblMenu },
                new Control[] { _iconSize, _moreIconSize, _accent, _menuScale });
        }
        else if (index == 1)
        {
            int after = LayoutFields(page, y, pad,
                new Label[] { _lblEdge, _lblPeek, _lblMax },
                new Control[] { _edge, _peek, _maxVisible });
            int labelW = DialogUi.Px(this, 160);
            int fieldX = pad + labelW + DialogUi.Px(this, 12);
            _monitorNote.Font = Font;
            _monitorNote.Location = new Point(fieldX, after + DialogUi.Px(this, 6));
            after += DialogUi.Px(this, 40);
            _startWithWindows.Font = Font;
            _startWithWindows.Location = new Point(fieldX, after + DialogUi.Px(this, 4));
        }
        else if (index == 2)
            LayoutApps(page, y, pad);
        else
            LayoutChrome(page, y, pad);
    }

    private int LayoutFields(Panel page, int y, int pad, Label[] labels, Control[] fields)
    {
        int labelW = DialogUi.Px(this, 160);
        int fieldX = pad + labelW + DialogUi.Px(this, 12);
        int fieldW = Math.Max(DialogUi.Px(this, 180), page.ClientSize.Width - fieldX - pad);
        int row = DialogUi.Px(this, 40);
        for (int i = 0; i < fields.Length; i++)
        {
            labels[i].Font = Font;
            labels[i].Location = new Point(pad, y + DialogUi.Px(this, 8));
            fields[i].Font = Font;
            fields[i].SetBounds(fieldX, y, fieldW, DialogUi.Px(this, 28));
            y += row;
        }
        return y;
    }

    private void LayoutApps(Panel page, int y, int pad)
    {
        _appsHint.Font = DialogUi.UiFont(8.5f, FontStyle.Regular);
        _appsHint.MaximumSize = new Size(Math.Max(40, page.ClientSize.Width - pad * 2), 0);
        _appsHint.AutoSize = true;
        _appsHint.Location = new Point(pad, y);
        y += _appsHint.PreferredHeight + DialogUi.Px(this, 10);

        int btnColW = DialogUi.Px(this, 150);
        int gap = DialogUi.Px(this, 8);
        int listW = Math.Max(40, page.ClientSize.Width - pad * 2 - btnColW - gap);
        int listH = Math.Max(DialogUi.Px(this, 160), page.ClientSize.Height - y - pad);
        _pinList.Font = Font;
        _pinList.SetBounds(pad, y, listW, listH);

        int bx = pad + listW + gap;
        int bh = DialogUi.Px(this, 30);
        int by = y;
        _pinUp.SetBounds(bx, by, btnColW, bh); by += bh + gap;
        _pinDown.SetBounds(bx, by, btnColW, bh); by += bh + gap;
        _pinRemove.SetBounds(bx, by, btnColW, bh); by += bh + gap * 2;
        _addCatalog.SetBounds(bx, by, btnColW, bh); by += bh + gap;
        _addFile.SetBounds(bx, by, btnColW, bh);
    }

    private void LayoutChrome(Panel page, int y, int pad)
    {
        _hubHint.Font = DialogUi.UiFont(8.5f, FontStyle.Regular);
        _hubHint.MaximumSize = new Size(Math.Max(40, page.ClientSize.Width - pad * 2), 0);
        _hubHint.AutoSize = true;
        _hubHint.Location = new Point(pad, y);
        y += _hubHint.PreferredHeight + DialogUi.Px(this, 10);

        int hubListH = Math.Max(DialogUi.Px(this, 140), page.ClientSize.Height - y - DialogUi.Px(this, 100));
        _hubList.Font = Font;
        _hubList.SetBounds(pad, y, Math.Max(40, page.ClientSize.Width - pad * 2), hubListH);
        y += hubListH + DialogUi.Px(this, 10);
        int half = Math.Max(100, (page.ClientSize.Width - pad * 3) / 2);
        _hubTitle.Font = Font;
        _hubUrl.Font = Font;
        _hubTitle.SetBounds(pad, y, half, DialogUi.Px(this, 28));
        _hubUrl.SetBounds(pad + half + pad, y, half, DialogUi.Px(this, 28));
        y += DialogUi.Px(this, 36);
        int btnW = DialogUi.Px(this, 90);
        _hubAdd.SetBounds(pad, y, btnW, DialogUi.Px(this, 30));
        _hubRemove.SetBounds(pad + btnW + DialogUi.Px(this, 8), y, btnW, DialogUi.Px(this, 30));
    }

    private void RefreshPinList()
    {
        int sel = _pinList.SelectedIndex;
        _pinList.Items.Clear();
        if (_items == null) return;
        for (int i = 0; i < _items.Count; i++)
        {
            DockItem it = _items[i];
            if (it == null) continue;
            _pinList.Items.Add(string.IsNullOrEmpty(it.name) ? "(unnamed)" : it.name);
        }
        if (sel >= 0 && sel < _pinList.Items.Count)
            _pinList.SelectedIndex = sel;
    }

    private void PinMove(int delta)
    {
        int i = _pinList.SelectedIndex;
        if (_items == null || i < 0)
            return;
        int j = i + delta;
        if (j < 0 || j >= _items.Count)
            return;
        DockItem tmp = _items[i];
        _items[i] = _items[j];
        _items[j] = tmp;
        RefreshPinList();
        _pinList.SelectedIndex = j;
    }

    private void PinRemove()
    {
        int i = _pinList.SelectedIndex;
        if (_items == null || i < 0 || i >= _items.Count)
            return;
        _items.RemoveAt(i);
        RefreshPinList();
        if (_pinList.Items.Count > 0)
            _pinList.SelectedIndex = Math.Min(i, _pinList.Items.Count - 1);
    }

    private void PinAddCatalog()
    {
        var exclude = new List<string>();
        if (_items != null)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i] != null && !string.IsNullOrEmpty(_items[i].name))
                    exclude.Add(_items[i].name);
            }
        }
        using (var dlg = new CatalogPickForm(exclude))
        {
            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;
            if (_items == null)
                _items = new List<DockItem>();
            AppCatalog.AppendCatalogPins(_items, dlg.Selected);
            RefreshPinList();
        }
    }

    private void PinAddFile()
    {
        using (var ofd = new OpenFileDialog())
        {
            ofd.Filter = "Apps (*.exe;*.lnk)|*.exe;*.lnk|All files (*.*)|*.*";
            ofd.Title = "Add app to dock";
            ofd.CheckFileExists = true;
            if (ofd.ShowDialog(this) != DialogResult.OK)
                return;
            DockItem item = AppCatalog.TryCreateCustomItem(ofd.FileName);
            if (item == null)
                return;
            if (_items == null)
                _items = new List<DockItem>();
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i] != null &&
                    string.Equals(_items[i].name, item.name, StringComparison.OrdinalIgnoreCase))
                {
                    _pinList.SelectedIndex = i;
                    return;
                }
            }
            _items.Add(item);
            RefreshPinList();
            _pinList.SelectedIndex = _pinList.Items.Count - 1;
        }
    }

    private void RefreshHubList()
    {
        _hubList.Items.Clear();
        if (_hubPins == null) return;
        for (int i = 0; i < _hubPins.Count; i++)
        {
            HubPin p = _hubPins[i];
            if (p == null) continue;
            string t = string.IsNullOrEmpty(p.title) ? "(untitled)" : p.title;
            string u = string.IsNullOrEmpty(p.url) ? "" : p.url;
            _hubList.Items.Add(t + " — " + u);
        }
    }

    private void HubAdd()
    {
        string title = _hubTitle.Text != null ? _hubTitle.Text.Trim() : "";
        string url = _hubUrl.Text != null ? _hubUrl.Text.Trim() : "";
        if (title.Length == 0 || url.Length == 0)
            return;
        if (_hubPins == null)
            _hubPins = new List<HubPin>();
        _hubPins.Add(new HubPin { list = "Chrome", title = title, url = url });
        _hubTitle.Text = "";
        _hubUrl.Text = "";
        RefreshHubList();
    }

    private void HubRemove()
    {
        int i = _hubList.SelectedIndex;
        if (i < 0 || _hubPins == null || i >= _hubPins.Count)
            return;
        _hubPins.RemoveAt(i);
        RefreshHubList();
    }

    private Label MakeLabel(string text)
    {
        return new Label
        {
            Text = text,
            ForeColor = ForeColor,
            Font = Font,
            AutoSize = true,
            BackColor = Color.Transparent
        };
    }

    private ComboBox MakeCombo()
    {
        return new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = FieldBack,
            ForeColor = ForeColor,
            Font = Font
        };
    }

    private TextBox MakeText()
    {
        return new TextBox
        {
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = FieldBack,
            ForeColor = ForeColor,
            Font = Font
        };
    }

    private NumericUpDown MakeNum(int min, int max, int value)
    {
        return new NumericUpDown
        {
            Minimum = min,
            Maximum = max,
            Value = Math.Max(min, Math.Min(max, value)),
            BackColor = FieldBack,
            ForeColor = ForeColor,
            Font = Font
        };
    }

    private void DoApply()
    {
        var cfg = Clone(Result);
        cfg.edge = _edge.SelectedItem != null ? _edge.SelectedItem.ToString() : "top";
        cfg.iconSize = _iconSize.SelectedItem != null ? Convert.ToInt32(_iconSize.SelectedItem) : 96;
        cfg.moreIconSize = _moreIconSize.SelectedItem != null ? Convert.ToInt32(_moreIconSize.SelectedItem) : DockDefaults.DefaultMoreIconSize;
        cfg.accent = _accent.SelectedItem != null ? _accent.SelectedItem.ToString() : "blue";
        cfg.peek = (int)_peek.Value;
        cfg.menuScale = _menuScale.SelectedIndex <= 0 ? DockDefaults.MenuScaleNormal : DockDefaults.MenuScaleLarge;
        cfg.maxVisible = (int)_maxVisible.Value;
        cfg.monitor = "primary";
        cfg.startWithWindows = _startWithWindows.Checked;
        cfg.hubPins = CloneHubs(_hubPins);
        cfg.items = CloneItems(_items);
        cfg.setupComplete = true;
        try { DockStartup.SetEnabled(cfg.startWithWindows); }
        catch { }
        cfg.startWithWindows = DockStartup.IsEnabled();
        DockDefaults.Normalize(cfg);
        cfg.startWithWindows = DockStartup.IsEnabled();
        AppCatalog.InvalidateScan();
        Result = cfg;
        DialogResult = DialogResult.OK;
        Close();
    }

    private static DockConfig Clone(DockConfig src)
    {
        var cfg = new DockConfig();
        cfg.edge = src.edge;
        cfg.iconSize = src.iconSize;
        cfg.moreIconSize = src.moreIconSize;
        cfg.accent = src.accent;
        cfg.peek = src.peek;
        cfg.menuScale = src.menuScale;
        cfg.maxVisible = src.maxVisible;
        cfg.monitor = src.monitor;
        cfg.startWithWindows = src.startWithWindows;
        cfg.setupComplete = src.setupComplete;
        cfg.items = CloneItems(src.items);
        cfg.hubPins = CloneHubs(src.hubPins);
        return cfg;
    }

    private static List<DockItem> CloneItems(List<DockItem> src)
    {
        var list = new List<DockItem>();
        if (src == null) return list;
        for (int i = 0; i < src.Count; i++)
        {
            DockItem it = src[i];
            if (it == null) continue;
            list.Add(new DockItem { name = it.name, lnk = it.lnk, icon = it.icon });
        }
        return list;
    }

    private static List<HubPin> CloneHubs(List<HubPin> src)
    {
        var list = new List<HubPin>();
        if (src == null) return list;
        for (int i = 0; i < src.Count; i++)
        {
            HubPin p = src[i];
            if (p == null) continue;
            list.Add(new HubPin { list = p.list, title = p.title, url = p.url });
        }
        return list;
    }

    private static void SelectInt(ComboBox box, int value, int fallback)
    {
        for (int i = 0; i < box.Items.Count; i++)
        {
            if (Convert.ToInt32(box.Items[i]) == value)
            {
                box.SelectedIndex = i;
                return;
            }
        }
        for (int i = 0; i < box.Items.Count; i++)
        {
            if (Convert.ToInt32(box.Items[i]) == fallback)
            {
                box.SelectedIndex = i;
                return;
            }
        }
        if (box.Items.Count > 0)
            box.SelectedIndex = box.Items.Count - 1;
    }

    private static void SelectString(ComboBox box, string value, string fallback)
    {
        for (int i = 0; i < box.Items.Count; i++)
        {
            if (string.Equals(box.Items[i].ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                box.SelectedIndex = i;
                return;
            }
        }
        for (int i = 0; i < box.Items.Count; i++)
        {
            if (string.Equals(box.Items[i].ToString(), fallback, StringComparison.OrdinalIgnoreCase))
            {
                box.SelectedIndex = i;
                return;
            }
        }
        if (box.Items.Count > 0)
            box.SelectedIndex = 0;
    }
}
