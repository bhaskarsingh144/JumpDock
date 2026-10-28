using System;

/// <summary>
/// Fit / Px cases for JumpDock target panels:
/// 16" 2.8K 16:10, 27" 4K 16:9, 16" 1080p-class 16:10.
/// Run via scripts\test.ps1.
/// </summary>
internal static class DisplayScaleTests
{
    private static int _passed;
    private static int _failed;

    private static int Main()
    {
        CurveSanity();
        OfficeLaptop1080p();
        CurrentLaptop28K();
        WorkMonitor4K();
        PxHelpers();
        ShellChromeStrips();

        Console.WriteLine();
        Console.WriteLine("DisplayScale tests: {0} passed, {1} failed", _passed, _failed);
        return _failed == 0 ? 0 : 1;
    }

    private static void CurveSanity()
    {
        ExpectFloat(0.88f, DisplayScale.FitFromLogicalHeight(1080f), 0.001f, "curve 1080 → 0.88");
        ExpectFloat(0.94f, DisplayScale.FitFromLogicalHeight(1200f), 0.001f, "curve 1200 → 0.94");
        ExpectFloat(0.74f, DisplayScale.FitFromLogicalHeight(900f), 0.001f, "curve 900 → 0.74");
        ExpectFloat(1f, DisplayScale.FitFromLogicalHeight(1440f), 0.001f, "curve 1440 → 1");
        // 4K @150% logical height ≈ 1440
        ExpectFloat(1f, DisplayScale.FitFromLogicalHeight(2160f * 96f / 144f), 0.001f, "curve 4K@150% logicH → 1");
    }

    private static void OfficeLaptop1080p()
    {
        // 16" FHD+ 16:10 @100%
        DisplayProfile ten100 = DisplayScale.FromPhysical(1920, 1200, 96f, 16.0f, true);
        ExpectFitRange(ten100, 0.80f, 0.94f, "office 1920x1200 @100% Fit");
        ExpectFloat(0.92f, DisplayScale.ComputeTextFit(ten100), 0.001f, "office @100% TextFit");

        // @125% — do not yank Fit back to laptop floor
        DisplayProfile ten125 = DisplayScale.FromPhysical(1920, 1200, 120f, 16.0f, true);
        float fit125 = DisplayScale.ComputeFit(ten125);
        ExpectTrue(fit125 <= 0.84f, "office @125% Fit ≤ 0.84, got " + fit125.ToString("0.00"));
        ExpectFloat(0.90f, DisplayScale.ComputeTextFit(ten125), 0.001f, "office @125% TextFit");

        int icon100 = DisplayScale.Px(96, 96f, DisplayScale.ComputeFit(ten100));
        int icon125 = DisplayScale.Px(96, 120f, fit125);
        ExpectTrue(icon125 > icon100, "office @125% physical icon > @100%");
    }

    private static void CurrentLaptop28K()
    {
        // 16" 2.8K 16:10 @150% — common setting
        DisplayProfile k28_150 = DisplayScale.FromPhysical(2880, 1800, 144f, 16.0f, true);
        float fit150 = DisplayScale.ComputeFit(k28_150);
        ExpectTrue(fit150 >= 0.80f && fit150 <= 1.0f,
            "2.8K @150% Fit in [0.80,1], got " + fit150.ToString("0.00"));
        int icon150 = DisplayScale.Px(96, 144f, fit150);
        ExpectTrue(icon150 > 110, "2.8K @150% Px(96) > 110, got " + icon150);

        // @200% — logical H≈900; high scale must not yank Fit to panel floor
        DisplayProfile k28_200 = DisplayScale.FromPhysical(2880, 1800, 192f, 16.0f, true);
        float fit200 = DisplayScale.ComputeFit(k28_200);
        ExpectTrue(fit200 >= 0.74f && fit200 <= 0.84f,
            "2.8K @200% Fit in [0.74,0.84], got " + fit200.ToString("0.00"));
        ExpectFloat(1f, DisplayScale.ComputeTextFit(k28_200), 0.001f, "2.8K TextFit == 1");
        int icon200 = DisplayScale.Px(96, 192f, fit200);
        ExpectTrue(icon200 > 120, "2.8K @200% Px(96) > 120, got " + icon200);
        ExpectTrue(icon200 > icon150, "2.8K @200% icon > @150% icon");
    }

