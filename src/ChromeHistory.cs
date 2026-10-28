// JumpDock.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

public static class ChromeHistory
{
    public const int CapPerApp = 40;

    private const int SQLITE_OPEN_READONLY = 0x00000001;
    private const int SQLITE_OPEN_NOMUTEX = 0x00008000;
    private const int SQLITE_ROW = 100;
    private const int SQLITE_DONE = 101;

    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int sqlite3_open_v2(byte[] filename, out IntPtr ppDb, int flags, IntPtr zVfs);

    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int sqlite3_close(IntPtr db);

    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int sqlite3_prepare_v2(IntPtr db, byte[] sql, int nByte, out IntPtr stmt, IntPtr pzTail);

    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int sqlite3_step(IntPtr stmt);

    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr sqlite3_column_text(IntPtr stmt, int iCol);

    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern long sqlite3_column_int64(IntPtr stmt, int iCol);

    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int sqlite3_finalize(IntPtr stmt);

    public sealed class Result
    {
        public bool Ok;
        public readonly Dictionary<string, List<JumpItem>> Lists =
            new Dictionary<string, List<JumpItem>>(StringComparer.OrdinalIgnoreCase);
    }

    public static Result TryRead()
    {
        var result = new Result();
        EnsureList(result, ChromeAppUrl.GitHubPr);
        EnsureList(result, ChromeAppUrl.Jira);
        EnsureList(result, ChromeAppUrl.Confluence);

        string home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"Google\Chrome\User Data\Default");
        string hist = Path.Combine(home, "History");
        if (!File.Exists(hist))
            return result;

        string dir = Path.Combine(Path.GetTempPath(), "wa_chromehist_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir);
            if (!CopyShared(hist, Path.Combine(dir, "History")))
                return result;
            CopyShared(Path.Combine(home, "History-wal"), Path.Combine(dir, "History-wal"));
            CopyShared(Path.Combine(home, "History-shm"), Path.Combine(dir, "History-shm"));
            if (!Query(Path.Combine(dir, "History"), result))
                return result;
            result.Ok = true;
            return result;
        }
        catch
        {
            return new Result();
        }
        finally
        {
            try { Directory.Delete(dir, true); }
            catch { }
        }
    }

    private static void EnsureList(Result result, string name)
    {
        result.Lists[name] = new List<JumpItem>();
    }

    private static bool CopyShared(string src, string dest)
    {
        if (!File.Exists(src))
            return false;
        try
        {
            using (var input = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var output = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var buf = new byte[64 * 1024];
                int n;
                while ((n = input.Read(buf, 0, buf.Length)) > 0)
                    output.Write(buf, 0, n);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool Query(string dbPath, Result result)
    {
        IntPtr db;
        byte[] name = Utf8Z(dbPath);
        int flags = SQLITE_OPEN_READONLY | SQLITE_OPEN_NOMUTEX;
        if (sqlite3_open_v2(name, out db, flags, IntPtr.Zero) != 0 || db == IntPtr.Zero)
            return false;
        IntPtr stmt = IntPtr.Zero;
        try
        {
            const string sql =
                "SELECT url, title, visit_count, last_visit_time FROM urls " +
                "WHERE (url LIKE '%github.com%' OR url LIKE '%atlassian.net%') " +
                "AND url LIKE 'http%' " +
                "ORDER BY last_visit_time DESC LIMIT 800";
            if (sqlite3_prepare_v2(db, Utf8Z(sql), -1, out stmt, IntPtr.Zero) != 0 || stmt == IntPtr.Zero)
                return false;

            var seen = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            int rc;
            while ((rc = sqlite3_step(stmt)) == SQLITE_ROW)
            {
                string url = PtrUtf8(sqlite3_column_text(stmt, 0));
                string title = PtrUtf8(sqlite3_column_text(stmt, 1));
                long chromeTime = sqlite3_column_int64(stmt, 3);
                AddRow(result, seen, url, title, chromeTime);
            }
            return rc == SQLITE_DONE;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (stmt != IntPtr.Zero)
                sqlite3_finalize(stmt);
            sqlite3_close(db);
        }
    }

    private static void AddRow(Result result, Dictionary<string, bool> seen, string url, string title, long chromeTime)
    {
        string dock = ChromeAppUrl.Classify(url);
        if (dock == null)
            return;
        string canon = ChromeAppUrl.Canonical(url);
        string key = dock + "|" + canon;
        if (seen.ContainsKey(key))
            return;
        List<JumpItem> list;
        if (!result.Lists.TryGetValue(dock, out list) || list.Count >= CapPerApp)
            return;
        seen[key] = true;
        list.Add(new JumpItem
        {
            kind = "url",
            title = ChromeAppUrl.Title(canon, title),
            target = canon,
            lastUsed = FromChromeTime(chromeTime)
        });
    }

    private static string FromChromeTime(long micros)
    {
        if (micros <= 0)
            return DateTime.UtcNow.ToString("o");
        try
        {
            var t = DateTime.FromFileTimeUtc(micros * 10);
            return t.ToString("o");
        }
        catch
        {
            return DateTime.UtcNow.ToString("o");
        }
    }

    private static byte[] Utf8Z(string s)
    {
        var utf = Encoding.UTF8.GetBytes(s ?? "");
        var z = new byte[utf.Length + 1];
        Buffer.BlockCopy(utf, 0, z, 0, utf.Length);
        return z;
    }

    private static string PtrUtf8(IntPtr p)
    {
        if (p == IntPtr.Zero)
            return "";
        int len = 0;
        while (Marshal.ReadByte(p, len) != 0)
            len++;
        if (len == 0)
            return "";
        var buf = new byte[len];
        Marshal.Copy(p, buf, 0, len);
        return Encoding.UTF8.GetString(buf);
    }
}
