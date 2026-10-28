using System;

/// <summary>
/// Fit / Px cases for common panels (16" FHD+, 2.8K, 27" 4K).
/// Run via scripts\test.ps1.
/// </summary>
internal static class DisplayScaleTests
{
    private static int _passed;
    private static int _failed;

    private static int Main()
    {
        // Logical height curve (desktop diagonal → no laptop floor yank).
        ExpectFloat(0.88f, DisplayScale.FitFromLogicalHeight(1080f), 0.001f, "curve 1080 → 0.88");
        ExpectFloat(0.94f, DisplayScale.FitFromLogicalHeight(1200f), 0.001f, "curve 1200 → 0.94");
        ExpectFloat(0.74f, DisplayScale.FitFromLogicalHeight(900f), 0.001f, "curve 900 → 0.74");
        ExpectFloat(1f, DisplayScale.FitFromLogicalHeight(1440f), 0.001f, "curve 1440 → 1");

        // 16" FHD+ 16:10 @100% — office laptop profile.
        DisplayProfile ten100 = DisplayScale.FromPhysical(1920, 1200, 96f, 16.0f, true);
        ExpectFitRange(ten100, 0.80f, 0.94f, "16\" 1920x1200 @100% Fit");
        ExpectFloat(0.92f, DisplayScale.ComputeTextFit(ten100), 0.001f, "16\" 1920x1200 @100% TextFit");

        // Same panel @125% — do not over-floor Fit.
        DisplayProfile ten125 = DisplayScale.FromPhysical(1920, 1200, 120f, 16.0f, true);
        float fitTen = DisplayScale.ComputeFit(ten125);
        ExpectTrue(fitTen <= 0.84f, "16\" 1920x1200 @125% Fit ≤ 0.84, got " + fitTen.ToString("0.00"));
        ExpectFloat(0.90f, DisplayScale.ComputeTextFit(ten125), 0.001f, "16\" 1920x1200 @125% TextFit");

        // 16" 2.8K 16:10 @200% — logical H≈900; high scale must not yank Fit to panel floor.
        DisplayProfile k28 = DisplayScale.FromPhysical(2880, 1800, 192f, 16.0f, true);
        float fit28 = DisplayScale.ComputeFit(k28);
        ExpectTrue(fit28 >= 0.74f && fit28 <= 0.84f,
            "16\" 2880x1800 @200% Fit in [0.74,0.84], got " + fit28.ToString("0.00"));
        ExpectFloat(1f, DisplayScale.ComputeTextFit(k28), 0.001f, "2.8K TextFit == 1");
        // Physical icon still grows with DPI even when Fit < 1.
        int icon28 = DisplayScale.Px(96, 192f, fit28);
        ExpectTrue(icon28 > 120, "2.8K @200% Px(96) > 120, got " + icon28);

        // 27" 4K @150% — desktop curve.
        DisplayProfile fourK = DisplayScale.FromPhysical(3840, 2160, 144f, 27.0f, false);
        ExpectFloat(1f, DisplayScale.ComputeFit(fourK), 0.001f, "27\" 4K @150% Fit");
        ExpectFloat(1f, DisplayScale.ComputeTextFit(fourK), 0.001f, "27\" 4K @150% TextFit");

        // Px grows with DPI at Fit=1.
        int px96 = DisplayScale.Px(96, 96f, 1f);
        int px144 = DisplayScale.Px(96, 144f, 1f);
        int px192 = DisplayScale.Px(96, 192f, 1f);
        ExpectTrue(px96 == 96, "Px(96,96,1) == 96");
        ExpectTrue(px144 == 144, "Px(96,144,1) == 144");
        ExpectTrue(px192 == 192, "Px(96,192,1) == 192");
        ExpectTrue(px192 > px144 && px144 > px96, "Px monotonic with DPI");

        // Fit softens Px on short logical screens.
        int pxSoft = DisplayScale.Px(96, 96f, 0.88f);
        ExpectTrue(pxSoft < 96 && pxSoft >= 80, "Px with Fit 0.88 softens below 96");

        Console.WriteLine();
        Console.WriteLine("DisplayScale tests: {0} passed, {1} failed", _passed, _failed);
        return _failed == 0 ? 0 : 1;
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
