// JumpDock — place settings/popups under the dock; hover-bridge geometry (testable).
using System;
using System.Drawing;

public static class PopupPlacement
{
    public const int DefaultDockClearDip = 56;
    public const int DefaultBridgePadDip = 8;
    public const int DeactivateGraceMs = 500;

    /// <summary>
    /// Horizontally center under the dock: y = work.Top + dockClearance (not vertical center).
    /// </summary>
    public static Point PlaceUnderDock(Size formSize, Rectangle work, int dockClearPx)
    {
        if (dockClearPx < 0) dockClearPx = 0;
        int x = work.Left + Math.Max(0, (work.Width - formSize.Width) / 2);
        int y = work.Top + dockClearPx;
        if (y + formSize.Height > work.Bottom)
        {
            // Prefer under-dock clearance; if the form is taller than remaining space,
            // clamp to work.Top so the window still fits.
            y = work.Bottom - formSize.Height;
            if (y < work.Top) y = work.Top;
            if (y < work.Top + dockClearPx && formSize.Height <= work.Height - dockClearPx)
                y = work.Top + dockClearPx;
        }
        if (y < work.Top) y = work.Top;
        if (x + formSize.Width > work.Right)
            x = Math.Max(work.Left, work.Right - formSize.Width);
        if (x < work.Left) x = work.Left;
        return new Point(x, y);
    }

    /// <summary>
    /// Rectangle covering the gap between dock bottom and popup top (plus side pad).
    /// Cursor in this region should cancel popup auto-close.
    /// </summary>
    public static Rectangle BridgeRect(Rectangle dockBounds, Rectangle popupBounds, int sidePadPx)
    {
        int left = Math.Min(dockBounds.Left, popupBounds.Left) - sidePadPx;
        int right = Math.Max(dockBounds.Right, popupBounds.Right) + sidePadPx;
        int top = Math.Min(dockBounds.Bottom, popupBounds.Top);
        int bottom = Math.Max(dockBounds.Bottom, popupBounds.Top);
        if (bottom < top) bottom = top;
        // Always at least 1px tall so Contains works when flush.
        if (bottom == top) bottom = top + 1;
        return Rectangle.FromLTRB(left, top, right, bottom);
    }

    public static bool PointInBridgeOrPopup(Point screenPt, Rectangle dockBounds, Rectangle popupBounds, int sidePadPx)
    {
        if (popupBounds.Contains(screenPt))
            return true;
        return BridgeRect(dockBounds, popupBounds, sidePadPx).Contains(screenPt);
    }
}
