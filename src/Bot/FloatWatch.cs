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
                if (Diff(before, a, i) > 60 && Diff(a, b, i) < 25)
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
                if (Sat(r, g, b) < 60) continue;
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
