// Catch statistics - read from the result window with the Windows text recognition.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;

public static partial class FishaoBot
{
    // ---------------- statistics ----------------
    static readonly object statLock = new object();
    static readonly List<CatchInfo> session = new List<CatchInfo>();  // this run of the program
    static CatchInfo record;                                           // biggest fish ever (from catches.csv)
    static int allTimeFish = 0;
    static bool statsLoaded = false;

    public static event Action<CatchInfo> Caught;
    public static string CatchesFile { get { return Path.Combine(DebugDir, "catches.csv"); } }
    public static string RecordImageFile { get { return Path.Combine(DebugDir, "record.png"); } }

    public static List<CatchInfo> SessionCatches { get { lock (statLock) return new List<CatchInfo>(session); } }
    public static CatchInfo Record { get { lock (statLock) return record; } }
    public static int AllTimeFish { get { lock (statLock) return allTimeFish; } }

    // every line of catches.csv (for the history in the window), oldest first
    public static List<CatchInfo> AllCatches()
    {
        var list = new List<CatchInfo>();
        try
        {
            if (!File.Exists(CatchesFile)) return list;
            foreach (var line in File.ReadAllLines(CatchesFile)) { var c = CatchInfo.FromCsv(line); if (c != null) list.Add(c); }
        }
        catch { }
        return list;
    }

    // older versions used Czech file names - rename them once so the history is kept
    static void RenameOldFiles()
    {
        foreach (var pair in new[] { new[] { "ulovky.csv", "catches.csv" }, new[] { "rekord.png", "record.png" } })
        {
            try
            {
                string from = Path.Combine(DebugDir, pair[0]), to = Path.Combine(DebugDir, pair[1]);
                if (File.Exists(from) && !File.Exists(to)) File.Move(from, to);
            }
            catch { }
        }
    }

    public static void LoadStats()
    {
        lock (statLock)
        {
            if (statsLoaded) return; statsLoaded = true;
            RenameOldFiles();
            try
            {
                if (!File.Exists(CatchesFile)) return;
                foreach (var line in File.ReadAllLines(CatchesFile))
                {
                    var c = CatchInfo.FromCsv(line);
                    if (c == null || c.Escaped) continue;
                    allTimeFish++;
                    if (record == null || c.Cm > record.Cm) record = c;
                }
            }
            catch { }
        }
    }

    // Reads the open result dialog - called right before closing it. Works with the Czech and the English game:
    //   "<fish> ULOVEN/A!" ... "37 BZ"      "<fish> CAUGHT!" ... "You've gained 12 exp"
    //   "RYBA SE UTRHLA" / "RYBA UNIKLA!"  (English escape texts below are a guess - not seen yet)
    const string PointsRx = @"(\d+)\s*(?:B\s*[Z2]|e\s*[xk]\s*p)";   // "37 BZ" or "12 exp"