    private static void WorkMonitor4K()
    {
        // 27" 4K @100%
        DisplayProfile k4_100 = DisplayScale.FromPhysical(3840, 2160, 96f, 27.0f, false);
        ExpectFloat(1f, DisplayScale.ComputeFit(k4_100), 0.001f, "4K @100% Fit");
        ExpectTrue(DisplayScale.Px(96, 96f, 1f) == 96, "4K @100% icon DIP==physical");

        // @150% — desktop curve, no text tighten
        DisplayProfile k4_150 = DisplayScale.FromPhysical(3840, 2160, 144f, 27.0f, false);
        ExpectFloat(1f, DisplayScale.ComputeFit(k4_150), 0.001f, "4K @150% Fit");
        ExpectFloat(1f, DisplayScale.ComputeTextFit(k4_150), 0.001f, "4K @150% TextFit");
        ExpectTrue(DisplayScale.Px(96, 144f, 1f) == 144, "4K @150% Px(96)==144");

        // @200% still Fit 1 (logical H = 1080 → curve 0.88, but wait: 2160*96/192=1080 → FitFromLogicalHeight 0.88)
        // Desktop 27" has no laptop floor; ComputeFit uses curve only → 0.88
        DisplayProfile k4_200 = DisplayScale.FromPhysical(3840, 2160, 192f, 27.0f, false);
        float fit4k200 = DisplayScale.ComputeFit(k4_200);
        ExpectTrue(fit4k200 >= 0.85f && fit4k200 <= 1.0f,
            "4K @200% Fit in [0.85,1], got " + fit4k200.ToString("0.00"));
        int icon4k200 = DisplayScale.Px(96, 192f, fit4k200);
        ExpectTrue(icon4k200 > 144, "4K @200% icon still larger than @150% raw 144-path, got " + icon4k200);
    }

    private static void PxHelpers()
    {
        int px96 = DisplayScale.Px(96, 96f, 1f);
        int px144 = DisplayScale.Px(96, 144f, 1f);
        int px192 = DisplayScale.Px(96, 192f, 1f);
        ExpectTrue(px96 == 96, "Px(96,96,1) == 96");
        ExpectTrue(px144 == 144, "Px(96,144,1) == 144");
        ExpectTrue(px192 == 192, "Px(96,192,1) == 192");
        ExpectTrue(px192 > px144 && px144 > px96, "Px monotonic with DPI");

        int pxSoft = DisplayScale.Px(96, 96f, 0.88f);
        ExpectTrue(pxSoft < 96 && pxSoft >= 80, "Px with Fit 0.88 softens below 96");

        ExpectTrue(DisplayScale.Px(0, 192f, 1f) == 0, "Px(0) == 0");
        ExpectTrue(DisplayScale.Px(14, 96f, 1f) == 14, "Px radius DIP passthrough @96");
    }

    private static void ShellChromeStrips()
    {
        // HoverStrip 24 / PastEdge 32 / CoverSlop 8 design units must grow with DPI×Fit
        int hover96 = DisplayScale.Px(24, 96f, 1f);
        int hover192 = DisplayScale.Px(24, 192f, 0.78f);
        ExpectTrue(hover96 == 24, "HoverStrip @96 == 24");
        ExpectTrue(hover192 > hover96, "HoverStrip grows at 200% even with Fit under 1");
        ExpectTrue(DisplayScale.Px(32, 192f, 0.78f) > 32, "PastEdge grows at 200%");
        ExpectTrue(DisplayScale.Px(8, 144f, 1f) == 12, "CoverSlop @150% == 12");
    }

    private static void ExpectFitRange(DisplayProfile p, float min, float max, string name)
    {
        float fit = DisplayScale.ComputeFit(p);
        if (fit >= min && fit <= max)
        {
            _passed++;
            Console.WriteLine("  PASS  {0} ({1:0.00})", name, fit);
        }
        else
        {
            _failed++;
            Console.WriteLine("  FAIL  {0}: {1:0.00} not in [{2:0.00},{3:0.00}]", name, fit, min, max);
        }
    }

    private static void ExpectTrue(bool ok, string name)
    {
        if (ok)
        {
            _passed++;
            Console.WriteLine("  PASS  {0}", name);
        }
        else
        {
            _failed++;
            Console.WriteLine("  FAIL  {0}", name);
        }
    }

    private static void ExpectFloat(float expected, float actual, float eps, string name)
    {
        if (Math.Abs(expected - actual) <= eps)
        {
            _passed++;
            Console.WriteLine("  PASS  {0}", name);
        }
        else
        {
            _failed++;
            Console.WriteLine("  FAIL  {0}: expected {1}, got {2}", name, expected, actual);
        }
    }
}
