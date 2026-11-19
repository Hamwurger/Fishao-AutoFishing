// Finding the minigame bar.
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
    // colours of the minigame bar: red -> yellow -> green -> yellow -> red, no blue
    // (the grass is ~ (120,180,0), so only the very bright, saturated bar colours may pass)
    static bool IsBar(int r, int g, int b) { return b < 70 && Math.Max(r, g) >= 210; }
    static bool IsRed(int r, int g, int b) { return r >= 200 && g < 130 && b < 70; }
    static bool IsGreenish(int r, int g, int b) { return g >= 210 && r < 220 && b < 70; }

    public class Bar { public int L, R, Y; public int W { get { return R - L; } } }

    // Finds the minigame bar inside the frame.
    public static Bar FindBar(Frame f)
    {
        var hits = new List<Bar>();
        for (int y = 0; y < f.H; y += 2)
        {
            int runStart = -1, lastBar = -1, r, g, b;
            for (int x = 0; x < f.W; x++)
            {
                f.Get(x, y, out r, out g, out b);
                if (IsBar(r, g, b))
                {
                    if (runStart < 0 || x - lastBar > 30) { TryRun(f, y, runStart, lastBar, hits); runStart = x; }
                    lastBar = x;
                }
            }
            TryRun(f, y, runStart, lastBar, hits);
        }
        if (hits.Count < 4) return null;
        Bar best = null; int bestN = 0;
        foreach (var h in hits)
        {
            int n = 0; foreach (var o in hits) if (Math.Abs(o.L - h.L) <= 4 && Math.Abs(o.R - h.R) <= 4) n++;
            if (n > bestN) { bestN = n; best = h; }
        }
        if (bestN < 4) return null;
        var ys = new List<int>(); var ls = new List<int>(); var rs = new List<int>();
        foreach (var o in hits) if (Math.Abs(o.L - best.L) <= 4 && Math.Abs(o.R - best.R) <= 4) { ys.Add(o.Y); ls.Add(o.L); rs.Add(o.R); }
        ys.Sort(); ls.Sort(); rs.Sort();
        return new Bar { Y = ys[ys.Count / 2], L = ls[ls.Count / 2], R = rs[rs.Count / 2] };
    }

    static void TryRun(Frame f, int y, int L, int R, List<Bar> hits)
    {
        if (L < 0 || R - L < 120) return;
        int W = R - L, r, g, b;
        f.Get(L + W * 3 / 100, y, out r, out g, out b); if (!IsRed(r, g, b)) return;
        f.Get(R - W * 3 / 100, y, out r, out g, out b); if (!IsRed(r, g, b)) return;
        int gr = 0;
        foreach (int pc in new[] { 35, 40, 45, 55, 60, 65 }) { f.Get(L + W * pc / 100, y, out r, out g, out b); if (IsGreenish(r, g, b)) gr++; }
        if (gr < 3) return;
        hits.Add(new Bar { L = L, R = R, Y = y });
    }

    // The pointer is a pair of small blue triangles on the top and bottom edge of the bar.
    // Returns the average x of blue pixels in the rows just below the bar's top edge (-1 if none).
    // barFraction = share of bar-coloured pixels on the middle row (tells whether the bar is still shown).
    public static double FindPointer(Frame f, int top, int mid, int L, int R, out double barFraction)
    {
        int r, g, b, barPx = 0; long sum = 0; int n = 0;
        for (int x = L; x <= R; x++) { f.Get(x, mid, out r, out g, out b); if (IsBar(r, g, b)) barPx++; }
        barFraction = barPx / (double)Math.Max(1, R - L + 1);
        for (int y = top + 1; y <= top + 7 && y < f.H; y++)
            for (int x = L; x <= R; x++)
            {
                f.Get(x, y, out r, out g, out b);
                if (b >= 120 && r < 90 && b > g) { sum += x; n++; }
            }
        return n >= 3 ? sum / (double)n : -1;
    }

    // walks up from the middle row to the first row of the bar (measured in the orange part at 10 %)
    public static int FindTop(Frame f, Bar bar)
    {
        int x = bar.L + bar.W / 10, y = bar.Y, r, g, b;
        while (y > 0) { f.Get(x, y - 1, out r, out g, out b); if (!IsBar(r, g, b)) break; y--; }
        return y;
    }
}
