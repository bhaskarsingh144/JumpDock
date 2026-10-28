// JumpDock — per-tile jump list refresh.
using System;
using System.Collections.Generic;

public static class JumpListRefresh
{
    public static void RefreshOne(JumpListStore store, DockConfig cfg, string dockName)
    {
        if (store == null || string.IsNullOrEmpty(dockName))
            return;

        JumpSource source = JumpLists.SourceOf(dockName);
        if (source == JumpSource.None)
        {
            // Keep persisted pins/recents. Never ClearRecent on Refresh.
            return;
        }

        var autoTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.Equals(dockName, "Chrome", StringComparison.OrdinalIgnoreCase))
        {
            var hubs = cfg != null && cfg.hubPins != null ? cfg.hubPins : DockDefaults.DefaultHubPins();
            for (int i = 0; i < hubs.Count; i++)
            {
                if (hubs[i] == null || string.IsNullOrEmpty(hubs[i].url))
                    continue;
                if (!string.IsNullOrEmpty(hubs[i].list) &&
                    !string.Equals(hubs[i].list, "Chrome", StringComparison.OrdinalIgnoreCase))
                    continue;
                string canon = ChromeAppUrl.Canonical(hubs[i].url.Trim());
                if (!string.IsNullOrEmpty(canon))
                    autoTargets.Add(canon);
            }
        }

        store.PrunePins(dockName, autoTargets);

        List<JumpItem> recents = null;
        try
        {
            if (source == JumpSource.WindowsDest)
                recents = WindowsJumpList.CollectFor(dockName);
            else
                recents = AppRecents.CollectFor(dockName);
        }
        catch
        {
            recents = null;
        }
        if (recents == null)
            recents = new List<JumpItem>();

        if (source == JumpSource.WindowsDest)
        {
            // Additive: empty Windows harvest must not wipe Cursor (etc.).
            if (recents.Count > 0)
                store.MergeRecent(dockName, recents);
            store.TrimRecent(dockName, 40);
        }
        else
        {
            // ReplaceRecents no-ops when empty (JumpListStore contract).
            store.ReplaceRecents(dockName, recents);
        }

        if (string.Equals(dockName, "Chrome", StringComparison.OrdinalIgnoreCase))
        {
            try { JumpProviderRegistry.SyncHubPins(store, cfg); }
            catch { }
        }
    }
}
