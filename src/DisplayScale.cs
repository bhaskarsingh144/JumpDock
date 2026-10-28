using System;
using System.Drawing;
using System.Windows.Forms;

// Layout scale for the primary screen. Remeasured on start, DPI change, display change.
//
// Model (PerMonitorV2), same idea as QuickView:
//   Layout DIPs:  px = dip * (Dpi/96) * Fit
// Fit follows logical height (physical * 96 / Dpi). Soft laptop/panel floors
// avoid fighting Windows scale on 15–17" FHD / 2.8K panels.

internal struct DisplayProfile
{
    public int PhysW;
    public int PhysH;
    public float Dpi;
    public float LogicW;
    public float LogicH;
    public float DiagonalIn;
    public float Fit;
    public float TextFit;
    public bool SixteenTen;
    public bool Ultrawide;
}

internal static class DisplayScale
{
    public static DisplayProfile Measure(float dpi)
    {
        if (dpi < 96f)
            dpi = 96f;
        Rectangle bounds = Screen.PrimaryScreen.Bounds;
        DisplayProfile p = new DisplayProfile();
        p.PhysW = bounds.Width;
        p.PhysH = bounds.Height;
        p.Dpi = dpi;
        p.LogicW = bounds.Width * 96f / dpi;
        p.LogicH = bounds.Height * 96f / dpi;
        float ratio = AspectRatio(p.LogicW, p.LogicH);
        p.SixteenTen = IsSixteenTen(ratio);
        p.Ultrawide = IsUltrawide(ratio);
        p.DiagonalIn = EstimatedDiagonalInches(bounds, dpi);
        p.Fit = ComputeFit(p);
        p.TextFit = ComputeTextFit(p);
        return p;
    }

    /// <summary>Build a profile for tests without touching Screen.</summary>
    public static DisplayProfile FromPhysical(int physW, int physH, float dpi, float diagonalIn, bool sixteenTen)
    {
        if (dpi < 96f)
            dpi = 96f;
        DisplayProfile p = new DisplayProfile();
        p.PhysW = physW;
        p.PhysH = physH;
        p.Dpi = dpi;
        p.LogicW = physW * 96f / dpi;
        p.LogicH = physH * 96f / dpi;
        p.DiagonalIn = diagonalIn;
        p.SixteenTen = sixteenTen;
        p.Ultrawide = IsUltrawide(AspectRatio(p.LogicW, p.LogicH));
        p.Fit = ComputeFit(p);
        p.TextFit = ComputeTextFit(p);
        return p;
    }

    public static int Px(int dip, float dpi, float fit)
    {
        if (dip <= 0)
            return 0;
        if (dpi < 96f)
            dpi = 96f;
        if (fit < 0.62f)
            fit = 0.62f;
        if (fit > 1f)
            fit = 1f;
        int px = (int)Math.Round(dip * (dpi / 96f) * fit);
        return px < 1 ? 1 : px;
    }

    public static float ComputeFit(DisplayProfile p)
    {
        float fit = FitFromLogicalHeight(p.LogicH);
        float scale = p.Dpi / 96f;

        float floor = LaptopFloor(p.DiagonalIn, p.SixteenTen);
        float panel = StandardPanelFloor(p.PhysW, p.PhysH, p.DiagonalIn, p.SixteenTen);
        float want = Math.Max(floor, panel);

        if (want > 0f && fit < want)
        {
            if (scale >= 1.2f)
                fit = Math.Min(want, fit + 0.04f);
            else
                fit = want;
        }

        if (p.Ultrawide && fit > 0.94f && p.LogicH < 1200f)
            fit = 0.94f;

        if (fit > 1f)
            fit = 1f;
        if (fit < 0.62f)
            fit = 0.62f;
        return fit;
    }

    /// <summary>Optional Point-font tighten on compact laptop panels (menus already Dip).</summary>
    public static float ComputeTextFit(DisplayProfile p)
    {
        bool compactLaptop = p.DiagonalIn > 0f && p.DiagonalIn <= 17.5f
            && p.PhysH > 0 && p.PhysH <= 1200;
        if (!compactLaptop && p.DiagonalIn <= 0f && p.PhysH >= 1000 && p.PhysH <= 1200
            && (Near(p.PhysW, 1920) || (p.SixteenTen && p.PhysH >= 1100)))
            compactLaptop = true;

        if (!compactLaptop)
            return 1f;

        float scale = p.Dpi / 96f;
        if (scale >= 1.45f)
            return 0.88f;
        if (scale >= 1.2f)
            return 0.90f;
        return 0.92f;
    }

