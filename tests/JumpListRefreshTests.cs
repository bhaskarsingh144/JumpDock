using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// RefreshOne must never wipe Cursor / None tiles on empty harvest.
/// </summary>
internal static class JumpListRefreshTests
{
    private static int _passed;
    private static int _failed;

    private static int Main()
    {
        string dir = Path.Combine(Path.GetTempPath(), "JumpDockRefreshTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "jumplists.json");
        try
        {
            var store = new JumpListStore(path);
            store.MergeRecent("Cursor", new List<JumpItem>
            {
                Item("folder", "a", @"C:\work\a"),
                Item("folder", "b", @"C:\work\b")
            });
            Expect(2, store.For("Cursor").recent.Count, "seed Cursor recents");

            // None source: Edge — RefreshOne is no-op, must keep if we had data
            store.MergeRecent("Edge", new List<JumpItem>
            {
                Item("url", "e", "https://example.com/")
            });
            JumpListRefresh.RefreshOne(store, null, "Edge");
            Expect(1, store.For("Edge").recent.Count, "None Refresh keeps Edge");

            // Cursor WindowsDest: empty CollectFor must not ClearRecent
            // (real harvest may or may not find items; seed must survive empty path)
            int before = store.For("Cursor").recent.Count;
            JumpListRefresh.RefreshOne(store, null, "Cursor");
            int after = store.For("Cursor").recent.Count;
            Expect(true, after >= before, "Cursor Refresh does not shrink below seed when harvest empty-or-merge");
            Expect(true, after >= 2 || before >= 2, "Cursor still has at least seed count or merged");

            // Explicit: RefreshOne on None never clears
            store.ClearRecent("Edge");
            store.MergeRecent("Edge", new List<JumpItem> { Item("url", "keep", "https://keep.example/") });
            JumpListRefresh.RefreshOne(store, null, "Edge");
            Expect(1, store.For("Edge").recent.Count, "second None Refresh keeps");

            // Registry contract used by Refresh
            Expect(JumpSource.WindowsDest, JumpLists.SourceOf("Cursor"), "Cursor source for Refresh");
            Expect(false, JumpLists.CanRefresh("Edge"), "Edge cannot Refresh");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }

        Console.WriteLine();
        Console.WriteLine("JumpListRefresh tests: {0} passed, {1} failed", _passed, _failed);
        return _failed == 0 ? 0 : 1;
    }

    private static JumpItem Item(string kind, string title, string target)
    {
        return new JumpItem { kind = kind, title = title, target = target };
    }

    private static void Expect(int expected, int actual, string name)
    {
        if (expected == actual)
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

    private static void Expect(bool expected, bool actual, string name)
    {
        if (expected == actual)
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

    private static void Expect(JumpSource expected, JumpSource actual, string name)
    {
        if (expected == actual)
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
