// Buying energy when it runs out.
// The game shows "KOUPIT DALŠÍ ENERGII!" with 5 packs; the bot buys the one picked in the settings.
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
    // ---------------- energy ----------------
    public static bool AutoBuyEnergy = true;
    public static int EnergyWaitMinutes = 3;        // wait for free energy this long, then try again
    public static int NoMoneyRetryMinutes = 30;     // after a failed purchase, do not try to buy again for this long

    // the packs in the shop window, top to bottom
    public static readonly int[] EnergyPacks = { 5, 15, 30, 60, 100 };
    public static readonly int[] EnergyPrices = { 35, 100, 185, 340, 525 };
    public static int EnergyPack = 4;               // index of the pack to buy (4 = 100 energy)

    // rows of the packs relative to the last row (100 energy), in game pixels
    static readonly int[] PackRowDy = { -316, -237, -159, -79, 0 };

    static int energyUnits = 0; static long noMoneyUntil = 0;
    public static int EnergyBought { get { return energyUnits; } }     // energy bought this session

    // called when the energy shop window is open; buy100 = green button of the "100 ENERGIE" row.
    // Returns false if the bot was stopped.
    static bool HandleNoEnergy(Point buy100, Frame beforeCast)
    {
        double normal = OuterBrightness(beforeCast);
        int pack = Math.Max(0, Math.Min(EnergyPacks.Length - 1, EnergyPack));
        int n = EnergyPacks[pack], price = EnergyPrices[pack];
        bool mayBuy = AutoBuyEnergy && clock.ElapsedMilliseconds >= noMoneyUntil;
        if (mayBuy)
        {
            SetStatus(L.T("Kupuji " + n + " energie", "Buying " + n + " energy"));
            Log(L.T("Došla energie - kupuji " + n + " energie za " + price + " zelených.", "Out of energy - buying " + n + " energy for " + price + " green."));
            Click(new Point(buy100.X, buy100.Y + PackRowDy[pack]));
            if (!Wait(2500)) return false;
            var f = Grab();
            if (FindEnergyBuyButton(f) == null && OuterBrightness(f) > normal * 0.85)
            {
                energyUnits += n; Changed();
                Log(L.T("Koupeno " + n + " energie.", "Bought " + n + " energy."));
                return true; // bought - cast again right away
            }
            // still darkened = the game opened the bank instead (not enough green money)
            Log(L.T("Nákup se nepovedl - nejspíš nemáš dost zelených peněz.", "Buying failed - probably not enough green money."));
            noMoneyUntil = clock.ElapsedMilliseconds + NoMoneyRetryMinutes * 60000L;
        }
        else if (clock.ElapsedMilliseconds - lastEnergyLog > 10 * 60000L)   // not every minute
            Log(AutoBuyEnergy ? L.T("Došla energie (na nákup nejsou peníze).", "Out of energy (no money to buy more).") : L.T("Došla energie (automatický nákup je vypnutý).", "Out of energy (automatic buying is off)."));

        if (!CloseAllDialogs(normal)) return false;

        // The game shows "more in 00:59" under the energy - wait exactly that long, then cast again.
        // (If it is still not enough, the game shows the energy window again and this repeats.)
        int secs = ReadEnergyCountdown();
        if (secs >= 0)
        {
            if (clock.ElapsedMilliseconds - lastEnergyLog > 10 * 60000L)
                Log(L.T("Počkám na další energii (odpočet ve hře) a hned nahodím.", "Waiting for the next energy (the game's countdown), then casting right away."));
            lastEnergyLog = clock.ElapsedMilliseconds;
            var ready = DateTime.Now.AddSeconds(secs + 2);
            while (DateTime.Now < ready)
            {
                var left = ready - DateTime.Now;
                SetStatus(L.T("Čekám na energii - další za ", "Waiting for energy - next in ") + ((int)left.TotalMinutes) + ":" + left.Seconds.ToString("00"));
                if (!Wait(1000)) return false;
            }
            return true;
        }
        lastEnergyLog = clock.ElapsedMilliseconds;

        // countdown not readable - wait the time from the settings
        var until = DateTime.Now.AddMinutes(EnergyWaitMinutes);
        SetStatus(L.T("Čekám na energii (do ", "Waiting for energy (until ") + until.ToString("HH:mm") + ")");
        Log(L.T("Čekám ", "Waiting ") + EnergyWaitMinutes + L.T(" min, než se energie sama doplní.", " min for the energy to refill by itself."));
        return Wait(EnergyWaitMinutes * 60000);
    }

    static long lastEnergyLog = -999999999;

    // Seconds until the next energy ("more in 00:59" / "další za 00:46" under the energy counter at the top),
    // or -1 when it is not shown (energy full) or can't be read. Read from a fresh picture without dialogs.
    public static int ReadEnergyCountdown()
    {
        try
        {
            if (!WinOcr.Available) return -1;
            Grab();
            if (capBmp == null) return -1;
            var rc = new Rectangle(capBmp.Width / 2 - 92, 42, 92, 18);   // the small line under the energy box
            foreach (int k in new[] { 3, 4 })
                using (var b = new Bitmap(rc.Width * k + 60, rc.Height * k + 60))
                {
                    using (var g = Graphics.FromImage(b))
                    {
                        g.Clear(Color.FromArgb(246, 232, 200));
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.DrawImage(capBmp, new Rectangle(30, 30, rc.Width * k, rc.Height * k), rc, GraphicsUnit.Pixel);
                    }
                    foreach (var line in WinOcr.Read(b))
                    {
                        var m = Regex.Match(line, @"(\d{1,2})\s*[:.]\s*(\d{2})");
                        if (m.Success) return int.Parse(m.Groups[1].Value) * 60 + int.Parse(m.Groups[2].Value);
                    }
                }
        }
        catch { }
        return -1;
    }

    // closes dialogs until the window is bright again (max. a few tries)
    static bool CloseAllDialogs(double normalBrightness)
    {
        LearnBrightness(normalBrightness);
        return ClearDialogs(false);
    }

    // only plausible values (a dialog-free game view is ~290-340, a dimmed one ~170)
    static void LearnBrightness(double b) { if (b >= 230 && b <= 700) normalBright = b; } // beach sand is very bright
}