    static void ReadCatch(Frame f)
    {
        try
        {
            if (!WinOcr.Available) return;
            var panel = FindPanel(f);
            if (panel == null || capBmp == null) return;
            var pr = panel.Value;
            string[] lines;
            using (var crop = capBmp.Clone(pr, PixelFormat.Format32bppArgb)) lines = WinOcr.Read(crop);
            string all = string.Join("\n", lines);
            var c = new CatchInfo { Time = DateTime.Now };

            string up = all.ToUpperInvariant();
            // "RYBA SE UTRHLA" (line broke) or "RYBA UNIKLA!" (fish too strong for the rod/line)
            if (up.Contains("UTRHL") || up.Contains("UNIKL") || up.Contains("BYLA OPRAVDU SILN")
                || up.Contains("ESCAPED") || up.Contains("GOT AWAY") || up.Contains("BROKE") || up.Contains("SNAPPED") || up.Contains("TOO STRONG")) c.Escaped = true;
            else
            {
                // "<fish> ULOVEN/A!" - the text recognition sometimes reads O as 0, N as M, misses letters...
                var title = new Regex(@"U\s*L\s*\S\s*V\s*E\s*[NM]|ULOV|LOVEN|\bU\S{1,3}EN\s*/\s*A|\b[CG]\s*A\s*U\s*[GC]\s*H\s*T\b", RegexOptions.IgnoreCase); // ... "CAUGHT" (OCR reads GAUGHT too)
                Match tm = null;
                // the name is read from the title alone, as black on white (the whole-window reading mixes up C/G)
                foreach (var l in ReadTitle(pr)) { var mt = title.Match(l); if (mt.Success) { tm = mt; c.Name = l.Substring(0, mt.Index).Trim(); break; } }
                if (tm == null)
                    foreach (var l in lines) { var mt = title.Match(l); if (mt.Success) { tm = mt; c.Name = l.Substring(0, mt.Index).Trim(); break; } }
                c.Stars = CountStars(f, pr);
                bool looksLikeCatch = tm != null || Regex.IsMatch(all, PointsRx, RegexOptions.IgnoreCase) || all.ToUpperInvariant().Contains("FISHDEX");
                if (!looksLikeCatch && all.ToUpperInvariant().Contains("ENERGI")) return; // energy shop
                if (!looksLikeCatch)
                {   // unknown window - keep it so the reading can be improved
                    SaveShot("unknown");
                    Log(L.T("Okno s výsledkem jsem nepřečetl (čtu: ", "Could not read the result window (reading: ") + all.Replace("\n", " | ") + ")");
                    return;
                }
                c.Name = c.Name.Trim(' ', '-', '!', ':', '.');
                // the game font's C is sometimes read as G
                c.Name = Regex.Replace(c.Name, @"\bGommon\b", "Common");
                c.Name = Regex.Replace(c.Name, @"\b([Gg])arp\b", m => m.Groups[1].Value == "G" ? "Carp" : "carp");
                if (c.Name.Length > 0) c.Name = char.ToUpper(c.Name[0]) + c.Name.Substring(1);

                // big yellow "45 cm" (lower left) and "37 BZ" / "12 exp" (lower right)
                c.Cm = ReadNumber(new Rectangle(pr.X, pr.Y + pr.Height * 84 / 100, pr.Width * 45 / 100, pr.Height * 15 / 100), @"(\d+)\s*c\s*m");
                c.Points = ReadNumber(new Rectangle(pr.X + pr.Width * 60 / 100, pr.Y + pr.Height * 84 / 100, pr.Width * 38 / 100, pr.Height * 15 / 100), @"(\d+)\s*(?:B|e\s*[xk]\s*p)"); // looser: only this small area is read
                if (c.Cm == 0) { var mc = Regex.Match(all, @"(\d+)\s*c\s*m", RegexOptions.IgnoreCase); if (mc.Success) int.TryParse(mc.Groups[1].Value, out c.Cm); }
                if (c.Points == 0) { var mb = Regex.Match(all, PointsRx, RegexOptions.IgnoreCase); if (mb.Success) int.TryParse(mb.Groups[1].Value, out c.Points); }
            }

            bool newRecord = false;
            lock (statLock)
            {
                session.Add(c);
                if (!c.Escaped)
                {
                    allTimeFish++;
                    if (c.Cm > 0 && (record == null || c.Cm > record.Cm)) { record = c; newRecord = true; }
                }
            }
            try { File.AppendAllText(CatchesFile, c.ToCsv() + Environment.NewLine); } catch { }
            if (newRecord) SaveRecordImage(pr);

            if (c.Escaped) Log(L.T("Ryba se utrhla.", "The fish got away."));
            else Log(L.T("Úlovek: ", "Catch: ") + c.Name + (c.Cm > 0 ? ", " + c.Cm + " cm" : "") + ", " + new string('*', c.Stars) + " (" + c.Stars + "/5)" + (c.Points > 0 ? ", " + c.Points + L.T(" BZ", " exp") : "") + (newRecord ? L.T("  - NOVÝ REKORD!", "  - NEW RECORD!") : ""));
            var h = Caught; if (h != null) try { h(c); } catch { }
            Changed();
        }
        catch (Exception ex) { Log(L.T("Nepodařilo se přečíst úlovek: ", "Could not read the catch: ") + ex.Message); }
    }

