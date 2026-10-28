using System;

/// <summary>
/// DockLayout DIP → physical icon scaling and width fit.
/// Run via scripts\test.ps1.
/// </summary>
internal static class DockLayoutScaleTests
{
    private static int _passed;
    private static int _failed;

    private static int Main()
    {
        const int iconDip = 96;
        const int peekDip = 6;
        const int count = 10;

        // Same DIP at 96 vs 192 DPI, Fit=1, wide screen — icons track DPI.
        DockLayout at96 = DockLayout.For(iconDip, peekDip, count, 3840, 2160, 96f, 1f);
        DockLayout at192 = DockLayout.For(iconDip, peekDip, count, 3840, 2160, 192f, 1f);
        ExpectTrue(at96.Icon == 96, "96 DPI icon physical == 96, got " + at96.Icon);
        ExpectTrue(at192.Icon == 192, "192 DPI icon physical == 192, got " + at192.Icon);
        ExpectTrue(at192.Icon > at96.Icon, "higher DPI → larger physical icon");
        ExpectTrue(at192.BarH > at96.BarH, "higher DPI → taller bar");
        ExpectTrue(at192.Radius > at96.Radius, "higher DPI → larger radius");

        // 27" 4K @150% profile (Fit ~1).
        DisplayProfile fourK = DisplayScale.FromPhysical(3840, 2160, 144f, 27.0f, false);
        DockLayout k4 = DockLayout.For(iconDip, peekDip, count, 3840, 2160, fourK);
        ExpectTrue(k4.Icon == DisplayScale.Px(96, 144f, fourK.Fit), "4K layout icon matches Px");
        ExpectTrue(k4.Icon >= 140, "4K @150% icon roughly ≥140, got " + k4.Icon);

        // 16" 1080p-class 1920x1200 @100% — Fit softens, width still fits.
        DisplayProfile office = DisplayScale.FromPhysical(1920, 1200, 96f, 16.0f, true);
        DockLayout officeLay = DockLayout.For(iconDip, peekDip, 12, 1920, 1200, office);
        ExpectTrue(officeLay.WidthFor(12) <= 1920, "office layout width ≤ screen");
        ExpectTrue(officeLay.Icon <= 96, "office Fit does not grow past DIP at 96 DPI");
        ExpectTrue(officeLay.Icon >= 40, "office icon above floor");

        // Crowded 1920-wide still binary-searches down instead of overflowing.
        DockLayout crowded = DockLayout.For(96, 6, 20, 1920, 1200, 96f, 1f);
        ExpectTrue(crowded.WidthFor(20) <= 1920 - 64, "crowded 20 tiles width-fit on 1920");
        ExpectTrue(crowded.Shrunk || crowded.Icon < 96, "crowded marks shrunk or reduced icon");

        // 2.8K @200% — icons much larger than 96 physical (old bug: stayed ~80).
        DisplayProfile k28 = DisplayScale.FromPhysical(2880, 1800, 192f, 16.0f, true);
        DockLayout lay28 = DockLayout.For(iconDip, peekDip, 12, 2880, 1800, k28);
        ExpectTrue(lay28.Icon > 120, "2.8K @200% icon > 120 (DPI-aware), got " + lay28.Icon);

        // No more 2160 height shrink: short physical height at 96 DPI uses Fit, not H/2160.
        DockLayout shortScreen = DockLayout.For(96, 6, 8, 1920, 1200, 96f, 0.94f);
        int expected = DisplayScale.Px(96, 96f, 0.94f);
        ExpectTrue(shortScreen.Icon == expected,
            "short screen uses Fit Px, got " + shortScreen.Icon + " expected " + expected);

        Console.WriteLine();
        Console.WriteLine("DockLayoutScale tests: {0} passed, {1} failed", _passed, _failed);
        return _failed == 0 ? 0 : 1;
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
}
