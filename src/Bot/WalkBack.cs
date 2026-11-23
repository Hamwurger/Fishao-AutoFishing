// Walking back: sometimes a click on the water makes the character walk instead of casting. The game camera
// follows the character, so the whole view scrolls and every cast point now points somewhere else.
// The bot remembers the view from the last good cast ("home"), finds how far the view moved and clicks on the
// spot where the character stood - the character walks back and the view lines up with "home" again.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;

public static partial class FishaoBot
{
    // small grey copies of the view at the last good cast (1/4 and 1/16 of the size)
    static Gray homeQ, homeS;
    static Point? charScreen;             // where the camera keeps the character on screen (learned while walking back)
    static readonly List<long> walkAways = new List<long>();

    class Gray { public int W, H; public byte[] P; }

    static Gray Shrink(Frame f, int k)
    {
        var g = new Gray { W = f.W / k, H = f.H / k };
        g.P = new byte[g.W * g.H];
        for (int y = 0; y < g.H; y++)
            for (int x = 0; x < g.W; x++)
            {
                int i = (y * k + k / 2) * f.Stride + (x * k + k / 2) * 4;
                g.P[y * g.W + x] = (byte)((f.Px[i] + f.Px[i + 1] * 2 + f.Px[i + 2]) >> 2);
            }
        return g;
    }

    static Gray Shrink(Gray q, int k)
    {
        var g = new Gray { W = q.W / k, H = q.H / k };
        g.P = new byte[g.W * g.H];
        for (int y = 0; y < g.H; y++)
            for (int x = 0; x < g.W; x++)
            {
                int s = 0;
                for (int yy = 0; yy < k; yy++) for (int xx = 0; xx < k; xx++) s += q.P[(y * k + yy) * q.W + x * k + xx];
                g.P[y * g.W + x] = (byte)(s / (k * k));
            }
        return g;
    }

    // called before every cast (the view then is right - otherwise the bot would have stopped): this is where the character belongs
    static void RememberHome(Frame before)
    {
        homeQ = Shrink(before, 4);
        homeS = Shrink(homeQ, 4);
    }

    // mean difference when "now" is "home" moved by (dx, dy); only the middle of the view (the game's
    // buttons and bars at the edges never move)
    static double Score(Gray home, Gray now, int dx, int dy, int step)
    {
        int x0 = (int)(now.W * 0.06), x1 = (int)(now.W * 0.90), y0 = (int)(now.H * 0.10), y1 = (int)(now.H * 0.86);
        long sum = 0; int n = 0;
        for (int y = y0; y < y1; y += step)
        {
            int hy = y - dy; if (hy < 0 || hy >= home.H) continue;
            for (int x = x0; x < x1; x += step)
            {
                int hx = x - dx; if (hx < 0 || hx >= home.W) continue;
                sum += Math.Abs(now.P[y * now.W + x] - home.P[hy * home.W + hx]); n++;
            }
        }
        return n < 200 ? double.MaxValue : (double)sum / n;
    }

    // how far the view moved since "home" (in full pixels): a spot at P in "home" is now at P + shift.
    // null when it can't be told (no home, the picture changed too much)
    static Point? ViewShift(Frame now)
    {
        if (homeQ == null || now.W / 4 != homeQ.W || now.H / 4 != homeQ.H) return null;
        var q = Shrink(now, 4); var s = Shrink(q, 4);
        // rough: 1/16 size, up to ~1/3 of the window in each direction
        int rx = s.W / 3, ry = s.H / 3, bx = 0, by = 0; double best = double.MaxValue;
        for (int dy = -ry; dy <= ry; dy++)
            for (int dx = -rx; dx <= rx; dx++)
            {
                double v = Score(homeS, s, dx, dy, 1);
                if (v < best) { best = v; bx = dx; by = dy; }
            }
        // fine: 1/4 size around it
        int fx = bx * 4, fy = by * 4; best = double.MaxValue;
        for (int dy = by * 4 - 5; dy <= by * 4 + 5; dy++)
            for (int dx = bx * 4 - 5; dx <= bx * 4 + 5; dx++)
            {
                double v = Score(homeQ, q, dx, dy, 2);
                if (v < best) { best = v; fx = dx; fy = dy; }
            }
        double still = Score(homeQ, q, 0, 0, 2);
        if (best > 40) return null;                                // not the same place at all
        if (fx == 0 && fy == 0) return Point.Empty;
        if (best > still * 0.8) return Point.Empty;                // moving does not fit better than not moving
        return new Point(fx * 4, fy * 4);
    }

