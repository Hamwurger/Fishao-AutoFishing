// Automatic updates from GitHub Releases.
// How to publish a new version:
//   1) raise Updater.Version below (e.g. 1.0.0 -> 1.1.0) and build
//   2) on GitHub: Releases -> "Draft a new release", tag v1.1.0, attach FishaoBot.exe, write what's new, Publish
// Every copy of the program then finds it (on start or with "Check for updates"), downloads it,
// swaps its own exe and restarts. The repository must be public.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Web.Script.Serialization;

[assembly: AssemblyTitle("Fishao Bot")]
[assembly: AssemblyProduct("Fishao Bot")]
[assembly: AssemblyVersion(Updater.Version + ".0")]
[assembly: AssemblyFileVersion(Updater.Version + ".0")]

public static class Updater
{
    public const string Version = "1.2.0";          // THIS program's version - raise it for every release
    public const string DefaultRepo = "Hamwurger/Fishao-AutoFishing";         // "github-user/repository" - built into the exe
    public static string Repo = DefaultRepo;        // FishaoBot.ini "updaterepo=" can override it
    public static bool AutoCheck = true;            // look for a new version when the program starts
    const string AssetName = "FishaoBot.exe";

    public class Release { public string Version, Notes, DownloadUrl, Page; public long Size; }

    public static bool Configured { get { return !string.IsNullOrEmpty(Repo) && Repo.Contains("/"); } }
    static string ExePath { get { return Assembly.GetExecutingAssembly().Location; } }
    static string OldPath { get { return Path.Combine(Path.GetDirectoryName(ExePath), "FishaoBot_old.exe"); } }

    static WebClient Client()
    {
        // GitHub only talks TLS 1.2+; .NET 4 defaults to older versions
        try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { }
        var wc = new WebClient { Encoding = System.Text.Encoding.UTF8 };   // GitHub answers in UTF-8 (Czech letters)
        wc.Headers[HttpRequestHeader.UserAgent] = "FishaoBot-Updater";
        return wc;
    }

    // The newest release when it is newer than this program, otherwise null. Throws on network errors.
    public static Release CheckForNewer()
    {
        if (!Configured) return null;
        string json;
        using (var wc = Client())
        {
            wc.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
            json = wc.DownloadString("https://api.github.com/repos/" + Repo + "/releases/latest");
        }
        var d = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
        if (d == null) return null;
        var r = new Release
        {
            Version = Str(d, "tag_name").TrimStart('v', 'V').Trim(),
            Notes = PlainText(Str(d, "body")),
            Page = Str(d, "html_url")
        };
        var assets = d.ContainsKey("assets") ? d["assets"] as IEnumerable : null;
        if (assets != null)
            foreach (var o in assets)
            {
                var a = o as Dictionary<string, object>;
                if (a == null || !string.Equals(Str(a, "name"), AssetName, StringComparison.OrdinalIgnoreCase)) continue;
                r.DownloadUrl = Str(a, "browser_download_url");
                long n; long.TryParse(Str(a, "size"), out n); r.Size = n;
            }
        if (r.DownloadUrl == null) return null;   // a release without the exe - nothing to install
        return IsNewer(r.Version, Version) ? r : null;
    }

    // GitHub release text (Markdown) -> plain text for the message box: only the "what's new" part,
    // without the warning box (quoted "> " lines) and without ** / # / ` marks
    static string PlainText(string md)
    {
        var lines = new List<string>();
        foreach (var raw in md.Replace("\r", "").Split('\n'))
        {
            string l = raw.TrimEnd();
            if (l.TrimStart().StartsWith(">")) continue;
            l = l.Replace("**", "").Replace("`", "");
            l = System.Text.RegularExpressions.Regex.Replace(l, @"^\s*#+\s*", "");
            l = System.Text.RegularExpressions.Regex.Replace(l, @"^(\s*)[-*]\s+", "$1•  ");
            if (l.Length == 0 && (lines.Count == 0 || lines[lines.Count - 1].Length == 0)) continue;
            lines.Add(l);
        }
        return string.Join("\n", lines.ToArray()).Trim();
    }

    static string Str(Dictionary<string, object> d, string k) { object o; return d.TryGetValue(k, out o) && o != null ? Convert.ToString(o, System.Globalization.CultureInfo.InvariantCulture) : ""; }

    static bool IsNewer(string a, string b)
    {
        Version va, vb;
        if (!System.Version.TryParse(Fix(a), out va) || !System.Version.TryParse(Fix(b), out vb)) return false;
        return va > vb;
    }
    static string Fix(string v) { int dash = v.IndexOf('-'); if (dash > 0) v = v.Substring(0, dash); return v.Contains(".") ? v : v + ".0"; }

    // Downloads the new exe and puts it in place of this one (the running exe can be renamed, not overwritten).
    // progress: 0..100. Returns the path to start. Throws on failure - the old program then stays as it was.
    public static string Download(Release r, Action<int> progress)
    {
        string exe = ExePath, tmp = exe + ".new";
        try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
        using (var wc = Client())
        {
            var done = new ManualResetEvent(false); Exception err = null;
            wc.DownloadProgressChanged += (s, e) => { if (progress != null) progress(e.ProgressPercentage); };
            wc.DownloadFileCompleted += (s, e) => { err = e.Error; done.Set(); };
            wc.DownloadFileAsync(new Uri(r.DownloadUrl), tmp);
            if (!done.WaitOne(TimeSpan.FromMinutes(5))) { wc.CancelAsync(); throw new Exception(L.T("Stahování trvá moc dlouho.", "The download takes too long.")); }
            if (err != null) throw err;
        }
        // sanity check: a real Windows program, complete
        var info = new FileInfo(tmp);
        bool ok = info.Exists && info.Length > 50000 && (r.Size <= 0 || info.Length == r.Size);
        if (ok) using (var fs = File.OpenRead(tmp)) ok = fs.ReadByte() == 'M' && fs.ReadByte() == 'Z';
        if (!ok) { try { File.Delete(tmp); } catch { } throw new Exception(L.T("Stažený soubor je poškozený.", "The downloaded file is damaged.")); }

        try { if (File.Exists(OldPath)) File.Delete(OldPath); } catch { }
        string old = File.Exists(OldPath) ? OldPath + "." + DateTime.Now.Ticks : OldPath;
        File.Move(exe, old);
        try { File.Move(tmp, exe); }
        catch { File.Move(old, exe); throw; }   // put the old one back
        return exe;
    }

    public static void Restart(string exe)
    {
        Process.Start(new ProcessStartInfo(exe, "--updated") { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe) });
    }

    // after an update: removes FishaoBot_old.exe as soon as the old program has exited
    public static void CleanupOld()
    {
        new Thread(delegate ()
        {
            for (int i = 0; i < 30; i++)
            {
                try
                {
                    string dir = Path.GetDirectoryName(ExePath);
                    foreach (var f in Directory.GetFiles(dir, "FishaoBot_old.exe*")) File.Delete(f);
                    var tmp = ExePath + ".new"; if (File.Exists(tmp)) File.Delete(tmp);
                    return;
                }
                catch { Thread.Sleep(1000); }
            }
        }) { IsBackground = true }.Start();
    }
}
