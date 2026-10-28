using System;

/// <summary>
/// DockLayout DIP → physical icon scaling and width fit across target displays.
/// Run via scripts\test.ps1.
/// </summary>
internal static class DockLayoutScaleTests
{
    private static int _passed;
    private static int _failed;

    private static int Main()
    {
        DpiTracksDesignUnits();
        WorkMonitor4K();
        OfficeLaptop();
        CurrentLaptop28K();
        WidthFitPressure();
        SettingsIconChoices();
        NoLegacyHeightShrink();

        Console.WriteLine();
        Console.WriteLine("DockLayoutScale tests: {0} passed, {1} failed", _passed, _failed);
        return _failed == 0 ? 0 : 1;
    }

    private static void DpiTracksDesignUnits()
    {
        const int iconDip = 96;
        const int peekDip = 6;
        const int count = 10;

        DockLayout at96 = DockLayout.For(iconDip, peekDip, count, 3840, 2160, 96f, 1f);
        DockLayout at192 = DockLayout.For(iconDip, peekDip, count, 3840, 2160, 192f, 1f);
        ExpectTrue(at96.Icon == 96, "96 DPI icon physical == 96, got " + at96.Icon);
        ExpectTrue(at192.Icon == 192, "192 DPI icon physical == 192, got " + at192.Icon);
        ExpectTrue(at192.Icon > at96.Icon, "higher DPI → larger physical icon");
        ExpectTrue(at192.BarH > at96.BarH, "higher DPI → taller bar");
        ExpectTrue(at192.Radius > at96.Radius, "higher DPI → larger radius");
        ExpectTrue(at192.Peek > at96.Peek, "peek DIP scales with DPI");
    }

    private static void WorkMonitor4K()
    {
        DisplayProfile fourK = DisplayScale.FromPhysical(3840, 2160, 144f, 27.0f, false);
        DockLayout k4 = DockLayout.For(96, 6, 12, 3840, 2160, fourK);
        ExpectTrue(k4.Icon == DisplayScale.Px(96, 144f, fourK.Fit), "4K layout icon matches Px");
        ExpectTrue(k4.Icon >= 140, "4K @150% icon roughly ≥140, got " + k4.Icon);
        ExpectTrue(k4.WidthFor(12) <= 3840, "4K bar width ≤ screen");
        ExpectTrue(!k4.Shrunk, "4K with 12 tiles not Shrunk");
    }

    private static void OfficeLaptop()
    {
        DisplayProfile office = DisplayScale.FromPhysical(1920, 1200, 96f, 16.0f, true);
        DockLayout officeLay = DockLayout.For(96, 6, 12, 1920, 1200, office);
        ExpectTrue(officeLay.WidthFor(12) <= 1920, "office layout width ≤ screen");
        ExpectTrue(officeLay.Icon <= 96, "office Fit does not grow past DIP at 96 DPI");
        ExpectTrue(officeLay.Icon >= 40, "office icon above floor");

        DisplayProfile office125 = DisplayScale.FromPhysical(1920, 1200, 120f, 16.0f, true);
        DockLayout lay125 = DockLayout.For(96, 6, 12, 1920, 1200, office125);
        ExpectTrue(lay125.Icon > officeLay.Icon, "office @125% icon > @100%");
        ExpectTrue(lay125.WidthFor(12) <= 1920, "office @125% still width-fits 12 tiles");
    }

    private static void CurrentLaptop28K()
    {
        DisplayProfile k28 = DisplayScale.FromPhysical(2880, 1800, 192f, 16.0f, true);
        DockLayout lay28 = DockLayout.For(96, 6, 12, 2880, 1800, k28);
        ExpectTrue(lay28.Icon > 120, "2.8K @200% icon > 120 (DPI-aware), got " + lay28.Icon);
        ExpectTrue(lay28.WidthFor(12) <= 2880, "2.8K bar width ≤ screen");

        DisplayProfile k28_150 = DisplayScale.FromPhysical(2880, 1800, 144f, 16.0f, true);
        DockLayout lay150 = DockLayout.For(96, 6, 12, 2880, 1800, k28_150);
        ExpectTrue(lay28.Icon > lay150.Icon, "2.8K @200% icon > @150%");
    }

    private static void WidthFitPressure()
    {
        // Crowded 1920-wide still binary-searches down instead of overflowing.
        DockLayout crowded = DockLayout.For(96, 6, 20, 1920, 1200, 96f, 1f);
        ExpectTrue(crowded.WidthFor(20) <= 1920 - 64, "crowded 20 tiles width-fit on 1920");
        ExpectTrue(crowded.Shrunk || crowded.Icon < 96, "crowded marks shrunk or reduced icon");

        // High DPI + many pins on 2.8K still fits.
        DisplayProfile k28 = DisplayScale.FromPhysical(2880, 1800, 192f, 16.0f, true);
        DockLayout many = DockLayout.For(96, 6, 18, 2880, 1800, k28);
        ExpectTrue(many.WidthFor(18) <= 2880, "2.8K 18 tiles width-fit");
    }

    private static void SettingsIconChoices()
    {
        // Settings offers 72 / 84 / 96 design sizes — relative order preserved at 150% DPI.
        DisplayProfile fourK = DisplayScale.FromPhysical(3840, 2160, 144f, 27.0f, false);
        DockLayout s72 = DockLayout.For(72, 6, 10, 3840, 2160, fourK);
        DockLayout s84 = DockLayout.For(84, 6, 10, 3840, 2160, fourK);
        DockLayout s96 = DockLayout.For(96, 6, 10, 3840, 2160, fourK);
        ExpectTrue(s72.Icon < s84.Icon && s84.Icon < s96.Icon, "design 72 < 84 < 96 at 4K@150%");
    }

    private static void NoLegacyHeightShrink()
    {
        // Old bug: screenH&lt;2000 applied H/2160. Now Fit drives size.
        DockLayout shortScreen = DockLayout.For(96, 6, 8, 1920, 1200, 96f, 0.94f);
        int expected = DisplayScale.Px(96, 96f, 0.94f);
        ExpectTrue(shortScreen.Icon == expected,
            "short screen uses Fit Px, got " + shortScreen.Icon + " expected " + expected);

        // Legacy would have been Round(96 * 1200/2160) ≈ 53; we must stay near Fit Px (~90).
        ExpectTrue(shortScreen.Icon > 70, "not legacy 2160-shrink (~53), got " + shortScreen.Icon);
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
