// Watching the float after the cast - recognises when nothing bit and the line came back.
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
    // ---------------- cast mask ----------------
    public static int MinMaskPx = 25;

    static int Diff(Frame f, Frame g, int i)
    { return Math.Abs(f.Px[i] - g.Px[i]) + Math.Abs(f.Px[i + 1] - g.Px[i + 1]) + Math.Abs(f.Px[i + 2] - g.Px[i + 2]); }

    // byte offsets of pixels that changed between "before" and "a" and then stayed the same between "a" and "b"
    // (line, float, rod in hand). Walking players change between a and b, so they are left out.
    static int[] CastMask(Frame before, Frame a, Frame b)
    {
        var list = new List<int>();
        if (before.W != a.W || before.H != a.H || a.W != b.W || a.H != b.H) return list.ToArray();
        for (int y = 0; y < a.H; y++)
            for (int x = 0; x < a.W; x++)
            {
                int i = y * a.Stride + x * 4;
                if (Diff(before, a, i) > 60 && Diff(a, b, i) < 25 && !WaterAt(a, i))
                {
                    list.Add(i);
                    if (list.Count > 40000) return new int[0]; // whole view changed (scrolling/dialog) - unusable
                }
            }
        return list.ToArray();
    }

    // share of mask pixels that look like before the cast again
    static double Reverted(int[] mask, Frame before, Frame now)
    {
        if (before.W != now.W || before.H != now.H) return 0;
        int n = 0;
        foreach (int i in mask) if (Diff(before, now, i) < 40) n++;
        return n / (double)mask.Length;
    }

    // ---------------- float watch ----------------
    public static int PatchRadius = 30;      // watched square around the cast point = 61x61 px
    public static int FloatPresentPx = 12;   // at least this many changed pixels = float/line is in the water
    public static int FloatGonePx = 3;       // at most this many = water is empty again

    // ---------------- float by colour ----------------
    // The float lands exactly where the bot clicked. Its colours (e.g. red/white) are learned right after
    // the cast; then it is enough to look for those colours around the cast point. Moving water (waves,
    // ripples) has different colours, so it does not disturb this.
    public static int FloatRadius = 22;
    public class FloatInfo { public Point C; public List<int> Colors = new List<int>(); }

    static int Sat(int r, int g, int b) { return Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b)); }

    // Colours of water itself: any blue (deep, light, turquoise) and whitish-blue foam. On a sea with moving waves
    // these pixels change all the time, so they must never be taken for the float, the line or the rod.
    static bool WaterColor(int r, int g, int b)
    {
        if (b > r + 40 && b + 20 > g) return true;                       // blue / turquoise water
        if (b >= r && b >= g - 10 && r > 150 && b - r < 70) return true;   // light foam on the waves (white-blue)
        return false;
    }
    static bool WaterAt(Frame f, int i) { return WaterColor(f.Px[i + 2], f.Px[i + 1], f.Px[i]); }

    // Finds what the cast put on the screen near the click: clusters of new, colourful pixels that also stayed
    // in place between "a" and "b" (walking players move, so they are left out) - the float, the rod tip over
    // the water, sometimes the character's casting pose. Returns up to "max" of them, best first.
    // The float and the rod tip disappear when the game reels the line in; watching several of them means
    // it doesn't matter which one is which.
    public static int FloatSearchRadius = 90;
    public static List<Point> FindNewMarks(Frame before, Frame a, Frame b, Point click, int max)
    {
        var found = new List<Point>();
        if (before.W != a.W || before.H != a.H || a.W != b.W || a.H != b.H) return found;
        const int cell = 8;
        int rr = FloatSearchRadius, n = rr * 2 / cell + 1;
        var cnt = new int[n, n];
        int x0 = click.X - rr, y0 = click.Y - rr;
        for (int y = Math.Max(0, y0); y <= Math.Min(a.H - 1, click.Y + rr); y++)
            for (int x = Math.Max(0, x0); x <= Math.Min(a.W - 1, click.X + rr); x++)
                if (IsNewFloatPixel(before, a, b, x, y)) cnt[(x - x0) / cell, (y - y0) / cell]++;
        // 3x3 cell windows (24 px - about the size of a float); closer to the click scores a bit more
        var cand = new List<double[]>();   // score, i, j
        for (int i = 1; i < n - 1; i++)
            for (int j = 1; j < n - 1; j++)
            {
                int s = 0;
                for (int di = -1; di <= 1; di++) for (int dj = -1; dj <= 1; dj++) s += cnt[i + di, j + dj];
                if (s < 8) continue;
                // the float lies on water: before the cast this spot was water, not grass
                if (!WasWater(before, x0 + (i - 1) * cell, y0 + (j - 1) * cell, cell * 3, click)) continue;
                double dx = x0 + i * cell + cell / 2 - click.X, dy = y0 + j * cell + cell / 2 - click.Y;
                cand.Add(new double[] { s * (1.0 - Math.Sqrt(dx * dx + dy * dy) / (rr * 2.0)), i, j });
            }
        cand.Sort((p, q) => q[0].CompareTo(p[0]));
        var used = new List<int[]>();
        foreach (var c in cand)
        {
            int bi = (int)c[1], bj = (int)c[2];
            bool near = false;
            foreach (var u in used) if (Math.Abs(u[0] - bi) <= 3 && Math.Abs(u[1] - bj) <= 3) { near = true; break; }
            if (near) continue;
            used.Add(new[] { bi, bj });
            // centre of the new pixels in that window
            long sx = 0, sy = 0, k = 0;
            for (int y = y0 + (bj - 1) * cell; y < y0 + (bj + 2) * cell; y++)
                for (int x = x0 + (bi - 1) * cell; x < x0 + (bi + 2) * cell; x++)
                    if (x >= 0 && y >= 0 && x < a.W && y < a.H && IsNewFloatPixel(before, a, b, x, y)) { sx += x; sy += y; k++; }
            if (k > 0) found.Add(new Point((int)(sx / k), (int)(sy / k)));
            if (found.Count >= max) break;
        }
        return found;
    }

    // watched marks (float, rod tip...) with their own "seen" / "gone since" state
    public class Mark { public FloatInfo Info; public int Min; public bool Seen; public long GoneAt = -1; }

    public static List<Mark> LearnMarks(Frame before, Frame a, Frame b, Point click)
    {
        var list = new List<Mark>();
        foreach (var p in FindNewMarks(before, a, b, click, 3))
        {
            var fi = LearnFloatAt(before, a, p);
            if (fi == null) continue;
            list.Add(new Mark { Info = fi, Min = Math.Max(3, FloatPixels(fi, before, a) / 5) });
        }
        return list;
    }

    // true when any watched mark was there and has now been gone for goneMs (the line was reeled in)
    public static bool AnyMarkGone(List<Mark> marks, Frame before, Frame now, long t, long goneMs)
    {
        foreach (var m in marks)
        {
            int n = FloatPixels(m.Info, before, now);
            if (n >= m.Min) { m.Seen = true; m.GoneAt = -1; }
            else if (m.Seen)
            {
                if (m.GoneAt < 0) m.GoneAt = t;
                else if (t - m.GoneAt >= goneMs) return true;
            }
        }
        return false;
    }

    // did most of this square look like the water at the click (before the cast)?
    static bool WasWater(Frame before, int x, int y, int size, Point click)
    {
        // water colour = the most common colour of a small patch around the click point
        int wr = 0, wg = 0, wb = 0, wn = 0;
        for (int yy = click.Y - 4; yy <= click.Y + 4; yy++)
            for (int xx = click.X - 4; xx <= click.X + 4; xx++)
                if (xx >= 0 && yy >= 0 && xx < before.W && yy < before.H) { int r, g, b; before.Get(xx, yy, out r, out g, out b); wr += r; wg += g; wb += b; wn++; }
        if (wn == 0) return true;
        wr /= wn; wg /= wn; wb /= wn;
        int water = 0, all = 0;
        for (int yy = y; yy < y + size; yy += 2)
            for (int xx = x; xx < x + size; xx += 2)
            {
                if (xx < 0 || yy < 0 || xx >= before.W || yy >= before.H) continue;
                int r, g, b; before.Get(xx, yy, out r, out g, out b); all++;
                bool blue = b > r + 40 && b + 20 > g;                                       // any blue water
                bool same = Math.Abs(r - wr) + Math.Abs(g - wg) + Math.Abs(b - wb) < 110;  // like the water at the click
                if (blue || same) water++;
            }
        return all > 0 && water * 10 >= all * 6;   // at least 60 %
    }

    static bool IsNewFloatPixel(Frame before, Frame a, Frame b, int x, int y)
    {
        int i = y * a.Stride + x * 4, r, g, bl;
        if (Diff(before, a, i) < 90 || Diff(a, b, i) > 60) return false;
        a.Get(x, y, out r, out g, out bl);
        return Sat(r, g, bl) >= 70 && !WaterColor(r, g, bl);
    }

    // colours of the float at its real position c (found by FindNewMarks). Only pixels that are new since
    // the cast count, so a red shirt or other players' floats nearby don't matter.
    public static FloatInfo LearnFloatAt(Frame before, Frame a, Point c)
    {
        if (before.W != a.W || before.H != a.H) return null;
        var fi = new FloatInfo { C = c };
        int rr = FloatRadius;
        for (int y = Math.Max(0, c.Y - rr); y <= Math.Min(a.H - 1, c.Y + rr); y++)
            for (int x = Math.Max(0, c.X - rr); x <= Math.Min(a.W - 1, c.X + rr); x++)
            {
                int i = y * a.Stride + x * 4, r, g, b;
                a.Get(x, y, out r, out g, out b);
                if (Diff(before, a, i) < 90 || Sat(r, g, b) < 70 || WaterColor(r, g, b)) continue;
                if (fi.Colors.Count < 40) fi.Colors.Add((r << 16) | (g << 8) | b);
            }
        return fi.Colors.Count >= 3 ? fi : null;
    }

    public static FloatInfo LearnFloat(Frame before, Frame a, Point c)
    {
        if (before.W != a.W || before.H != a.H) return null;
        var fi = new FloatInfo { C = c };
        int rr = FloatRadius;
        for (int y = Math.Max(0, c.Y - rr); y <= Math.Min(a.H - 1, c.Y + rr); y++)
            for (int x = Math.Max(0, c.X - rr); x <= Math.Min(a.W - 1, c.X + rr); x++)
            {
                int i = y * a.Stride + x * 4, r, g, b;
                a.Get(x, y, out r, out g, out b);
                if (Diff(before, a, i) < 90 || Sat(r, g, b) < 70) continue;   // must be new and colourful
                if (BackgroundHas(before, c, r, g, b)) continue;               // colour that the water also has
                if (fi.Colors.Count < 40) fi.Colors.Add((r << 16) | (g << 8) | b);
            }
        return fi.Colors.Count >= 3 ? fi : null;
    }

    // does a colour like this already exist in the wider area around the cast point before the cast?
    static bool BackgroundHas(Frame before, Point c, int r, int g, int b)
    {
        int rr = FloatRadius * 3, n = 0;
        for (int y = Math.Max(0, c.Y - rr); y <= Math.Min(before.H - 1, c.Y + rr); y += 2)
            for (int x = Math.Max(0, c.X - rr); x <= Math.Min(before.W - 1, c.X + rr); x += 2)
            {
                int r2, g2, b2; before.Get(x, y, out r2, out g2, out b2);
                if (Math.Abs(r - r2) + Math.Abs(g - g2) + Math.Abs(b - b2) < 60 && ++n >= 3) return true;
            }
        return false;
    }

    // number of float-coloured pixels around the cast point right now
    public static int FloatPixels(FloatInfo fi, Frame before, Frame now)
    {
        if (fi == null || now.W != before.W || now.H != before.H) return -1;
        int rr = FloatRadius, n = 0; var c = fi.C;
        for (int y = Math.Max(0, c.Y - rr); y <= Math.Min(now.H - 1, c.Y + rr); y++)
            for (int x = Math.Max(0, c.X - rr); x <= Math.Min(now.W - 1, c.X + rr); x++)
            {
                int r, g, b; now.Get(x, y, out r, out g, out b);
                if (Sat(r, g, b) < 60 || WaterColor(r, g, b)) continue;   // waves never count
                if (Diff(before, now, y * now.Stride + x * 4) < 50) continue;   // looks like before the cast = not the float
                foreach (int col in fi.Colors)
                    if (Math.Abs(r - ((col >> 16) & 255)) + Math.Abs(g - ((col >> 8) & 255)) + Math.Abs(b - (col & 255)) < 70) { n++; break; }
            }
        return n;
    }

    static int[] Patch(Frame f, Point c)
    {
        int s = PatchRadius * 2 + 1; var px = new int[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                int fx = c.X - PatchRadius + x, fy = c.Y - PatchRadius + y;
                if (fx < 0 || fy < 0 || fx >= f.W || fy >= f.H) continue;
                int r, g, b; f.Get(fx, fy, out r, out g, out b);
                px[y * s + x] = (r << 16) | (g << 8) | b;
            }
        return px;
    }

    static int PatchDiff(int[] a, int[] b)
    {
        int n = 0;
        for (int i = 0; i < a.Length; i++)
        {
            int x = a[i], y = b[i];
            int d = Math.Abs(((x >> 16) & 255) - ((y >> 16) & 255)) + Math.Abs(((x >> 8) & 255) - ((y >> 8) & 255)) + Math.Abs((x & 255) - (y & 255));
            if (d > 60) n++;
        }
        return n;
    }
}
