// Game windows: level up, catch result, other dialogs - recognising and closing them.
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
    // ---------------- other dialogs (level up, quests...) ----------------
    static double normalBright = 0;   // outer brightness without a dialog (learned from successful casts)
    static bool lastWasGame = false;  // the last thing the bot did was a minigame (level up etc. can follow)
    static int shotsSaved = 0;
    static long lastDialogLog = -999999;

    // an open dialog darkens everything around it (~300 -> ~170)
    static bool IsDimmed(Frame f)
    {
        double b = OuterBrightness(f);
        // relative to the learned normal brightness (a grassy park ~300, a sunny beach much more);
        // ClearDialogs re-learns it when no window panel is actually visible
        if (normalBright <= 0) return b < 250;
        return b < normalBright * 0.8;
    }

    // Closes windows until the game is not darkened any more: X button -> button in the middle of the
    // window's bottom part (e.g. "OK" after a level up; only when allowButton) -> Esc.
    // Returns false if the bot was stopped.
    static bool ClearDialogs(bool allowButton)
    {
        bool escTried = false;
        for (int i = 0; i < 5; i++)
        {
            var f = Grab();
            if (!IsDimmed(f)) { DialogsOk(); return true; }
            var lu = FindLevelUpOk(f);
            if (lu != null) { Click(lu.Value); dialogsClosed++; Changed(); Log(L.T("Nová úroveň! Klikám na OK.", "Level up! Clicking OK.")); if (!Wait(1200)) return false; continue; }
            var x = FindCloseButton(f);
            if (x != null) { Click(x.Value); dialogsClosed++; Changed(); Log(L.T("Zavírám okno křížkem.", "Closing the window with its X.")); }
            else if (FindPanel(f) == null)
            {
                // darker, but no game window on screen (e.g. evening / different place) - not a dialog
                normalBright = OuterBrightness(f);
                return true;
            }
            else
            {
                Point? btn = allowButton ? FindDialogButton(f) : null;
                SaveShot("window");
                if (btn != null)
                {
                    Click(btn.Value); dialogsClosed++; Changed();
                    Log(L.T("Okno bez křížku (např. nová úroveň) - klikám na tlačítko v ", "Window without an X (e.g. level up) - clicking the button at ") + btn.Value.X + "," + btn.Value.Y + ".");
                }
                else if (!escTried)
                {
                    escTried = true;
                    Log(L.T("Okno nemá známý křížek ani tlačítko - zkouším klávesu Esc.", "The window has no known X or button - trying the Esc key."));
                    PostKey(0x1B, true, false); Thread.Sleep(50); PostKey(0x1B, false, false);
                }
                else break;
            }
            if (!Wait(1200)) return false;
        }
        Log(L.T("Nepodařilo se zavřít okno ve hře - zavři ho prosím ručně.", "Could not close the window in the game - please close it by hand."));
        DialogNotClosed();   // watchdog: several in a row stop the bot and notify
        return running;
    }

    // light cream background of the game's windows (~ 245,238,215)
    static bool IsCream(int r, int g, int b) { return r > 225 && g > 205 && b > 150 && b < 240 && r >= g && g >= b && r - b > 12 && r - b < 90; }

    // green ("JÍT NA FISHDEX", "Koupit") or orange game buttons
    static bool IsButtonColour(int r, int g, int b)
    {
        bool green = g > 120 && g > r + 30 && g > b + 60;
        bool orange = r > 190 && g > 80 && g < 175 && b < 80;
        return green || orange;
    }

    // Finds a button centred horizontally in the lower part of the open window (level up "OK").
    // Returns its centre or null.
    public static string DlgDebug = "";
    public static Point? FindDialogButton(Frame f)
    {
        var panel = FindPanel(f);
        if (panel == null) return null;
        int top = panel.Value.Top, bottom = panel.Value.Bottom, left = panel.Value.Left, right = panel.Value.Right;
        int cx = (left + right) / 2, pw = right - left;
        DlgDebug = "panel " + left + "," + top + "-" + right + "," + bottom;
        return FindButtonBelow(f, top, bottom, cx, pw);
    }

    // bounding box of the cream window panel (rows with lots of cream pixels) in the middle area, or null
    public static Rectangle? FindPanel(Frame f)
    {
        int r, g, b;
        int x0 = f.W / 8, x1 = f.W * 7 / 8, y0 = f.H / 12, y1 = f.H * 11 / 12;
        int top = -1, bottom = -1, left = int.MaxValue, right = -1;
        for (int y = y0; y < y1; y += 2)
        {
            int n = 0, first = -1, last = -1;
            for (int x = x0; x < x1; x += 2)
            {
                f.Get(x, y, out r, out g, out b);
                if (IsCream(r, g, b)) { n++; if (first < 0) first = x; last = x; }
            }
            if (n * 2 >= 120) // at least ~120 px of cream on this row
            {
                if (top < 0) top = y; bottom = y;
                left = Math.Min(left, first); right = Math.Max(right, last);
            }
        }
        if (top < 0 || bottom - top < 120 || right - left < 200) return null;
        return Rectangle.FromLTRB(left, top, right, bottom);
    }

    static Point? FindButtonBelow(Frame f, int top, int bottom, int cx, int pw)
    {
        int r, g, b;

        // 2) rows in the lower half (the button may stick out a bit below the panel) with a wide,
        //    solid run of saturated colour around the panel centre
        int sy0 = top + (bottom - top) / 2, sy1 = Math.Min(f.H - 1, bottom + 50);
        int bandStart = -1, bestStart = -1, bestEnd = -1; long sumX = 0, cnt = 0, bestSumX = 0, bestCnt = 0;
        for (int y = sy0; y <= sy1 + 1; y++)
        {
            bool ok = false; int runL = -1, runR = -1;
            if (y <= sy1)
            {
                // widest run of saturated pixels (gaps <= 4 px for the label letters) that contains cx area
                int L = -1, last = -1;
                for (int x = Math.Max(0, cx - pw / 3); x <= Math.Min(f.W - 1, cx + pw / 3); x++)
                {
                    f.Get(x, y, out r, out g, out b);
                    bool btn = IsButtonColour(r, g, b);
                    bool white = Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b)) < 30 && r > 60; // label (white text, grey shadow)
                    if (btn || (white && L >= 0))
                    {
                        if (L < 0 || x - last > 4) L = x;
                        last = x;
                        if (last - L > runR - runL) { runL = L; runR = last; }
                    }
                }
                int w = runR - runL;
                ok = runL >= 0 && w >= 60 && w <= pw * 0.6 && Math.Abs((runL + runR) / 2 - cx) <= pw * 0.08;
                if (ok)
                {   // solid: most of the run is button colour (not text or a picture)
                    int n = 0;
                    int wh = 0;
                    for (int x = runL; x <= runR; x++) { f.Get(x, y, out r, out g, out b); if (IsButtonColour(r, g, b)) n++; else if (Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b)) < 30) wh++; } // white/grey label + its shadow
                    ok = n >= (w + 1) * 0.3 && n + wh >= (w + 1) * 0.8; // button colour + white label
                }
            }
            if (ok) { if (bandStart < 0) { bandStart = y; sumX = 0; cnt = 0; } sumX += (runL + runR) / 2; cnt++; }
            else if (bandStart >= 0)
            {
                if (y - bandStart >= 16 && y - bandStart <= 90) { bestStart = bandStart; bestEnd = y - 1; bestSumX = sumX; bestCnt = cnt; } // keep the lowest band
                bandStart = -1;
            }
        }
        if (bestStart < 0) return null;
        return new Point((int)(bestSumX / bestCnt), (bestStart + bestEnd) / 2);
    }

    // Debugging only: saves the last captured picture (max. 10 per run) as unknown_*.png / window_*.png,
    // so a window the bot doesn't understand can be checked later. Off for normal use.
    public static bool SaveDebugShots = false;

    static void SaveShot(string prefix)
    {
        if (!SaveDebugShots || shotsSaved >= 10 || capBmp == null) return;
        try
        {
            string path = System.IO.Path.Combine(DebugDir, prefix + "_" + DateTime.Now.ToString("MMdd_HHmmss") + ".png");
            capBmp.Save(path, ImageFormat.Png); shotsSaved++;
        }
        catch { }
    }
}
