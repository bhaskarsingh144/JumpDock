// JumpDock — dialog layout helpers (DPI-aware; footers remeasured each layout).
using System;
using System.Drawing;
using System.Windows.Forms;

public static class DialogUi
{
    public static int Dpi(Control c)
    {
        int dpi = 96;
        try
        {
            if (c != null && c.IsHandleCreated)
                dpi = c.DeviceDpi;
        }
        catch { }
        if (dpi < 96) dpi = 96;
        return dpi;
    }

    /// <summary>96-DPI design pixels -> physical pixels for current monitor.</summary>
    public static int Px(Control c, int design96)
    {
        return (int)Math.Round(design96 * Dpi(c) / 96.0);
    }

    public static Font UiFont(float pt, FontStyle style)
    {
        return new Font("Segoe UI", pt, style);
    }

    public static Rectangle Work(Control c)
    {
        try
        {
            if (c != null && c.IsHandleCreated)
                return Screen.FromControl(c).WorkingArea;
        }
        catch { }
        return Screen.PrimaryScreen.WorkingArea;
    }

    /// <summary>
    /// Client size from design @96dpi, capped to a fraction of the working area.
    /// </summary>
    public static Size FitClient(Control c, int designW96, int designH96, double maxWFrac, double maxHFrac)
    {
        var work = Work(c);
        int margin = Px(c, 48);
        int w = Px(c, designW96);
        int h = Px(c, designH96);
        int maxW = Math.Max(Px(c, 480), (int)(work.Width * maxWFrac) - margin);
        int maxH = Math.Max(Px(c, 360), (int)(work.Height * maxHFrac) - margin);
        if (w > maxW) w = maxW;
        if (h > maxH) h = maxH;
        if (w < Px(c, 480)) w = Math.Min(Px(c, 480), Math.Max(Px(c, 320), work.Width - margin));
        if (h < Px(c, 400)) h = Math.Min(Px(c, 400), Math.Max(Px(c, 280), work.Height - margin));
        return new Size(w, h);
    }

    /// <summary>Large dialog client size as a fraction of working area.</summary>
    public static Size LargeClient(Control c, double wFrac, double hFrac)
    {
        var work = Work(c);
        int margin = Px(c, 40);
        int w = Math.Max(Px(c, 640), (int)(work.Width * wFrac) - margin);
        int h = Math.Max(Px(c, 480), (int)(work.Height * hFrac) - margin);
        if (w > work.Width - margin) w = work.Width - margin;
        if (h > work.Height - margin) h = work.Height - margin;
        return new Size(w, h);
    }

    public static Panel MakeFooter(Control host, Button primary, Button secondary)
    {
        var footer = new Panel
        {
            Dock = DockStyle.Bottom,
            BackColor = Color.FromArgb(255, 28, 30, 34)
        };
        var flow = new FlowLayoutPanel
        {
            Name = "footerFlow",
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = footer.BackColor
        };
        if (secondary != null)
            flow.Controls.Add(secondary);
        if (primary != null)
            flow.Controls.Add(primary);
        footer.Controls.Add(flow);
        SizeFooter(host, footer, primary, secondary);
        return footer;
    }

    /// <summary>Re-apply DPI padding + height so Apply/Cancel are never half-clipped.</summary>
    public static void SizeFooter(Control host, Panel footer, Button primary, Button secondary)
    {
        if (footer == null) return;
        SizeButton(host, primary);
        SizeButton(host, secondary);
        int padX = Px(host, 20);
        int padTop = Px(host, 14);
        int padBot = Px(host, 16);
        FlowLayoutPanel flow = null;
        for (int i = 0; i < footer.Controls.Count; i++)
        {
            flow = footer.Controls[i] as FlowLayoutPanel;
            if (flow != null) break;
        }
        if (flow != null)
            flow.Padding = new Padding(padX, padTop, padX, padBot);
        int btnH = 0;
        if (primary != null) btnH = Math.Max(btnH, primary.Height);
        if (secondary != null) btnH = Math.Max(btnH, secondary.Height);
        // Extra slack so FlowLayout never clips button tops/bottoms
        footer.Height = btnH + padTop + padBot + Px(host, 8);
        footer.MinimumSize = new Size(0, footer.Height);
    }

    public static Button PrimaryButton(string text)
    {
        var b = new Button
        {
            Text = text,
            AutoSize = false,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(255, 56, 96, 140),
            ForeColor = Color.White,
            Font = UiFont(9.5f, FontStyle.Regular),
            Margin = new Padding(8, 0, 0, 0),
            UseVisualStyleBackColor = false,
            TextAlign = ContentAlignment.MiddleCenter
        };
        b.FlatAppearance.BorderColor = Color.FromArgb(255, 90, 130, 170);
        return b;
    }

    public static Button SecondaryButton(string text)
    {
        var b = new Button
        {
            Text = text,
            AutoSize = false,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(255, 48, 52, 58),
            ForeColor = Color.White,
            Font = UiFont(9.5f, FontStyle.Regular),
            Margin = new Padding(8, 0, 0, 0),
            UseVisualStyleBackColor = false,
            TextAlign = ContentAlignment.MiddleCenter
        };
        b.FlatAppearance.BorderColor = Color.FromArgb(255, 80, 84, 90);
        return b;
    }

    public static void SizeButton(Control host, Button b)
    {
        if (b == null) return;
        b.Font = UiFont(9.5f, FontStyle.Regular);
        // Measure text; keep comfortable hit target without clipping glyphs
        var textSize = TextRenderer.MeasureText(b.Text ?? "Apply", b.Font);
        int w = Math.Max(Px(host, 100), textSize.Width + Px(host, 28));
        int h = Math.Max(Px(host, 34), textSize.Height + Px(host, 14));
        b.Size = new Size(w, h);
        b.Padding = new Padding(0);
    }

    public static void PlaceNear(Form form, Point near, Rectangle work)
    {
        int gap = 8;
        int x = near.X;
        int y = near.Y + gap;
        if (x + form.Width > work.Right)
            x = work.Right - form.Width - gap;
        if (x < work.Left)
            x = work.Left + gap;
        if (y + form.Height > work.Bottom)
            y = work.Bottom - form.Height - gap;
        if (y < work.Top)
            y = work.Top + gap;
        form.Location = new Point(x, y);
    }
}
