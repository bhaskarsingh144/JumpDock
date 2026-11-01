using System;

/// <summary>Category map + rank for More apps discovery.</summary>
internal static class AppCategoriesTests
{
    private static int _passed;
    private static int _failed;

    private static int Main()
    {
        Expect(AppCategories.Communication, AppCategories.Resolve("Discord", "", null), "Discord → Communication");
        Expect(AppCategories.Browsers, AppCategories.Resolve("Firefox", "", null), "Firefox → Browsers");
        Expect(AppCategories.Browsers, AppCategories.Resolve("Firefox (Nightly)", "", null), "Nightly → Browsers");
        Expect(AppCategories.Development, AppCategories.Resolve("IntelliJ IDEA", "", null), "IntelliJ → Development");
        Expect(AppCategories.Development, AppCategories.Resolve("WebStorm", @"C:\Program Files\JetBrains\WebStorm\bin\webstorm64.exe", null), "WebStorm → Development");
        Expect(AppCategories.Utilities, AppCategories.Resolve("Geek Uninstaller", "", null), "Geek → Utilities");
        Expect(AppCategories.Multimedia, AppCategories.Resolve("Spotify", "", null), "Spotify → Multimedia");
        Expect(AppCategories.Communication, AppCategories.Resolve("Slack", "", null), "Slack → Communication");
        Expect(AppCategories.Other, AppCategories.Resolve("Some Obscure Tool", "", null), "unknown → Other");
        Expect(AppCategories.Development, AppCategories.Resolve("MyApp", "", AppCategories.Development), "curated Group wins");

        Expect(true, AppCategories.Rank(AppCategories.Development) < AppCategories.Rank(AppCategories.Other), "Development before Other");
        Expect(true, AppCategories.Rank(AppCategories.Browsers) < AppCategories.Rank(AppCategories.Games), "Browsers before Games");

        Console.WriteLine();
        Console.WriteLine("AppCategories tests: {0} passed, {1} failed", _passed, _failed);
        return _failed == 0 ? 0 : 1;
    }

    private static void Expect(string expected, string actual, string name)
    {
        if (string.Equals(expected, actual, StringComparison.Ordinal))
        {
            _passed++;
            Console.WriteLine("  PASS  {0}", name);
        }
        else
        {
            _failed++;
            Console.WriteLine("  FAIL  {0}: expected '{1}', got '{2}'", name, expected, actual);
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
