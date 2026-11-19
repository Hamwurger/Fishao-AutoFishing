// Small pictures (built into the exe) searched for in the game picture.
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
    // ---------------- picture templates (png files next to the bot) ----------------
    class Template
    {
        public int W, H; public int[] Px; public int Pr, Pg, Pb, Px0, Py0; // prefilter pixel = most distinctive one
        public int Count;  // pixels that are compared (transparent pixels of the png are ignored = "any background")
        public static Template Load(string file)
        {
            // a file next to the exe wins (easy to replace), otherwise the copy built into the exe
            string path = System.IO.Path.Combine(DebugDir, file);
            System.IO.Stream res = System.IO.File.Exists(path) ? null
                : System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(file);
            if (res == null && !System.IO.File.Exists(path)) { Log(L.T("Chybí soubor ", "Missing file ") + file + L.T(" - tahle funkce nebude fungovat.", " - this feature will not work.")); return null; }
            using (res)
            using (var b = res != null ? new Bitmap(res) : new Bitmap(path))
                return FromBitmap(b);
        }

        // from a picture in memory (transparent pixels = ignored)
        // prefilterCentre: take the quick-check pixel only from the middle of the picture (its edges may contain
        // things that are not always there, e.g. the green tick of the selected bait in the bag)
        public static Template FromBitmap(Bitmap b, bool prefilterCentre = false)
        {
            var t = new Template();
            t.W = b.Width; t.H = b.Height; t.Px = new int[t.W * t.H];
            for (int y = 0; y < t.H; y++) for (int x = 0; x < t.W; x++) t.Px[y * t.W + x] = b.GetPixel(x, y).ToArgb();
            // prefilter on the most saturated pixel (rarely matches random background)
            int best = -1;
            for (int i = 0; i < t.Px.Length; i++)
            {
                if (((t.Px[i] >> 24) & 255) < 128) continue; else t.Count++;
                if (prefilterCentre)
                {
                    int px = i % t.W, py = i / t.W;
                    if (px < t.W / 4 || px > t.W * 3 / 4 || py < t.H / 4 || py > t.H * 3 / 4) continue;
                }
                int c = t.Px[i], r = (c >> 16) & 255, g = (c >> 8) & 255, bl = c & 255;
                int sat = Math.Max(r, Math.Max(g, bl)) - Math.Min(r, Math.Min(g, bl));
                if (sat > best) { best = sat; t.Px0 = i % t.W; t.Py0 = i / t.W; t.Pr = r; t.Pg = g; t.Pb = bl; }
            }
            return t;
        }

        // mirror image (left <-> right)
        public Template Mirrored()
        {
            var m = new Template { W = W, H = H, Px = new int[Px.Length], Count = Count };
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) m.Px[y * W + (W - 1 - x)] = Px[y * W + x];
            m.Px0 = W - 1 - Px0; m.Py0 = Py0; m.Pr = Pr; m.Pg = Pg; m.Pb = Pb;
            return m;
        }
    }

    static Dictionary<string, Template> templates = new Dictionary<string, Template>();
    static Template Tpl(string file)
    {
        Template t;
        if (!templates.TryGetValue(file, out t)) { t = Template.Load(file); templates[file] = t; }
        return t;
    }

    // returns the top-left corner of the best place where the template fits, or null
    static Point? FindTemplate(Frame f, Template t, int avgTol)
    {
        if (t == null) return null;
        return FindTemplateIn(f, t, avgTol, new Rectangle(0, 0, f.W, f.H));
    }

    // same, but only where the template's top-left corner lies inside "area"
    static Point? FindTemplateIn(Frame f, Template t, int avgTol, Rectangle area)
    {
        double avg;
        return FindTemplateIn(f, t, avgTol, area, out avg);
    }

    // ... and tells how well it fits: average difference per colour channel (0 = identical)
    static Point? FindTemplateIn(Frame f, Template t, int avgTol, Rectangle area, out double avg)
    {
        avg = double.MaxValue;
        if (t == null || t.Count == 0) return null;
        int maxSad = t.Count * 3 * avgTol, bestSad = int.MaxValue; Point? best = null;
        int y0 = Math.Max(0, area.Top), y1 = Math.Min(f.H - t.H, area.Bottom - 1);
        int x0 = Math.Max(0, area.Left), x1 = Math.Min(f.W - t.W, area.Right - 1);
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int r, g, b;
                f.Get(x + t.Px0, y + t.Py0, out r, out g, out b);
                if (Math.Abs(r - t.Pr) + Math.Abs(g - t.Pg) + Math.Abs(b - t.Pb) > 45) continue; // quick prefilter
                int sad = 0;
                for (int ty = 0; ty < t.H && sad <= maxSad; ty++)
                    for (int tx = 0; tx < t.W; tx++)
                    {
                        int c = t.Px[ty * t.W + tx];
                        if (((c >> 24) & 255) < 128) continue; // transparent = ignored
                        f.Get(x + tx, y + ty, out r, out g, out b);
                        sad += Math.Abs(r - ((c >> 16) & 255)) + Math.Abs(g - ((c >> 8) & 255)) + Math.Abs(b - (c & 255));
                    }
                if (sad <= maxSad && sad < bestSad) { bestSad = sad; best = new Point(x, y); }
            }
        if (best != null) avg = bestSad / (3.0 * t.Count);
        return best;
    }

    // centre of a dialog close button (round X of result dialogs, or square orange X of other windows), or null
    public static Point? FindCloseButton(Frame f)
    {
        foreach (var file in new[] { "close_x.png", "close_x2.png" })
        {
            var t = Tpl(file);
            var p = FindTemplate(f, t, 18);
            if (p != null) return new Point(p.Value.X + t.W / 2, p.Value.Y + t.H / 2);
        }
        return null;
    }

    // Energy shop window ("buy more energy", any language): 5 rows, each with the same blue lightning icon.
    // Returns the centre of the green buy button of the last row (100 energy), or null.
    const int EnergyRowStep = 79;                         // distance of the rows in game pixels
    static readonly Point BoltToBuyButton = new Point(551, 34); // from the icon's top-left corner to its row's buy button
    public static Point? FindEnergyBuyButton(Frame f)
    {
        var t = Tpl("energy_bolt.png");
        var p = FindTemplate(f, t, 24);
        if (p == null) return null;
        // the other rows must have the same icon right above / below
        int rows = 1, lastY = p.Value.Y;
        for (int dir = -1; dir <= 1; dir += 2)
            for (int k = 1; k <= 4; k++)
            {
                var q = FindTemplateIn(f, t, 24, new Rectangle(p.Value.X - 4, p.Value.Y + dir * k * EnergyRowStep - 6, 9, 13));
                if (q == null) break;
                rows++; if (q.Value.Y > lastY) lastY = q.Value.Y;
            }
        if (rows < 4) return null; // a single bolt somewhere else (e.g. "full energy" in the level up window)
        return new Point(p.Value.X + BoltToBuyButton.X, lastY + BoltToBuyButton.Y);
    }

    // Level up window (any language): a big gold star on each side of the title, OK button below.
    // Returns the centre of the OK button, or null.
    static Template starMirrored;
    public static Point? FindLevelUpOk(Frame f)
    {
        var t = Tpl("levelup_star.png");
        var left = FindTemplate(f, t, 26);
        if (left == null) return null;
        // the twin star (a mirror image) on the same line to the right - the title is between them.
        // The distance depends on the title's length in the game's language, so allow a wide range.
        if (starMirrored == null) starMirrored = t.Mirrored();
        var right = FindTemplateIn(f, starMirrored, 30, new Rectangle(left.Value.X + 120, left.Value.Y - 8, 600, 17));
        if (right == null) return null;
        int cx = (left.Value.X + right.Value.X + t.W) / 2;
        var btn = FindDialogButton(f);
        if (btn != null && Math.Abs(btn.Value.X - cx) < 120) return btn;
        return new Point(cx, left.Value.Y + 514); // OK is ~514 px below the stars
    }

    // closes the level up window if it is open; returns true when it clicked
    static bool CloseLevelUp(Frame f)
    {
        var ok = FindLevelUpOk(f);
        if (ok == null) return false;
        if (!Wait(500)) return false; // let the window finish its animation
        ok = FindLevelUpOk(Grab());
        if (ok == null) return false;
        Click(ok.Value); dialogsClosed++; Changed();
        Log(L.T("Nová úroveň! Klikám na OK.", "Level up! Clicking OK."));
        Wait(1200);
        return true;
    }

    // average brightness outside the middle of the window; an open dialog darkens everything around it
    public static double OuterBrightness(Frame f)
    {
        long sum = 0; int n = 0, r, g, b;
        for (int y = 8; y < f.H - 8; y += 16)
            for (int x = 8; x < f.W - 8; x += 16)
            {
                if (x > f.W / 4 && x < f.W * 3 / 4 && y > f.H / 5 && y < f.H * 17 / 20) continue;
                f.Get(x, y, out r, out g, out b); sum += r + g + b; n++;
            }
        return n == 0 ? 0 : sum / (double)n;
    }
}
