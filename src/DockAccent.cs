using System;
using System.Drawing;

/// <summary>Accent presets for pin badges / Manage link (dock.json accent key).</summary>
internal static class DockAccent
{
    public static readonly Color Blue = Color.FromArgb(255, 152, 196, 236);
    public static readonly Color Teal = Color.FromArgb(255, 110, 190, 170);
    public static readonly Color Amber = Color.FromArgb(255, 230, 180, 100);
    public static readonly Color Gray = Color.FromArgb(255, 168, 174, 184);

    public static Color FromConfig(DockConfig cfg)
    {
        string key = cfg != null ? cfg.accent : null;
        return FromKey(key);
    }

    public static Color FromKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return Blue;
        if (string.Equals(key, "teal", StringComparison.OrdinalIgnoreCase))
            return Teal;
        if (string.Equals(key, "amber", StringComparison.OrdinalIgnoreCase))
            return Amber;
        if (string.Equals(key, "gray", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "grey", StringComparison.OrdinalIgnoreCase))
            return Gray;
        return Blue;
    }

    public static string NormalizeKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return "blue";
        if (string.Equals(key, "teal", StringComparison.OrdinalIgnoreCase))
            return "teal";
        if (string.Equals(key, "amber", StringComparison.OrdinalIgnoreCase))
            return "amber";
        if (string.Equals(key, "gray", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "grey", StringComparison.OrdinalIgnoreCase))
            return "gray";
        return "blue";
    }
}
