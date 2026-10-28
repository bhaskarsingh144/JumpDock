// JumpDock — which tiles have jump menus and how Refresh collects them.
using System;

public enum JumpSource
{
    /// <summary>No jump harvest on Refresh; keep store; hide Refresh.</summary>
    None,
    /// <summary>App-owned history via AppRecents.CollectFor.</summary>
    AppHistory,
    /// <summary>Windows Automatic/Custom Destinations via WindowsJumpList.</summary>
    WindowsDest
}

public static class JumpLists
{
    private struct Entry
    {
        public string Name;
        public JumpSource Source;
        public Entry(string name, JumpSource source)
        {
            Name = name;
            Source = source;
        }
    }

    private static readonly Entry[] Known = new Entry[]
    {
        new Entry("VS Code", JumpSource.AppHistory),
        new Entry("IntelliJ IDEA", JumpSource.AppHistory),
        new Entry("Notepad++", JumpSource.AppHistory),
        new Entry("Chrome", JumpSource.AppHistory),
        new Entry("File Explorer", JumpSource.AppHistory),
        new Entry("GitHub PR", JumpSource.AppHistory),
        new Entry("Jira", JumpSource.AppHistory),
        new Entry("Confluence", JumpSource.AppHistory),
        new Entry("GitHub Desktop", JumpSource.AppHistory),
        new Entry("MongoDB Compass", JumpSource.AppHistory),
        new Entry("Lens", JumpSource.AppHistory),
        new Entry("Termius", JumpSource.AppHistory),
        // Windows destinations only (no AppRecents collector)
        new Entry("Cursor", JumpSource.WindowsDest)
    };

    public static string[] Names()
    {
        string[] names = new string[Known.Length];
        for (int i = 0; i < Known.Length; i++)
            names[i] = Known[i].Name;
        return names;
    }

    public static string[] AppHistoryNames()
    {
        int n = 0;
        for (int i = 0; i < Known.Length; i++)
        {
            if (Known[i].Source == JumpSource.AppHistory)
                n++;
        }
        string[] names = new string[n];
        int j = 0;
        for (int i = 0; i < Known.Length; i++)
        {
            if (Known[i].Source == JumpSource.AppHistory)
                names[j++] = Known[i].Name;
        }
        return names;
    }

    public static JumpSource SourceOf(string name)
    {
        if (string.IsNullOrEmpty(name))
            return JumpSource.None;
        for (int i = 0; i < Known.Length; i++)
        {
            if (string.Equals(Known[i].Name, name, StringComparison.OrdinalIgnoreCase))
                return Known[i].Source;
        }
        return JumpSource.None;
    }

    /// <summary>True when Refresh should run a collector (not wipe / no-op).</summary>
    public static bool Supports(string name)
    {
        JumpSource s = SourceOf(name);
        return s == JumpSource.AppHistory || s == JumpSource.WindowsDest;
    }

    public static bool CanRefresh(string name)
    {
        return Supports(name);
    }
}