    // Reads a number with a unit ("12 cm") from a part of the captured picture. The game's font is read
    // much better when enlarged with a margin around it, so a few variants are tried. The yellow outlined
    // numbers are hard for the text recognition, so the last variants turn them into black text on white.
    static int ReadNumber(Rectangle rc, string withUnit)
    {
        rc.Intersect(new Rectangle(0, 0, capBmp.Width, capBmp.Height));
        if (rc.Width <= 0 || rc.Height <= 0) return 0;
        int fallback = 0;
        foreach (var v in new[] { new { K = 3f, Pad = 30, Bw = false }, new { K = 2f, Pad = 30, Bw = true }, new { K = 3f, Pad = 30, Bw = true },
                                  new { K = 2f, Pad = 30, Bw = false }, new { K = 3f, Pad = 0, Bw = false }, new { K = 1f, Pad = 0, Bw = false } })
        {
            using (var b = new Bitmap((int)(rc.Width * v.K) + 2 * v.Pad, (int)(rc.Height * v.K) + 2 * v.Pad, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(b))
                {
                    g.Clear(Color.FromArgb(250, 242, 218)); // window background colour
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(capBmp, new Rectangle(v.Pad, v.Pad, (int)(rc.Width * v.K), (int)(rc.Height * v.K)), rc, GraphicsUnit.Pixel);
                }
                if (v.Bw) YellowToBlack(b);
                foreach (var l in WinOcr.Read(b))
                {
                    int n;
                    var mu = Regex.Match(l, withUnit, RegexOptions.IgnoreCase);
                    if (mu.Success && int.TryParse(mu.Groups[1].Value, out n) && n > 0) return n;
                    var md = Regex.Match(l, @"^\s*(\d+)\s*$");
                    if (fallback == 0 && md.Success && int.TryParse(md.Groups[1].Value, out n)) fallback = n;
                }
            }
        }
        return fallback;
    }

    // text of the window title (top strip of the panel), yellow letters turned black on white
    static string[] ReadTitle(Rectangle pr)
    {
        try
        {
            var rc = new Rectangle(pr.X, pr.Y, pr.Width, Math.Max(10, pr.Height * 12 / 100));
            rc.Intersect(new Rectangle(0, 0, capBmp.Width, capBmp.Height));
            const float K = 2f; const int Pad = 30;
            using (var b = new Bitmap((int)(rc.Width * K) + 2 * Pad, (int)(rc.Height * K) + 2 * Pad, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(b))
                {
                    g.Clear(Color.FromArgb(250, 242, 218));
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(capBmp, new Rectangle(Pad, Pad, (int)(rc.Width * K), (int)(rc.Height * K)), rc, GraphicsUnit.Pixel);
                }
                YellowToBlack(b);
                return WinOcr.Read(b);
            }
        }
        catch { return new string[0]; }
    }

    // yellow/orange letter fill -> black, everything else (cream background, brown outline, buttons) -> white
    static void YellowToBlack(Bitmap b)
    {
        var d = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        var px = new byte[d.Stride * b.Height];
        Marshal.Copy(d.Scan0, px, 0, px.Length);
        for (int i = 0; i + 3 < px.Length; i += 4)
        {
            int bl = px[i], g = px[i + 1], r = px[i + 2];
            bool yellow = r > 200 && g > 140 && bl < 110 && r - bl > 120;
            byte v = yellow ? (byte)0 : (byte)255;
            px[i] = px[i + 1] = px[i + 2] = v; px[i + 3] = 255;
        }
        Marshal.Copy(px, 0, d.Scan0, px.Length);
        b.UnlockBits(d);
    }

    // gold stars = runs of columns with gold pixels in the right part of the window
    static int CountStars(Frame f, Rectangle pr)
    {
        int x0 = pr.X + pr.Width * 60 / 100, x1 = pr.Right - 5, y0 = pr.Y + pr.Height * 64 / 100, y1 = pr.Y + pr.Height * 82 / 100;
        int runs = 0, gap = 99, r, g, b;
        for (int x = x0; x < x1 && x < f.W; x++)
        {
            bool gold = false;
            for (int y = y0; y < y1 && y < f.H && !gold; y += 2)
            {
                f.Get(x, y, out r, out g, out b);
                gold = r > 235 && g > 170 && g < 235 && b < 60;
            }
            if (gold) { if (gap > 5) runs++; gap = 0; } else gap++;
        }
        return Math.Min(5, runs);
    }

    // picture of the record fish for the GUI
    static void SaveRecordImage(Rectangle pr)
    {
        try
        {
            var img = new Rectangle(pr.X + pr.Width * 2 / 100, pr.Y + pr.Height * 10 / 100, pr.Width * 96 / 100, pr.Height * 54 / 100);
            img.Intersect(new Rectangle(0, 0, capBmp.Width, capBmp.Height));
            using (var src = capBmp.Clone(img, PixelFormat.Format32bppArgb))
            using (var small = new Bitmap(320, Math.Max(1, 320 * img.Height / img.Width)))
            {
                using (var g = Graphics.FromImage(small)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(src, 0, 0, small.Width, small.Height); }
                small.Save(RecordImageFile, ImageFormat.Png);
            }
        }
        catch { }
    }

}