    static double Len(Point p) { return Math.Sqrt(p.X * (double)p.X + p.Y * (double)p.Y); }

    // waits until the view stops moving (the character arrived), max. ms
    static Frame WaitStill(int ms)
    {
        var sw = Stopwatch.StartNew();
        Frame prev = bufA = Grab(bufA); int calm = 0;
        while (sw.ElapsedMilliseconds < ms)
        {
            if (!Wait(300)) return null;
            var f = bufB = Grab(bufB);
            calm = ShiftedPart(prev, f) < 0.01 ? calm + 1 : 0;
            var t = bufA; bufA = bufB; bufB = t; prev = bufA;
            if (calm >= 2) break;
        }
        return prev;
    }

    // true = the character is back and the cast points fit again
    static bool WalkBack()
    {
        if (homeQ == null) return false;
        walkAways.Add(clock.ElapsedMilliseconds);
        walkAways.RemoveAll(t => clock.ElapsedMilliseconds - t > 300000);
        if (walkAways.Count >= 4)
        {
            Log(L.T("Postava odešla už 4× za 5 minut - místa nahození asi nejsou dobrá.", "The character walked away 4 times in 5 minutes - the cast spots are probably not good."));
            return false;
        }
        SetStatus(L.T("Vracím postavu na místo", "Walking the character back"));
        var now = WaitStill(8000); if (now == null) return false;
        if (IsDimmed(now)) { ClearDialogs(true); now = WaitStill(3000); if (now == null) return false; }
        var shift = ViewShift(now);
        if (shift == null) { Log(L.T("Nepoznal jsem, kam se obraz hry posunul.", "Could not tell where the game view moved.")); return false; }
        Log(L.T("Postava odešla (obraz se posunul o ", "The character walked away (the view moved by ") + shift.Value.X + ", " + shift.Value.Y + L.T(" px) - vracím ji zpátky.", " px) - walking it back."));
        Point me = charScreen ?? new Point(now.W / 2, now.H * 45 / 100);
        int nudge = 0;
        for (int tryNo = 0; tryNo < 4; tryNo++)
        {
            var d = shift.Value;
            if (Len(d) <= 16) { Log(L.T("Postava je zpátky na místě.", "The character is back in place.")); return true; }
            // the spot where the character stood is now at (character on screen) + shift
            var target = new Point(me.X + d.X, me.Y + d.Y + nudge);
            if (target.X < 20 || target.Y < 20 || target.X > now.W - 20 || target.Y > now.H - 20)
            { Log(L.T("Původní místo je mimo obraz.", "The old spot is outside the view.")); return false; }
            Click(target);
            now = WaitStill(10000); if (now == null) return false;
            if (IsDimmed(now)) { ClearDialogs(true); now = WaitStill(3000); if (now == null) return false; }
            var after = ViewShift(now);
            if (after == null) { Log(L.T("Po návratu jsem nepoznal místo.", "Could not recognise the place after walking.")); return false; }
            var moved = new Point(d.X - after.Value.X, d.Y - after.Value.Y);   // how far the character walked
            if (Len(moved) < 6)
            {
                // nothing happened (a player standing there, a popup...) - try a bit next to it
                nudge = nudge == 0 ? 30 : -nudge;
            }
            else
            {
                // the character walked by (click - its screen spot) - from that the spot on screen is known exactly
                var learned = new Point(target.X - moved.X, target.Y - moved.Y);
                if (learned.X > now.W / 5 && learned.X < now.W * 4 / 5 && learned.Y > now.H / 5 && learned.Y < now.H * 4 / 5)
                { me = learned; charScreen = learned; }
            }
            shift = after;
        }
        if (Len(shift.Value) <= 16) { Log(L.T("Postava je zpátky na místě.", "The character is back in place.")); return true; }
        return false;
    }
}
