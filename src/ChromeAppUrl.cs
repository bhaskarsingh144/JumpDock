// JumpDock.
using System;
using System.Text.RegularExpressions;

public static class ChromeAppUrl
{
    public const string GitHubPr = "GitHub PR";
    public const string Jira = "Jira";
    public const string Confluence = "Confluence";

    public static string Classify(string url)
    {
        Uri u;
        if (!TryParse(url, out u))
            return null;
        string host = (u.Host ?? "").ToLowerInvariant();
        if (host.StartsWith("www."))
            host = host.Substring(4);
        string path = u.AbsolutePath ?? "";
        if (host == "github.com")
            return ClassifyGitHub(path);
        if (host.EndsWith(".atlassian.net") || host == "atlassian.net")
            return ClassifyAtlassian(path);
        return null;
    }

    public static string Canonical(string url)
    {
        Uri u;
        if (!TryParse(url, out u))
            return url;
        string host = (u.Host ?? "").ToLowerInvariant();
        if (host.StartsWith("www."))
            host = host.Substring(4);
        string path = (u.AbsolutePath ?? "/").TrimEnd('/');
        if (path.Length == 0)
            path = "/";

        if (host == "github.com")
        {
            var m = Regex.Match(path, "^/([^/]+)/([^/]+)/pull/([0-9]+)", RegexOptions.IgnoreCase);
            if (m.Success)
                path = "/" + m.Groups[1].Value + "/" + m.Groups[2].Value + "/pull/" + m.Groups[3].Value;
        }
        else if (host.EndsWith(".atlassian.net") || host == "atlassian.net")
        {
            var browse = Regex.Match(path, "/browse/([A-Za-z][A-Za-z0-9]+-\\d+)", RegexOptions.IgnoreCase);
            if (browse.Success)
                path = "/browse/" + browse.Groups[1].Value.ToUpperInvariant();
        }

        return "https://" + host + path;
    }

    public static string Title(string url, string historyTitle)
    {
        string kind = Classify(url);
        Uri u;
        if (!TryParse(url, out u))
            return FirstUseful(historyTitle, url);

        string path = u.AbsolutePath ?? "";
        var segs = path.Trim('/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

        if (kind == GitHubPr)
        {
            var pull = Regex.Match(path, "^/([^/]+)/([^/]+)/pull/([0-9]+)", RegexOptions.IgnoreCase);
            if (pull.Success)
                return pull.Groups[1].Value + "/" + pull.Groups[2].Value + " #" + pull.Groups[3].Value;
            if (segs.Length >= 1 && string.Equals(segs[0], "pulls", StringComparison.OrdinalIgnoreCase))
            {
                if (segs.Length == 1)
                    return "Pull requests";
                return "Pulls / " + segs[1];
            }
            if (segs.Length >= 2)
                return segs[0] + "/" + segs[1];
        }
        if (kind == Jira)
        {
            var key = Regex.Match(path, "/browse/([A-Za-z][A-Za-z0-9]+-\\d+)", RegexOptions.IgnoreCase);
            if (key.Success)
                return key.Groups[1].Value.ToUpperInvariant();
            string fromTitle = JiraKeyFromTitle(historyTitle);
            if (fromTitle != null)
                return fromTitle;
        }
        if (kind == Confluence)
        {
            string t = CleanHistoryTitle(historyTitle);
            if (!string.IsNullOrEmpty(t))
                return t;
            if (segs.Length > 0)
            {
                try { return Uri.UnescapeDataString(segs[segs.Length - 1]).Replace('+', ' '); }
                catch { return segs[segs.Length - 1]; }
            }
        }

        string hist = CleanHistoryTitle(historyTitle);
        if (!string.IsNullOrEmpty(hist))
            return hist;
        if (segs.Length > 0)
        {
            try { return Uri.UnescapeDataString(segs[segs.Length - 1]); }
            catch { return segs[segs.Length - 1]; }
        }
        return u.Host;
    }

    public static bool HasStoredLabel(string title, string target)
    {
        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(target))
            return false;
        if (title.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return false;
        if (string.Equals(title.Trim(), target.Trim(), StringComparison.OrdinalIgnoreCase))
            return false;
        return title.Trim().Length > 0;
    }

    private static string ClassifyGitHub(string path)
    {
        var segs = (path ?? "").Trim('/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (segs.Length == 0)
            return null;
        string a = segs[0].ToLowerInvariant();
        if (a == "login" || a == "settings" || a == "notifications" || a == "orgs" ||
            a == "marketplace" || a == "features" || a == "pricing" || a == "about" ||
            a == "explore" || a == "topics" || a == "sponsors" || a == "account" ||
            a == "sessions" || a == "security" || a == "enterprise" || a == "team")
            return null;
        if (a == "pulls")
            return GitHubPr;
        if (segs.Length >= 4 && string.Equals(segs[2], "pull", StringComparison.OrdinalIgnoreCase))
            return GitHubPr;
        if (segs.Length >= 3 && string.Equals(segs[2], "pulls", StringComparison.OrdinalIgnoreCase))
            return GitHubPr;
        if (segs.Length == 2)
            return GitHubPr;
        return null;
    }

    private static string ClassifyAtlassian(string path)
    {
        string p = path ?? "";
        if (p.IndexOf("/wiki", StringComparison.OrdinalIgnoreCase) >= 0)
            return Confluence;
        if (p.IndexOf("/jira", StringComparison.OrdinalIgnoreCase) >= 0)
            return Jira;
        if (Regex.IsMatch(p, "/browse/[A-Za-z][A-Za-z0-9]+-\\d+", RegexOptions.IgnoreCase))
            return Jira;
        return null;
    }

    private static bool TryParse(string url, out Uri u)
    {
        u = null;
        if (string.IsNullOrEmpty(url))
            return false;
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return false;
        try
        {
            u = new Uri(url);
        }
        catch
        {
            return false;
        }
        if (u.Scheme != "http" && u.Scheme != "https")
            return false;
        if (string.IsNullOrEmpty(u.Host))
            return false;
        return true;
    }

    private static string CleanHistoryTitle(string title)
    {
        if (string.IsNullOrEmpty(title))
            return null;
        title = title.Trim();
        if (title.Length == 0 || title.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return null;
        int cut = title.IndexOf(" - Jira", StringComparison.OrdinalIgnoreCase);
        if (cut > 0)
            title = title.Substring(0, cut).Trim();
        cut = title.IndexOf(" - Confluence", StringComparison.OrdinalIgnoreCase);
        if (cut > 0)
            title = title.Substring(0, cut).Trim();
        cut = title.IndexOf(" · GitHub", StringComparison.OrdinalIgnoreCase);
        if (cut > 0)
            title = title.Substring(0, cut).Trim();
        if (title.Length > 80)
            title = title.Substring(0, 80).Trim();
        return title.Length == 0 ? null : title;
    }

    private static string JiraKeyFromTitle(string title)
    {
        if (string.IsNullOrEmpty(title))
            return null;
        var m = Regex.Match(title, @"\b([A-Za-z][A-Za-z0-9]+-\d+)\b");
        return m.Success ? m.Groups[1].Value.ToUpperInvariant() : null;
    }

    private static string FirstUseful(string historyTitle, string url)
    {
        string t = CleanHistoryTitle(historyTitle);
        return t ?? url;
    }
}
