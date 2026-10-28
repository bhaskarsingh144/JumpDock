using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Jump list persistence rules used on Reload / Sync. Empty harvest must not
/// wipe stored recents. Run via scripts\test.ps1 (no NuGet).
/// </summary>
internal static class JumpListStoreTests
{
    private static int _passed;
    private static int _failed;

    private static int Main()
    {
        string dir = Path.Combine(Path.GetTempPath(), "JumpDockTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "jumplists.json");
        try
        {
            // Seed recents
            var store = new JumpListStore(path);
            store.ReplaceRecents("VS Code", new List<JumpItem>
            {
                Item("folder", "proj-a", @"C:\work\proj-a"),
                Item("folder", "proj-b", @"C:\work\proj-b")
            });
            Expect(2, store.For("VS Code").recent.Count, "seed two recents");

            // Empty ReplaceRecents must keep them (Reload / failed CollectFor)
            store.ReplaceRecents("VS Code", new List<JumpItem>());
            Expect(2, store.For("VS Code").recent.Count, "empty list keeps recents");
            store.ReplaceRecents("VS Code", null);
            Expect(2, store.For("VS Code").recent.Count, "null list keeps recents");

            // Reload from disk still has them
            var again = new JumpListStore(path);
            Expect(2, again.For("VS Code").recent.Count, "reload from disk keeps recents");

            // Non-empty replace updates
            again.ReplaceRecents("VS Code", new List<JumpItem>
            {
                Item("folder", "proj-c", @"C:\work\proj-c")
            });
            Expect(1, again.For("VS Code").recent.Count, "non-empty replace updates");
            Expect(@"C:\work\proj-c", again.For("VS Code").recent[0].target, "replace target is proj-c");

            // ClearRecent is the intentional wipe
            again.ClearRecent("VS Code");
            Expect(0, again.For("VS Code").recent.Count, "ClearRecent wipes");

            // Pins survive ReplaceRecents / empty
            var pins = new JumpListStore(path);
            pins.Pin("IntelliJ IDEA", Item("folder", "idea", @"C:\work\idea"));
            pins.ReplaceRecents("IntelliJ IDEA", new List<JumpItem>
            {
                Item("folder", "other", @"C:\work\other")
            });
            Expect(1, pins.For("IntelliJ IDEA").pinned.Count, "pin kept after replace");
            pins.ReplaceRecents("IntelliJ IDEA", new List<JumpItem>());
            Expect(1, pins.For("IntelliJ IDEA").pinned.Count, "pin kept after empty replace");
            Expect(1, pins.For("IntelliJ IDEA").recent.Count, "recent kept after empty replace");

            // MergeRecent is additive
            pins.MergeRecent("Cursor", new List<JumpItem>
            {
                Item("folder", "c1", @"C:\work\c1")
            });
            pins.MergeRecent("Cursor", new List<JumpItem>
            {
                Item("folder", "c2", @"C:\work\c2")
            });
            Expect(2, pins.For("Cursor").recent.Count, "MergeRecent additive");
            pins.MergeRecent("Cursor", new List<JumpItem>());
            Expect(2, pins.For("Cursor").recent.Count, "MergeRecent empty no-op");

            // JumpLists.Supports
            Expect(true, JumpLists.Supports("Chrome"), "Supports Chrome");
            Expect(true, JumpLists.Supports("vs code"), "Supports case-insensitive");
            Expect(true, JumpLists.Supports("Cursor"), "Cursor WindowsDest is supported for Refresh");
            Expect(JumpSource.WindowsDest, JumpLists.SourceOf("Cursor"), "Cursor source WindowsDest");
            Expect(false, JumpLists.Supports("Edge"), "Edge has no jump collector");
            Expect(false, JumpLists.Supports(""), "empty name unsupported");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }

        Console.WriteLine();
        Console.WriteLine("JumpListStore tests: {0} passed, {1} failed", _passed, _failed);
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

    private static void Expect(string expected, string actual, string name)
    {
        if (string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
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
