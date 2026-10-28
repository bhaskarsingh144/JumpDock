// JumpDock — settings (DPI-aware, FlowLayout footer — no clipped buttons).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

public sealed class DockSettingsForm : Form
{
    private readonly Panel _body;
    private readonly Panel _footer;
    private readonly ComboBox _edge;
    private readonly ComboBox _iconSize;
    private readonly ComboBox _moreIconSize;
    private readonly ComboBox _accent;
    private readonly NumericUpDown _peek;
    private readonly ComboBox _menuScale;
    private readonly NumericUpDown _maxVisible;
    private readonly CheckBox _startWithWindows;
    private readonly Label _monitorNote;
    private readonly ListBox _hubList;
    private readonly TextBox _hubTitle;
    private readonly TextBox _hubUrl;
    private readonly Button _hubAdd;
    private readonly Button _hubRemove;
    private readonly Button _apply;
    private readonly Button _cancel;
    private readonly Label[] _labels = new Label[7];
    private readonly Label _hubLabel;
    private List<HubPin> _hubPins;

    public DockConfig Result { get; private set; }

    public DockSettingsForm(DockConfig current)
    {
        var src = current ?? new DockConfig();
        DockDefaults.Normalize(src);
        _hubPins = CloneHubs(src.hubPins);

        Text = "JumpDock Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.FromArgb(255, 28, 30, 34);
        ForeColor = Color.FromArgb(255, 236, 238, 242);
        Font = DialogUi.UiFont(9.5f, FontStyle.Regular);

        _apply = DialogUi.PrimaryButton("Apply");
        _cancel = DialogUi.SecondaryButton("Cancel");
        _cancel.DialogResult = DialogResult.Cancel;
        _apply.Click += delegate { DoApply(); };
        _footer = DialogUi.MakeFooter(this, _apply, _cancel);

        _body = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BackColor,
            AutoScroll = true,
            Padding = new Padding(20, 16, 20, 8)
        };

        _edge = MakeCombo();
        _edge.Items.AddRange(new object[] { "top", "bottom" });
        _edge.SelectedItem = string.Equals(src.edge, "bottom", StringComparison.OrdinalIgnoreCase) ? "bottom" : "top";

        _iconSize = MakeCombo();
        _iconSize.Items.AddRange(new object[] { 72, 84, 96, 108, 120, 128 });
        SelectInt(_iconSize, src.iconSize, 96);

        _moreIconSize = MakeCombo();
        _moreIconSize.Items.AddRange(new object[] { 48, 56, 64, 72, 84, 96 });
        SelectInt(_moreIconSize, src.moreIconSize > 0 ? src.moreIconSize : DockDefaults.DefaultMoreIconSize, 64);

        _accent = MakeCombo();
        _accent.Items.AddRange(new object[] { "blue", "teal", "amber", "gray" });
        SelectString(_accent, DockAccent.NormalizeKey(src.accent), "blue");

        _peek = MakeNum(0, 24, src.peek);
        _menuScale = MakeCombo();
        _menuScale.Items.Add("Normal (1.0)");
        _menuScale.Items.Add("Large (1.15)");
        _menuScale.SelectedIndex = src.menuScale < 1.07 ? 0 : 1;
        _maxVisible = MakeNum(8, 24, src.maxVisible);
        _monitorNote = new Label
        {
            Text = "Primary (only)",
            ForeColor = ForeColor,
            Font = Font,
            AutoSize = true
        };
        _startWithWindows = new CheckBox
        {
            Text = "Start with Windows",
            ForeColor = ForeColor,
            Font = Font,
            AutoSize = true,
            Checked = DockStartup.IsEnabled()
        };

        _hubLabel = new Label
        {
            Text = "Chrome hub pins",
            ForeColor = ForeColor,
            Font = Font,
            AutoSize = true
        };
        _hubList = new ListBox
        {
            BackColor = Color.FromArgb(255, 40, 44, 50),
            ForeColor = ForeColor,
            Font = Font,
            IntegralHeight = false
        };
        _hubTitle = MakeText();
        _hubUrl = MakeText();
        _hubAdd = DialogUi.SecondaryButton("Add");
        _hubRemove = DialogUi.SecondaryButton("Remove");
        _hubAdd.Click += delegate { HubAdd(); };
        _hubRemove.Click += delegate { HubRemove(); };
        RefreshHubList();

