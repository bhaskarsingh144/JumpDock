using System;
using System.Drawing;

/// <summary>Settings/popup placement under dock + hover bridge.</summary>
internal static class PopupPlacementTests
{
    private static int _passed;
    private static int _failed;

    private static int Main()
    {
        var work = new Rectangle(0, 0, 1920, 1080);
        Size form = new Size(900, 700);
        Point p = PopupPlacement.PlaceUnderDock(form, work, 56);
        Expect(true, p.Y == 56, "under-dock Y == clearance");
        Expect(true, p.X == (1920 - 900) / 2, "horizontally centered");
        Expect(true, p.X + form.Width <= work.Right, "fits horizontally");
        Expect(true, p.Y + form.Height <= work.Bottom, "fits vertically on 1080");

        // Tall form on short work area: must still fit in the working area.
        var shortWork = new Rectangle(0, 0, 1280, 600);
        Point p2 = PopupPlacement.PlaceUnderDock(new Size(900, 580), shortWork, 56);
        Expect(true, p2.Y + 580 <= shortWork.Bottom, "short screen fits height");
        Expect(true, p2.Y >= shortWork.Top, "short screen Y within work");
        // Roomier short screen: keep clearance.
        Point p3 = PopupPlacement.PlaceUnderDock(new Size(900, 500), shortWork, 56);
        Expect(true, p3.Y == 56, "roomy short screen keeps clearance");

        var dock = new Rectangle(400, 0, 800, 80);
        var popup = new Rectangle(200, 120, 900, 600);
        Rectangle bridge = PopupPlacement.BridgeRect(dock, popup, 8);
        Expect(true, bridge.Top <= 80 && bridge.Bottom >= 120, "bridge spans dock→popup gap");
        Expect(true, PopupPlacement.PointInBridgeOrPopup(new Point(500, 100), dock, popup, 8), "mid-gap is in bridge");
        Expect(true, PopupPlacement.PointInBridgeOrPopup(new Point(300, 200), dock, popup, 8), "inside popup counts");
        Expect(false, PopupPlacement.PointInBridgeOrPopup(new Point(50, 400), dock, popup, 8), "far left not in bridge");

        // Flush: dock bottom == popup top
        var flushPopup = new Rectangle(400, 80, 800, 500);
        Expect(true, PopupPlacement.PointInBridgeOrPopup(new Point(500, 80), dock, flushPopup, 8), "flush seam still hittable");

        Expect(500, PopupPlacement.DeactivateGraceMs, "grace ms constant");

        Console.WriteLine();
        Console.WriteLine("PopupPlacement tests: {0} passed, {1} failed", _passed, _failed);
        return _failed == 0 ? 0 : 1;
    }

    private static void Expect(bool expected, bool actual, string name)
    {
        if (expected == actual) { _passed++; Console.WriteLine("  PASS  {0}", name); }
        else { _failed++; Console.WriteLine("  FAIL  {0}: expected {1}, got {2}", name, expected, actual); }
    }

    private static void Expect(int expected, int actual, string name)
    {
        if (expected == actual) { _passed++; Console.WriteLine("  PASS  {0}", name); }
        else { _failed++; Console.WriteLine("  FAIL  {0}: expected {1}, got {2}", name, expected, actual); }
    }
}
