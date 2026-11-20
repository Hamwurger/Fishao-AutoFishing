// The main cycle: cast, wait for a bite, play the minigame, close the result.
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
    // ---------------- main cycle ----------------
    static void Cycle()
    {
        // picture before the cast: later, when the line/float/rod pose are gone again, the line was reeled in
        castPoint = NextCastPoint();
        if (castPoint == null) { Log(L.T("Není vybrané žádné místo nahození.", "No cast point selected.")); Stop(); return; }
        Frame before = bufBefore = Grab(bufBefore);
        if (CloseLevelUp(before)) return;
        if (IsDimmed(before))
        {
            // some window (level up, quest...) still covers the game - get rid of it first
            bool quiet = clock.ElapsedMilliseconds - lastDialogLog < 60000;
            if (!quiet) Log(L.T("Ve hře je otevřené okno - zavírám ho před nahozením.", "A window is open in the game - closing it before casting."));
            lastDialogLog = clock.ElapsedMilliseconds;
            ClearDialogs(lastWasGame && !quiet);
            if (IsDimmed(Grab())) { SetStatus(L.T("Ve hře je otevřené okno - zavři ho", "A window is open in the game - close it")); Wait(10000); }
            else Wait(500);
            return;
        }
        if (BaitEmpty(before)) { HandleNoBait(); return; }
        int[] emptyWater = Patch(before, castPoint.Value);

        SetStatus(L.T("Nahazuji", "Casting"));
        Log(L.T("Nahazuji (míření ", "Casting (aiming ") + CastHoverMs + L.T(" ms, záběr čekám max. ", " ms, waiting for a bite max. ") + BiteTimeoutMs / 1000 + " s)...");
        HoverThenClick(castPoint.Value, CastHoverMs);
        if (!Wait(2500)) return; // throw animation
        var sw = Stopwatch.StartNew();

        // "cast mask": pixels that changed because of the cast (line, float, rod) and are not moving
        Frame a = bufA = Grab(bufA);
        var buy = FindEnergyBuyButton(a);
        if (buy != null) { HandleNoEnergy(buy.Value, before); return; }
        if (!Wait(400)) return; Frame b = bufB = Grab(bufB);
        int[] mask = CastMask(before, a, b);
        if (mask.Length < MinMaskPx) Log(L.T("Nahození jsem na obrazovce nepoznal - neodešla postava místo nahazování?", "Could not see the cast on screen - did the character walk away instead of casting?"));
        else LearnBrightness(OuterBrightness(before)); // the cast worked = no dialog was open = normal brightness
        lastWasGame = false;
        var fl = LearnFloat(before, a, castPoint.Value);   // colours of the float at the cast point
        int flMin = fl == null ? 0 : Math.Max(3, FloatPixels(fl, before, a) / 5);
        long floatGoneAt = -1; bool flSeen = false;
        SetStatus(L.T("Čekám na záběr", "Waiting for a bite"));

        Bar bar = null; int top = 0;
        bool floatSeen = false; int emptyCount = 0, revertCount = 0; long lastCheck = 0;
        while (true)
        {
            if (!Wait(80)) return;
            if (sw.ElapsedMilliseconds > BiteTimeoutMs) { Log(L.T("Žádný záběr za ", "No bite in ") + BiteTimeoutMs / 1000 + L.T(" s, klikám znovu.", " s, clicking again.")); return; }
            var f = Grab();
            bar = FindBar(f);
            if (bar != null) { top = FindTop(f, bar); break; }

            if (sw.ElapsedMilliseconds - lastCheck < 250) continue;
            lastCheck = sw.ElapsedMilliseconds;

            // 0) main signal: the float is gone from the cast point. On a bite the float also dips, but then
            //    the minigame appears 1.2 s later, so "gone for 1.7 s" means the game reeled the line in.
            if (fl != null)
            {
                int n = FloatPixels(fl, before, f);
                if (n >= flMin) { flSeen = true; floatGoneAt = -1; }
                else if (flSeen)
                {
                    if (floatGoneAt < 0) floatGoneAt = sw.ElapsedMilliseconds;
                    else if (sw.ElapsedMilliseconds - floatGoneAt >= 1700)
                    { noFish++; Log(L.T("Nezabrala - splávek zmizel, nahazuji znovu (po ", "No bite - the float is gone, casting again (after ") + (sw.ElapsedMilliseconds / 1000.0 + 2.9).ToString("0.0") + " s)."); Changed(); Wait(300); return; }
                }
            }

            // the energy shop may also pop up a moment later
            if (mask.Length < MinMaskPx && sw.ElapsedMilliseconds < 8000)
            {
                var eb = FindEnergyBuyButton(f);
                if (eb != null) { HandleNoEnergy(eb.Value, before); return; }
            }

            // 1) cast mask back to the pre-cast look = line reeled in
            if (mask.Length >= MinMaskPx)
            {
                double rev = Reverted(mask, before, f);
                revertCount = rev >= 0.75 ? revertCount + 1 : 0;
                if (revertCount >= 5) // >= 1.5 s: a bite also hides the float, but the minigame appears 1.2 s after it
                { noFish++; Log(L.T("Nezabrala - hra stáhla prut po ", "No bite - the game reeled in after ") + (sw.ElapsedMilliseconds / 1000.0 + 2.9).ToString("0.0") + " s."); Changed(); Wait(300); return; }
            }
            // 2) backup (only when the float colours could not be learned): the water square is empty again
            if (fl != null) continue;
            int changed = PatchDiff(emptyWater, Patch(f, castPoint.Value));
            if (changed >= FloatPresentPx) { floatSeen = true; emptyCount = 0; }
            else if (changed <= FloatGonePx && floatSeen) emptyCount++;
            if (floatSeen && emptyCount >= 5)
            { noFish++; Log(L.T("Nezabrala - splávek zmizel po ", "No bite - the float disappeared after ") + (sw.ElapsedMilliseconds / 1000.0 + 2.9).ToString("0.0") + " s."); Changed(); Wait(300); return; }
        }
        SetStatus(L.T("Minihra", "Minigame"));
        Log(L.T("Záběr! Hraji minihru.", "Bite! Playing the minigame."));
        games++; lastWasGame = true; Changed();
        PlayMinigame(bar, top);
        Release();
        Log(L.T("Minihra č. ", "Minigame #") + games + L.T(" skončila.", " finished."));
        SetStatus(L.T("Čekám na okno s výsledkem", "Waiting for the result window"));
        Log(L.T("Čekám na okno s rybou (max. ", "Waiting for the fish window (max. ") + (AfterGameDelayMs + 6000) / 1000 + " s).");

        // wait for the result dialog and close it with its X button
        var sw2 = Stopwatch.StartNew(); bool closed = false;
        while (sw2.ElapsedMilliseconds < AfterGameDelayMs + 6000)
        {
            if (!Wait(250)) return;
            Point? x = FindCloseButton(Grab());
            if (x != null)
            {
                if (!Wait(400)) return; // let the dialog finish its opening animation
                x = FindCloseButton(Grab());
                if (x == null) continue;
                ReadCatch(bufLoop); // fish name, size, stars -> statistics
                Click(x.Value);
                dialogsClosed++;
                Log(L.T("Zavřel jsem okno s výsledkem.", "Closed the result window."));
                closed = true; Changed();
                if (!Wait(1000)) return;
                sw2 = Stopwatch.StartNew(); continue; // another dialog (level up, quest...) may follow
            }
            if (closed && sw2.ElapsedMilliseconds > 1500) break;
        }
        if (!closed && closePoint != null) { Click(closePoint.Value); if (!Wait(800)) return; }
        if (!Wait(1500)) return;
        // level up ("OK" button, cannot be closed by Esc) or another window may follow the result dialog
        var after = Grab();
        if (CloseLevelUp(after)) after = Grab();
        if (IsDimmed(after)) ClearDialogs(true);
        Wait(500);
    }

    static void PlayMinigame(Bar bar, int top)
    {
        int L = bar.L, R = bar.R, mid = Math.Max(top + 10, bar.Y);
        double center = (L + R) / 2.0, dead = bar.W * 0.05;
        double prevX = -1, vel = 0; long prevT = 0; int lost = 0; double lastX = center;
        var sw = Stopwatch.StartNew();
        // only the strip with the bar is copied (~290 x 20 px instead of the whole window)
        var roi = new Rectangle(L, top, R - L + 1, mid - top + 1);
        Frame strip = null;
        while (true)
        {
            HandleHotkeys(); if (!running) return;
            strip = GrabRegion(roi, strip);
            double frac; double x = FindPointer(strip, 0, mid - top, 0, R - L, out frac);
            if (x >= 0) x += L;
            if (frac < 0.5) { if (++lost > 6) break; RepeatKeys(); Thread.Sleep(10); continue; }
            lost = 0;
            if (x < 0) x = lastX; lastX = x;

            long t = sw.ElapsedMilliseconds;
            if (prevX >= 0 && t > prevT) { double v = (x - prevX) / (t - prevT); vel = vel * 0.6 + v * 0.4; }
            prevX = x; prevT = t;

            double pred = x + vel * 140; // look ~4 game ticks ahead
            double err = pred - center;
            if (err > dead) Key(true, false);        // too far right -> push left
            else if (err < -dead) Key(false, true);  // too far left -> push right
            else Key(true, true);                    // both = no push, but resets the "idle" wobble
            Thread.Sleep(5);
        }
    }
}