        string[] names = new string[]
        {
            "Edge", "Icon size (design)", "More apps icons", "Accent",
            "Peek (design px)", "Menu scale", "Max visible"
        };
        Control[] fields = new Control[]
        {
            _edge, _iconSize, _moreIconSize, _accent, _peek, _menuScale, _maxVisible
        };
        for (int i = 0; i < names.Length; i++)
        {
            _labels[i] = new Label
            {
                Text = names[i],
                ForeColor = ForeColor,
                Font = Font,
                AutoSize = true
            };
            _body.Controls.Add(_labels[i]);
            _body.Controls.Add(fields[i]);
        }
        _body.Controls.Add(_monitorNote);
        _body.Controls.Add(_startWithWindows);
        _body.Controls.Add(_hubLabel);
        _body.Controls.Add(_hubList);
        _body.Controls.Add(_hubTitle);
        _body.Controls.Add(_hubUrl);
        _body.Controls.Add(_hubAdd);
        _body.Controls.Add(_hubRemove);

        Controls.Add(_body);
        Controls.Add(_footer);

        AcceptButton = _apply;
        CancelButton = _cancel;
        Result = Clone(src);

        Load += delegate { Relayout(); };
        Shown += delegate { Relayout(); };
    }

    private void Relayout()
    {
        DialogUi.SizeFooter(this, _footer, _apply, _cancel);
        ClientSize = DialogUi.FitClient(this, 520, 620, 0.50, 0.72);
        MinimumSize = Size;

        int pad = DialogUi.Px(this, 16);
        int labelW = DialogUi.Px(this, 120);
        int fieldX = pad + labelW + DialogUi.Px(this, 8);
        int fieldW = Math.Max(DialogUi.Px(this, 180), _body.ClientSize.Width - fieldX - pad);
        int row = DialogUi.Px(this, 34);
        int y = pad;
        Control[] fields = new Control[]
        {
            _edge, _iconSize, _moreIconSize, _accent, _peek, _menuScale, _maxVisible
        };
        for (int i = 0; i < fields.Length; i++)
        {
            _labels[i].Font = Font;
            _labels[i].Location = new Point(pad, y + DialogUi.Px(this, 6));
            fields[i].Font = Font;
            fields[i].SetBounds(fieldX, y, fieldW, DialogUi.Px(this, 26));
            y += row;
        }

        _monitorNote.Font = Font;
        _monitorNote.Text = "Monitor: Primary (only)";
        _monitorNote.Location = new Point(fieldX, y + DialogUi.Px(this, 4));
        y += row;

        _startWithWindows.Font = Font;
        _startWithWindows.Location = new Point(fieldX, y + DialogUi.Px(this, 4));
        y += row + DialogUi.Px(this, 8);

        _hubLabel.Font = Font;
        _hubLabel.Location = new Point(pad, y);
        y += _hubLabel.PreferredHeight + DialogUi.Px(this, 6);
        int hubListH = DialogUi.Px(this, 90);
        _hubList.SetBounds(pad, y, Math.Max(40, _body.ClientSize.Width - pad * 2), hubListH);
        y += hubListH + DialogUi.Px(this, 6);
        int half = Math.Max(80, (_body.ClientSize.Width - pad * 3) / 2);
        _hubTitle.SetBounds(pad, y, half, DialogUi.Px(this, 24));
        _hubUrl.SetBounds(pad + half + pad, y, half, DialogUi.Px(this, 24));
        y += DialogUi.Px(this, 28);
        int btnW = DialogUi.Px(this, 80);
        _hubAdd.SetBounds(pad, y, btnW, DialogUi.Px(this, 28));
        _hubRemove.SetBounds(pad + btnW + DialogUi.Px(this, 8), y, btnW, DialogUi.Px(this, 28));
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

    private ComboBox MakeCombo()
    {
        return new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(255, 40, 44, 50),
            ForeColor = ForeColor,
            Font = Font
        };
    }

    private TextBox MakeText()
    {
        return new TextBox
        {
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(255, 40, 44, 50),
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
            BackColor = Color.FromArgb(255, 40, 44, 50),
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
        try { DockStartup.SetEnabled(cfg.startWithWindows); }
        catch { }
        cfg.startWithWindows = DockStartup.IsEnabled();
        DockDefaults.Normalize(cfg);
        cfg.startWithWindows = DockStartup.IsEnabled();
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
        cfg.items = src.items;
        cfg.hubPins = CloneHubs(src.hubPins);
        return cfg;
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
