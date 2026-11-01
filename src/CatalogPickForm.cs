// JumpDock — compact searchable catalog picker (Settings → Apps).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

public sealed class CatalogPickForm : Form
{
    private readonly TextBox _search;
    private readonly ListBox _list;
    private readonly Button _ok;
    private readonly Button _cancel;
    private readonly Panel _footer;
    private readonly List<CatalogApp> _all = new List<CatalogApp>();
    private readonly List<CatalogApp> _shown = new List<CatalogApp>();
    private readonly HashSet<string> _exclude;

    public List<CatalogApp> Selected { get; private set; }

    public CatalogPickForm(IEnumerable<string> excludeNames)
    {
        Selected = new List<CatalogApp>();
        _exclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (excludeNames != null)
        {
            foreach (string n in excludeNames)
            {
                if (!string.IsNullOrEmpty(n))
                    _exclude.Add(n);
            }
        }

        Text = "Add from catalog";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.FromArgb(255, 28, 30, 34);
        ForeColor = Color.FromArgb(255, 236, 238, 242);
        Font = DialogUi.UiFont(9.5f, FontStyle.Regular);

        _ok = DialogUi.PrimaryButton("Add");
        _cancel = DialogUi.SecondaryButton("Cancel");
        _cancel.DialogResult = DialogResult.Cancel;
        _ok.Click += delegate { DoOk(); };
        _footer = DialogUi.MakeFooter(this, _ok, _cancel);

        _search = new TextBox
        {
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(255, 40, 44, 50),
            ForeColor = ForeColor,
            Font = Font
        };
        _search.TextChanged += delegate { ApplyFilter(); };

        _list = new ListBox
        {
            BackColor = Color.FromArgb(255, 40, 44, 50),
            ForeColor = ForeColor,
            Font = Font,
            SelectionMode = SelectionMode.MultiExtended,
            IntegralHeight = false
        };
        _list.DoubleClick += delegate { DoOk(); };

        Controls.Add(_list);
        Controls.Add(_search);
        Controls.Add(_footer);

        AcceptButton = _ok;
        CancelButton = _cancel;

        Load += delegate
        {
            LoadCatalog();
            Relayout();
            try { _search.Focus(); } catch { }
        };
        Shown += delegate { Relayout(); };
    }

    private void Relayout()
    {
        DialogUi.SizeFooter(this, _footer, _ok, _cancel);
        ClientSize = DialogUi.FitClient(this, 420, 460, 0.42, 0.55);
        MinimumSize = Size;
        int pad = DialogUi.Px(this, 16);
        int searchH = DialogUi.Px(this, 26);
        _search.SetBounds(pad, pad, Math.Max(40, ClientSize.Width - pad * 2), searchH);
        int top = pad + searchH + DialogUi.Px(this, 8);
        int bottom = _footer.Height + pad;
        _list.SetBounds(pad, top, Math.Max(40, ClientSize.Width - pad * 2),
            Math.Max(40, ClientSize.Height - top - bottom));
    }

    private void LoadCatalog()
    {
        _all.Clear();
        List<CatalogApp> scan = AppCatalog.ScanCached();
        if (scan == null)
            return;
        for (int i = 0; i < scan.Count; i++)
        {
            CatalogApp app = scan[i];
            if (app == null || string.IsNullOrEmpty(app.DisplayName))
                continue;
            if (_exclude.Contains(app.DisplayName))
                continue;
            if (!app.Installed && !string.Equals(app.Kind, "chromeApp", StringComparison.OrdinalIgnoreCase))
                continue;
            _all.Add(app);
        }
        _all.Sort(CompareCatalogPick);
        ApplyFilter();
    }

    private static int CompareCatalogPick(CatalogApp a, CatalogApp b)
    {
        bool ae = AppCatalog.IsExternal(a);
        bool be = AppCatalog.IsExternal(b);
        if (ae != be)
            return ae ? 1 : -1; // Installed (desktop) first
        int c = AppCatalog.CategoryRankPublic(a == null ? "" : a.Group)
            .CompareTo(AppCatalog.CategoryRankPublic(b == null ? "" : b.Group));
        if (c != 0)
            return c;
        bool ja = a != null && JumpLists.Supports(a.DisplayName);
        bool jb = b != null && JumpLists.Supports(b.DisplayName);
        if (ja != jb)
            return ja ? -1 : 1;
        string an = a == null || a.DisplayName == null ? "" : a.DisplayName;
        string bn = b == null || b.DisplayName == null ? "" : b.DisplayName;
        return string.Compare(an, bn, StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyFilter()
    {
        string q = _search.Text != null ? _search.Text.Trim() : "";
        _shown.Clear();
        _list.BeginUpdate();
        _list.Items.Clear();
        for (int i = 0; i < _all.Count; i++)
        {
            CatalogApp app = _all[i];
            if (q.Length > 0 &&
                (app.DisplayName == null ||
                 app.DisplayName.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0))
                continue;
            _shown.Add(app);
            string group = string.IsNullOrEmpty(app.Group) ? "" : ("  ·  " + app.Group);
            string jump = JumpLists.LabelOf(app.DisplayName);
            string jumpHint = string.IsNullOrEmpty(jump) ? "" : ("  ·  Jump");
            _list.Items.Add(app.DisplayName + group + jumpHint);
        }
        _list.EndUpdate();
    }

    private void DoOk()
    {
        Selected = new List<CatalogApp>();
        for (int i = 0; i < _list.SelectedIndices.Count; i++)
        {
            int idx = _list.SelectedIndices[i];
            if (idx >= 0 && idx < _shown.Count)
                Selected.Add(_shown[idx]);
        }
        if (Selected.Count == 0)
            return;
        DialogResult = DialogResult.OK;
        Close();
    }
}
