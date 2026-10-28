using System;

/// <summary>
/// Jump source registry: Cursor is WindowsDest; Edge has no refresh collector.
/// </summary>
internal static class JumpListsTests
{
    private static int _passed;
    private static int _failed;

    private static int Main()
    {
        Expect(JumpSource.AppHistory, JumpLists.SourceOf("VS Code"), "VS Code AppHistory");
        Expect(JumpSource.AppHistory, JumpLists.SourceOf("Chrome"), "Chrome AppHistory");
        Expect(JumpSource.AppHistory, JumpLists.SourceOf("IntelliJ IDEA"), "IntelliJ AppHistory");
        Expect(JumpSource.WindowsDest, JumpLists.SourceOf("Cursor"), "Cursor WindowsDest");
        Expect(JumpSource.WindowsDest, JumpLists.SourceOf("cursor"), "Cursor case-insensitive");
        Expect(JumpSource.None, JumpLists.SourceOf("Edge"), "Edge None");
        Expect(JumpSource.None, JumpLists.SourceOf("Teams"), "Teams None");
        Expect(JumpSource.None, JumpLists.SourceOf(""), "empty None");
        Expect(JumpSource.None, JumpLists.SourceOf(null), "null None");

        Expect(true, JumpLists.Supports("Cursor"), "Supports Cursor");
        Expect(true, JumpLists.CanRefresh("Cursor"), "CanRefresh Cursor");
        Expect(true, JumpLists.CanRefresh("VS Code"), "CanRefresh VS Code");
        Expect(false, JumpLists.CanRefresh("Edge"), "CanRefresh Edge false");
        Expect(false, JumpLists.Supports("Company Portal"), "Supports Company Portal false");

        string[] history = JumpLists.AppHistoryNames();
        bool hasCursorInHistory = false;
        bool hasVs = false;
        for (int i = 0; i < history.Length; i++)
        {
            if (string.Equals(history[i], "Cursor", StringComparison.OrdinalIgnoreCase))
                hasCursorInHistory = true;
            if (string.Equals(history[i], "VS Code", StringComparison.OrdinalIgnoreCase))
                hasVs = true;
        }
        Expect(false, hasCursorInHistory, "AppHistoryNames excludes Cursor");
        Expect(true, hasVs, "AppHistoryNames includes VS Code");

        Console.WriteLine();
        Console.WriteLine("JumpLists tests: {0} passed, {1} failed", _passed, _failed);
        return _failed == 0 ? 0 : 1;
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
}
