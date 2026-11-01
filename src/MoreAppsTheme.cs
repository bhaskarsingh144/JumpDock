// JumpDock — More apps chrome tokens (QuickBrowse-inspired; JumpDock-local).
using System;
using System.Drawing;

public static class MoreAppsTheme
{
    public static readonly Color Bg = Color.FromArgb(255, 0x20, 0x22, 0x27);
    public static readonly Color Surface = Color.FromArgb(255, 0x2B, 0x2E, 0x35);
    public static readonly Color Raised = Color.FromArgb(255, 0x34, 0x38, 0x40);
    public static readonly Color Chip = Color.FromArgb(255, 0x32, 0x36, 0x3E);
    public static readonly Color Border = Color.FromArgb(255, 0x45, 0x4A, 0x54);
    public static readonly Color BorderSoft = Color.FromArgb(255, 0x55, 0x5B, 0x66);
    public static readonly Color Hover = Color.FromArgb(255, 0x3A, 0x3F, 0x48);
    public static readonly Color Press = Color.FromArgb(255, 0x46, 0x4C, 0x56);
    public static readonly Color Text = Color.FromArgb(255, 0xE8, 0xEB, 0xF0);
    public static readonly Color Secondary = Color.FromArgb(255, 0xC8, 0xCD, 0xD6);
    public static readonly Color Muted = Color.FromArgb(255, 0x9A, 0xA1, 0xAC);
    public static readonly Color Accent = Color.FromArgb(255, 0x60, 0xCD, 0xFF);
    public static readonly Color AccentSoft = Color.FromArgb(40, 0x60, 0xCD, 0xFF);
    public static readonly Color Selected = Color.FromArgb(255, 0x2F, 0x3A, 0x48);

    public const int CornerDip = 16;
    public const int TileCornerDip = 10;
    public const int ChipCornerDip = 8;
    public const int OpenSlideDip = 22;
    public const int OpenAnimMs = 160;
    public const int TabAnimMs = 120;

    public static Font TitleFont()
    {
        return Semibold(12f);
    }

    public static Font HintFont()
    {
        return new Font("Segoe UI", 9f, FontStyle.Regular);
    }

    public static Font TabFont(bool selected)
    {
        return Semibold(9f, selected);
    }

    public static Font CaptionFont()
    {
        return Semibold(9f);
    }

    public static Font SubcaptionFont()
    {
        return new Font("Segoe UI", 7.5f, FontStyle.Regular);
    }

    public static Font SectionFont()
    {
        return Semibold(8.5f);
    }

    public static Font Semibold(float pt)
    {
        return Semibold(pt, true);
    }

    public static Font Semibold(float pt, bool preferBold)
    {
        try
        {
            return new Font("Segoe UI Semibold", pt, FontStyle.Regular);
        }
        catch
        {
            return new Font("Segoe UI", pt, preferBold ? FontStyle.Bold : FontStyle.Regular);
        }
    }

    /// <summary>Smoothstep ease used by QuickView motion.</summary>
    public static double Ease(double t)
    {
        if (t <= 0) return 0;
        if (t >= 1) return 1;
        return t * t * (3.0 - 2.0 * t);
    }
}
