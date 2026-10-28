using System;

/// <summary>
/// Dock bar geometry. iconSize / peek in config are 96-DPI design units (DIPs);
/// For() converts them with DisplayScale (Dpi/96 * Fit) then width-fits.
/// </summary>
internal sealed class DockLayout
{
    public int Icon;
    public int Tile;
    public int Gap;
    public int BarPad;
    public int BarH;
    public int Radius;
    public int TileRadius;
    public int Peek;
    public int EdgeHit;
    public int XSlop;
    public int ShowInset;
    public bool Shrunk;

    public int WidthFor(int count)
    {
        if (count <= 0) return BarPad * 2;
        return BarPad * 2 + count * Tile + (count - 1) * Gap;
    }

    /// <summary>
    /// Build layout for the bar. iconSizeDip / peekDip are design units at 96 DPI.
    /// </summary>
    public static DockLayout For(int iconSizeDip, int peekDip, int count, int screenW, int screenH, float dpi, float fit)
    {
        if (iconSizeDip < 32) iconSizeDip = 32;
        if (dpi < 96f) dpi = 96f;
        if (fit < 0.62f) fit = 0.62f;
        if (fit > 1f) fit = 1f;

        int desiredIcon = DisplayScale.Px(iconSizeDip, dpi, fit);
        if (desiredIcon < 40) desiredIcon = 40;

        int peekPx = peekDip > 0 ? DisplayScale.Px(peekDip, dpi, fit) : 0;

        int sideMin = DisplayScale.Px(64, dpi, fit);
        int side = Math.Max(sideMin, screenW / 16);
        int maxWFloor = DisplayScale.Px(320, dpi, fit);
        int maxW = Math.Max(maxWFloor, screenW - side * 2);

        var layout = FromIcon(desiredIcon, peekPx, dpi, fit);
        layout.Shrunk = false;
        if (count <= 0 || layout.WidthFor(count) <= maxW)
            return layout;

        int minIcon = DisplayScale.Px(40, dpi, fit);
        if (minIcon < 32) minIcon = 32;
        int lo = minIcon;
        int hi = desiredIcon;
        var best = FromIcon(minIcon, peekPx, dpi, fit);
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            var trial = FromIcon(mid, peekPx, dpi, fit);
            if (trial.WidthFor(count) <= maxW)
            {
                best = trial;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        int gapFloor = DisplayScale.Px(2, dpi, fit);
        int padFloor = DisplayScale.Px(8, dpi, fit);
        if (gapFloor < 1) gapFloor = 1;
        if (padFloor < 4) padFloor = 4;
        while (best.WidthFor(count) > maxW && (best.Gap > gapFloor || best.BarPad > padFloor))
        {
            if (best.Gap > gapFloor) best.Gap--;
            if (best.WidthFor(count) <= maxW) break;
            if (best.BarPad > padFloor) best.BarPad--;
        }

        int shrunkMark = DisplayScale.Px(56, dpi, fit);
        best.Shrunk = best.Icon < desiredIcon || best.Icon < shrunkMark;
        return best;
    }

    /// <summary>Convenience when callers already measured DisplayProfile.</summary>
    public static DockLayout For(int iconSizeDip, int peekDip, int count, int screenW, int screenH, DisplayProfile profile)
    {
        float dpi = profile.Dpi > 0 ? profile.Dpi : 96f;
        float fit = profile.Fit > 0 ? profile.Fit : 1f;
        return For(iconSizeDip, peekDip, count, screenW, screenH, dpi, fit);
    }

    private static DockLayout FromIcon(int icon, int peekPx, float dpi, float fit)
    {
        int tilePad = Math.Max(DisplayScale.Px(6, dpi, fit), (int)Math.Round(icon * 0.10));
        int tile = icon + tilePad;
        int gap = Math.Max(DisplayScale.Px(4, dpi, fit), (int)Math.Round(icon * 0.06));
        int vPad = Math.Max(DisplayScale.Px(12, dpi, fit), (int)Math.Round(icon * 0.12));
        var layout = new DockLayout();
        layout.Icon = icon;
        layout.Tile = tile;
        layout.Gap = gap;
        layout.BarH = tile + vPad;
        layout.Radius = DisplayScale.Px(14, dpi, fit);
        layout.BarPad = Math.Max(DisplayScale.Px(14, dpi, fit), (int)Math.Round(icon * 0.12));
        int trMin = DisplayScale.Px(8, dpi, fit);
        int trMax = DisplayScale.Px(12, dpi, fit);
        layout.TileRadius = Math.Max(trMin, Math.Min(trMax, tile / 8));
        layout.Peek = peekPx > 0 ? peekPx : Math.Max(DisplayScale.Px(4, dpi, fit), icon / 16);
        layout.EdgeHit = Math.Max(DisplayScale.Px(10, dpi, fit), icon / 6);
        layout.XSlop = Math.Max(DisplayScale.Px(48, dpi, fit), icon);
        layout.ShowInset = Math.Max(DisplayScale.Px(6, dpi, fit), icon / 10);
        return layout;
    }
}
