// JumpDock — settings (DPI-aware, FlowLayout footer — no clipped buttons).
using System;
using System.Drawing;
using System.Windows.Forms;

public sealed class DockSettingsForm : Form
{
    private readonly Panel _body;
    private readonly Panel _footer;
    private readonly ComboBox _edge;
    private readonly ComboBox _iconSize;
    private readonly NumericUpDown _peek;
    private readonly ComboBox _menuScale;
    private readonly NumericUpDown _maxVisible;
    private readonly CheckBox _startWithWindows;
    private readonly Label _monitorNote;
    private readonly Button _apply;
    private readonly Button _cancel;
    private readonly Label[] _labels = new Label[6];

    public DockConfig Result { get; private set; }

    public DockSettingsForm(DockConfig current)
    {
        var src = current ?? new DockConfig();
        DockDefaults.Normalize(src);

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
        _iconSize.Items.AddRange(new object[] { 72, 84, 96 });
        SelectInt(_iconSize, src.iconSize, 96);

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

        string[] names = new string[] { "Edge", "Icon size (design)", "Peek (design px)", "Menu scale", "Max visible", "Monitor" };
        Control[] fields = new Control[] { _edge, _iconSize, _peek, _menuScale, _maxVisible, _monitorNote };
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
        _body.Controls.Add(_startWithWindows);

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

        // Comfortable on 1080p; on 4K DPI scales Px()
        ClientSize = DialogUi.FitClient(this, 460, 400, 0.45, 0.55);
        MinimumSize = Size;

        int pad = DialogUi.Px(this, 16);
        int labelW = DialogUi.Px(this, 100);
        int fieldX = pad + labelW + DialogUi.Px(this, 8);
        int fieldW = Math.Max(DialogUi.Px(this, 160), _body.ClientSize.Width - fieldX - pad);
        int row = DialogUi.Px(this, 36);
        int y = pad;
        Control[] fields = new Control[] { _edge, _iconSize, _peek, _menuScale, _maxVisible, _monitorNote };
        for (int i = 0; i < fields.Length; i++)
        {
            _labels[i].Font = Font;
            _labels[i].Location = new Point(pad, y + DialogUi.Px(this, 6));
            fields[i].Font = Font;
            fields[i].SetBounds(fieldX, y, fieldW, DialogUi.Px(this, 26));
            y += row;
        }
        _startWithWindows.Font = Font;
        _startWithWindows.Location = new Point(fieldX, y + DialogUi.Px(this, 4));
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
        cfg.peek = (int)_peek.Value;
        cfg.menuScale = _menuScale.SelectedIndex <= 0 ? DockDefaults.MenuScaleNormal : DockDefaults.MenuScaleLarge;
        cfg.maxVisible = (int)_maxVisible.Value;
        cfg.monitor = "primary";
        cfg.startWithWindows = _startWithWindows.Checked;
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
        cfg.peek = src.peek;
        cfg.menuScale = src.menuScale;
        cfg.maxVisible = src.maxVisible;
        cfg.monitor = src.monitor;
        cfg.startWithWindows = src.startWithWindows;
        cfg.setupComplete = src.setupComplete;
        cfg.items = src.items;
        cfg.hubPins = src.hubPins;
        return cfg;
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
}
