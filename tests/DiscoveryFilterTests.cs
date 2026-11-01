using System;

/// <summary>Start Menu discovery denylist + executable gate.</summary>
internal static class DiscoveryFilterTests
{
    private static int _passed;
    private static int _failed;

    private static int Main()
    {
        Expect(true, AppCatalog.IsDeniedDiscoveryName("Administrative"), "deny Administrative");
        Expect(true, AppCatalog.IsDeniedDiscoveryName("Administrative Tools"), "deny Administrative Tools");
        Expect(true, AppCatalog.IsDeniedDiscoveryName("Audio"), "deny Audio");
        Expect(true, AppCatalog.IsDeniedDiscoveryName("Console"), "deny Console");
        Expect(true, AppCatalog.IsDeniedDiscoveryName("Command Prompt"), "deny Command Prompt");
        Expect(true, AppCatalog.IsDeniedDiscoveryName("Windows PowerShell"), "deny PowerShell");
        Expect(true, AppCatalog.IsDeniedDiscoveryName("Uninstall Discord"), "deny uninstall");
        Expect(true, AppCatalog.IsDeniedDiscoveryName(""), "deny empty");
        Expect(true, AppCatalog.IsDeniedDiscoveryName(null), "deny null");

        Expect(false, AppCatalog.IsDeniedDiscoveryName("Discord"), "allow Discord");
        Expect(false, AppCatalog.IsDeniedDiscoveryName("Firefox"), "allow Firefox");
        Expect(false, AppCatalog.IsDeniedDiscoveryName("Geek Uninstaller"), "allow Geek");
        Expect(false, AppCatalog.IsDeniedDiscoveryName("IntelliJ IDEA"), "allow IntelliJ");

        Expect(true, AppCatalog.IsAppExecutableTarget(@"C:\Windows\System32\notepad.exe") ||
                     !System.IO.File.Exists(@"C:\Windows\System32\notepad.exe"),
            "notepad.exe accepted when present (or skip if missing)");
        // Always-false cases:
        Expect(false, AppCatalog.IsAppExecutableTarget(""), "empty target false");
        Expect(false, AppCatalog.IsAppExecutableTarget(null), "null target false");
        Expect(false, AppCatalog.IsAppExecutableTarget(@"C:\Windows"), "folder target false");
        Expect(false, AppCatalog.IsAppExecutableTarget(@"C:\no\such\app.exe"), "missing exe false");

        Expect(true, AppCatalog.IsExternal(new CatalogApp { Kind = "chromeApp", Group = "Web" }), "chromeApp external");
        Expect(true, AppCatalog.IsExternal(new CatalogApp { Kind = "lnk", Group = "Web" }), "Web group external");
        Expect(false, AppCatalog.IsExternal(new CatalogApp { Kind = "exe", Group = "Development" }), "dev not external");
        Expect(true, AppCatalog.IsInstalledDesktop(new CatalogApp { Kind = "exe", Group = "Browsers", Installed = true }), "installed desktop");
        Expect(false, AppCatalog.IsInstalledDesktop(new CatalogApp { Kind = "chromeApp", Group = "Web", Installed = true }), "chrome not desktop");

        Console.WriteLine();
        Console.WriteLine("DiscoveryFilter tests: {0} passed, {1} failed", _passed, _failed);
        return _failed == 0 ? 0 : 1;
    }

    private static void Expect(bool expected, bool actual, string name)
    {
        if (expected == actual) { _passed++; Console.WriteLine("  PASS  {0}", name); }
        else { _failed++; Console.WriteLine("  FAIL  {0}: expected {1}, got {2}", name, expected, actual); }
    }
}