    public static float FitFromLogicalHeight(float h)
    {
        if (h >= 1600f)
            return 1f;
        if (h >= 1440f)
            return 1f;
        if (h >= 1280f)
            return 0.97f;
        if (h >= 1200f)
            return 0.94f;
        if (h >= 1080f)
            return 0.88f;
        if (h >= 1024f)
            return 0.82f;
        if (h >= 900f)
            return 0.74f;
        if (h >= 800f)
            return 0.70f;
        if (h >= 768f)
            return 0.66f;
        return 0.62f;
    }

    private static float LaptopFloor(float diagonalIn, bool sixteenTen)
    {
        if (diagonalIn <= 0f || diagonalIn > 18.5f)
            return 0f;
        if (diagonalIn >= 15.0f)
            return sixteenTen ? 0.80f : 0.76f;
        if (diagonalIn >= 13.0f)
            return sixteenTen ? 0.76f : 0.72f;
        return sixteenTen ? 0.72f : 0.68f;
    }

    private static float StandardPanelFloor(int physW, int physH, float diagonalIn, bool sixteenTen)
    {
        int w = physW;
        int h = physH;
        if (Near(w, 1920) && Near(h, 1200))
        {
            if (diagonalIn > 0f && diagonalIn < 15.0f)
                return 0.76f;
            return Laptopish(diagonalIn) ? 0.80f : 0f;
        }
        if (Near(w, 1920) && Near(h, 1080))
        {
            if (diagonalIn > 0f && diagonalIn < 15.0f)
                return 0.72f;
            return Laptopish(diagonalIn) ? 0.76f : 0f;
        }
        if (Near(w, 2240) && Near(h, 1400))
            return 0.80f;
        if (Near(w, 2560) && Near(h, 1600))
            return Laptopish(diagonalIn) ? 0.88f : 0f;
        if (Near(w, 2560) && Near(h, 1440))
            return Laptopish(diagonalIn) ? 0.86f : 0f;
        if (Near(w, 2880) && Near(h, 1800))
            return Laptopish(diagonalIn) ? 0.88f : 0f;
        if (Near(w, 3072) && Near(h, 1920))
            return Laptopish(diagonalIn) ? 0.88f : 0f;
        if (Near(w, 3840) && Near(h, 2400))
            return Laptopish(diagonalIn) ? 0.90f : 0f;
        if (Near(w, 3840) && Near(h, 2160))
            return 0f;
        if (Near(w, 3440) && Near(h, 1440))
            return 0f;
        if (Near(w, 1366) && Near(h, 768))
            return 0.62f;
        if (Near(w, 1600) && Near(h, 900))
            return Laptopish(diagonalIn) ? 0.68f : 0f;
        if (sixteenTen && Laptopish(diagonalIn) && h >= 1100 && h <= 1300)
        {
            if (diagonalIn > 0f && diagonalIn < 15.0f)
                return 0.76f;
            return 0.80f;
        }
        return 0f;
    }

    private static bool Laptopish(float diagonalIn)
    {
        if (diagonalIn <= 0f)
            return true;
        return diagonalIn <= 18.5f;
    }

    private static bool Near(int value, int target)
    {
        int d = value - target;
        if (d < 0)
            d = -d;
        return d <= 16;
    }

    private static float AspectRatio(float w, float h)
    {
        if (h < 1f)
            return 0f;
        return w / h;
    }

    private static bool IsSixteenTen(float ratio)
    {
        return ratio >= 1.53f && ratio <= 1.67f;
    }

    private static bool IsUltrawide(float ratio)
    {
        return ratio >= 2.0f && ratio <= 3.6f;
    }

    private static float EstimatedDiagonalInches(Rectangle bounds, float dpi)
    {
        float w = bounds.Width / dpi;
        float h = bounds.Height / dpi;
        float d = (float)Math.Sqrt(w * w + h * h);
        if (d >= 10f && d <= 45f)
            return d;
        return 0f;
    }
}
