// JumpDock — repo-maintained app categories for More apps / catalog.
// Extend Patterns (longest / most specific first) when adding common day-to-day apps.
using System;
using System.IO;

public static class AppCategories
{
    public const string Development = "Development";
    public const string Browsers = "Browsers";
    public const string Communication = "Communication";
    public const string Multimedia = "Multimedia";
    public const string Productivity = "Productivity";
    public const string Utilities = "Utilities";
    public const string Games = "Games";
    public const string Other = "Other";
    public const string Web = "Web";

    private struct Rule
    {
        public string Needle;
        public string Category;
        public bool PathAlso;
        public Rule(string needle, string category, bool pathAlso)
        {
            Needle = needle;
            Category = category;
            PathAlso = pathAlso;
        }
    }

    // Longer / more specific needles first.
    private static readonly Rule[] Patterns = new Rule[]
    {
        new Rule("intellij", Development, true),
        new Rule("webstorm", Development, true),
        new Rule("pycharm", Development, true),
        new Rule("phpstorm", Development, true),
        new Rule("rider", Development, true),
        new Rule("clion", Development, true),
        new Rule("datagrip", Development, true),
        new Rule("goland", Development, true),
        new Rule("rubymine", Development, true),
        new Rule("android studio", Development, true),
        new Rule("visual studio code", Development, true),
        new Rule("vs code", Development, false),
        new Rule("vscode", Development, true),
        new Rule("cursor", Development, true),
        new Rule("github desktop", Development, true),
        new Rule("mongodb compass", Development, true),
        new Rule("compass", Development, false),
        new Rule("notepad++", Development, true),
        new Rule("sublime", Development, true),
        new Rule("jetbrains", Development, true),
        new Rule("docker", Development, true),
        new Rule("postman", Development, true),
        new Rule("lens", Development, true),
        new Rule("openlens", Development, true),
        new Rule("termius", Communication, true),
        new Rule("windows terminal", Utilities, true),
        new Rule("powershell", Utilities, true),
        new Rule("git bash", Development, true),
        new Rule("gitkraken", Development, true),
        new Rule("sourcetree", Development, true),
        new Rule("firefox nightly", Browsers, true),
        new Rule("firefox developer", Browsers, true),
        new Rule("firefox", Browsers, true),
        new Rule("google chrome", Browsers, true),
        new Rule("chrome", Browsers, false),
        new Rule("microsoft edge", Browsers, true),
        new Rule("msedge", Browsers, true),
        new Rule("brave", Browsers, true),
        new Rule("opera", Browsers, true),
        new Rule("vivaldi", Browsers, true),
        new Rule("discord", Communication, true),
        new Rule("slack", Communication, true),
        new Rule("microsoft teams", Communication, true),
        new Rule("teams", Communication, false),
        new Rule("zoom", Communication, true),
        new Rule("telegram", Communication, true),
        new Rule("whatsapp", Communication, true),
        new Rule("signal", Communication, true),
        new Rule("skype", Communication, true),
        new Rule("spotify", Multimedia, true),
        new Rule("vlc", Multimedia, true),
        new Rule("obs studio", Multimedia, true),
        new Rule("obs64", Multimedia, true),
        new Rule("obs32", Multimedia, true),
        new Rule("audacity", Multimedia, true),
        new Rule("photoshop", Multimedia, true),
        new Rule("lightroom", Multimedia, true),
        new Rule("premiere", Multimedia, true),
        new Rule("after effects", Multimedia, true),
        new Rule("davinci", Multimedia, true),
        new Rule("handbrake", Multimedia, true),
        new Rule("itunes", Multimedia, true),
        new Rule("groove music", Multimedia, true),
        new Rule("photos", Multimedia, false),
        new Rule("notion", Productivity, true),
        new Rule("obsidian", Productivity, true),
        new Rule("onenote", Productivity, true),
        new Rule("outlook", Productivity, true),
        new Rule("word", Productivity, false),
        new Rule("excel", Productivity, false),
        new Rule("powerpoint", Productivity, true),
        new Rule("libreoffice", Productivity, true),
        new Rule("acrobat", Productivity, true),
        new Rule("todoist", Productivity, true),
        new Rule("evernote", Productivity, true),
        new Rule("geek uninstaller", Utilities, true),
        new Rule("geek", Utilities, false),
        new Rule("7-zip", Utilities, true),
        new Rule("winrar", Utilities, true),
        new Rule("everything", Utilities, true),
        new Rule("powertoys", Utilities, true),
        new Rule("sharex", Utilities, true),
        new Rule("notepad", Utilities, false),
        new Rule("calculator", Utilities, true),
        new Rule("snipping", Utilities, true),
        new Rule("paint", Utilities, false),
        new Rule("file explorer", Utilities, true),
        new Rule("explorer", Utilities, false),
        new Rule("steam", Games, true),
        new Rule("epic games", Games, true),
        new Rule("xbox", Games, true),
        new Rule("minecraft", Games, true),
    };

    public static int Rank(string category)
    {
        if (category == Web) return 0;
        if (category == Development) return 1;
        if (category == Browsers) return 2;
        if (category == Communication) return 3;
        if (category == Multimedia) return 4;
        if (category == Productivity) return 5;
        if (category == Utilities) return 6;
        if (category == Games) return 7;
        return 8; // Other
    }

    public static bool IsKnownCategory(string category)
    {
        return Rank(category) < 8 || category == Other || category == Web;
    }

    /// <summary>
    /// Resolve display category for a catalog app. Curated Group wins when already set
    /// to a known bucket; otherwise match name/path patterns.
    /// </summary>
    public static string Resolve(string displayName, string pathOrLnk, string existingGroup)
    {
        if (!string.IsNullOrEmpty(existingGroup))
        {
            if (string.Equals(existingGroup, Web, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(existingGroup, Development, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(existingGroup, Browsers, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(existingGroup, Communication, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(existingGroup, Multimedia, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(existingGroup, Productivity, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(existingGroup, Utilities, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(existingGroup, Games, StringComparison.OrdinalIgnoreCase))
                return existingGroup;
        }

        string name = displayName ?? "";
        string path = pathOrLnk ?? "";
        string nameLower = name.ToLowerInvariant();
        string pathLower = path.ToLowerInvariant();

        for (int i = 0; i < Patterns.Length; i++)
        {
            Rule r = Patterns[i];
            if (nameLower.IndexOf(r.Needle, StringComparison.Ordinal) >= 0)
                return r.Category;
            if (r.PathAlso && pathLower.IndexOf(r.Needle, StringComparison.Ordinal) >= 0)
                return r.Category;
        }

        // Path folder heuristics when name didn't match.
        if (pathLower.IndexOf(@"\jetbrains\") >= 0) return Development;
        if (pathLower.IndexOf(@"\discord\") >= 0) return Communication;
        if (pathLower.IndexOf(@"\slack\") >= 0) return Communication;
        if (pathLower.IndexOf(@"\mozilla firefox\") >= 0) return Browsers;
        if (pathLower.IndexOf(@"\spotify\") >= 0) return Multimedia;
        if (pathLower.IndexOf(@"\steam\") >= 0) return Games;

        return Other;
    }

    public static string ResolveForDiscovered(string displayName, string lnkPath, string targetPath)
    {
        string path = string.IsNullOrEmpty(targetPath) ? lnkPath : targetPath;
        return Resolve(displayName, path, null);
    }
}
