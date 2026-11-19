// Bait check - the NÁVNADY slot shows a hook when no bait is set.
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
    // ---------------- bait ----------------
    public static bool AutoBait = true;        // put on new bait when it runs out
    public static bool BaitFromShop = false;   // when the bag has none: buy it in the shop

    // The "NÁVNADY" slot (bottom right) shows a hook on a grey background when no bait is set.
    // It is anchored to the right/bottom edge of the window.
    public static bool BaitEmpty(Frame f)
    {
        // the hook on the white tile (bait_empty.png) - searched near its usual place, the slot is anchored
        // to the bottom right corner of the window
        var t = Tpl("bait_empty.png");
        if (t != null && FindTemplateIn(f, t, 22, new Rectangle(f.W - 98, f.H - 207, 17, 17)) != null) return true;

        // older check: hook on a pinkish grey background
        int hits = 0, r, g, b;
        foreach (int dx in new[] { 84, 72, 54, 42 })
            foreach (int dy in new[] { 191, 174, 157 })
            {
                int x = f.W - dx, y = f.H - dy;
                if (x < 0 || y < 0) continue;
                f.Get(x, y, out r, out g, out b);
                if (r >= 190 && r <= 240 && g >= 160 && g <= 220 && Math.Abs(g - b) <= 12 && r - g >= 8 && r - g <= 45) hits++;
            }
        return hits >= 5;
    }

    static long lastBaitLog = -999999, lastBaitTry = -999999;
    static void HandleNoBait()
    {
        SetStatus(L.T("Došla návnada", "Out of bait"));
        // 1) the chosen bait from the bag, 2) if there is none and buying is on: buy it in the shop, then put it on.
        // At most every 2 minutes, so a failure doesn't loop.
        if (AutoBait && clock.ElapsedMilliseconds - lastBaitTry > 120000)
        {
            lastBaitTry = clock.ElapsedMilliseconds;
            if (PutOnBaitFromBag()) return;
            if (BaitFromShop && BuyBait())
            {
                if (!Wait(800)) return;
                if (PutOnBaitFromBag()) return;
            }
        }
        if (clock.ElapsedMilliseconds - lastBaitLog > 300000)
        {
            Log(L.T("Došla návnada (v rámečku NÁVNADY je jen háček). Nasaď návnadu - bot pak sám pokračuje.", "Out of bait (the bait slot shows only a hook). Put on a bait - the bot then continues by itself."));
            lastBaitLog = clock.ElapsedMilliseconds;
        }
        Wait(5000);
    }

    // ---- putting on bait from the bag:
    //   click the BAIT slot -> INVENTORY window opens on the "Bait" tab -> find the chosen bait (bait.png)
    //   in the list -> click it -> click the blue "Select" button -> close the window
    static Point BaitSlot(Frame f) { return new Point(f.W - 63, f.H - 174); }   // centre of the BAIT slot (bottom right)

    // where the chosen bait is in the open inventory (centre), or null. The BAIT slot itself (bottom right) is skipped.
    public static Point? FindChosenBait(Frame f)
    {
        // only inside the inventory window (its cream panel) - never in the game picture around it
        var panel = FindPanel(f);
        if (panel == null) return null;
        var all = panel.Value;
        var p = FindBaitPicture(f, all);
        if (p == null) return null;
        // the selected bait is also shown big in the detail at the top - the one to click is in the list below it
        var lower = FindBaitPicture(f, Rectangle.FromLTRB(all.Left, p.Value.Bottom + 20, all.Right, all.Bottom));
        if (lower != null) p = lower;
        return new Point(p.Value.X + p.Value.Width / 2, p.Value.Y + p.Value.Height / 2);
    }

    // blue "Select" button in the inventory (bright blue - the dimmed water around the window is much darker)
    public static Point? FindSelectButton(Frame f)
    {
        // only the upper right part of the inventory window - the button is there, and a blue bait in the
        // list below (e.g. a fish) would otherwise merge with it
        var panel = FindPanel(f);
        var area = panel != null
            ? Rectangle.FromLTRB(panel.Value.X + panel.Value.Width * 55 / 100, panel.Value.Y, panel.Value.Right, panel.Value.Y + panel.Value.Height * 35 / 100)
            : Rectangle.FromLTRB(f.W / 3, f.H / 10, f.W * 9 / 10, f.H * 7 / 10);
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1, n = 0, r, g, b;
        for (int y = Math.Max(0, area.Top); y < Math.Min(f.H, area.Bottom); y += 2)
            for (int x = Math.Max(0, area.Left); x < Math.Min(f.W, area.Right); x += 2)
            {
                f.Get(x, y, out r, out g, out b);
                if (b > 200 && g > 110 && g < 200 && r < 100)
                { n++; minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y); }
            }
        // a button-sized blue area (about 110 x 32 px)
        if (n < 150 || maxX - minX < 50 || maxX - minX > 260 || maxY - minY < 15 || maxY - minY > 70) return null;
        return new Point((minX + maxX) / 2, (minY + maxY) / 2);
    }

    static bool PutOnBaitFromBag()
    {
        if (!BaitChosen)
        {
            Log(L.T("Návnada k nasazení není vybraná (Nastavení → Návnady → Vybrat návnadu).", "No bait chosen to put on (Settings → Bait → Pick the bait)."));
            return false;
        }
        var before = Grab();
        double normal = OuterBrightness(before);
        SetStatus(L.T("Nasazuji návnadu", "Putting on bait"));
        Click(BaitSlot(before));
        if (!Wait(1500)) return false;

        var f = Grab();
        var bait = FindChosenBait(f);
        if (bait == null)
        {
            Log(L.T("Vybraná návnada v batohu není (asi došla).", "The chosen bait is not in the bag (probably used up)."));
            CloseAllDialogs(normal);
            return false;
        }
        Click(bait.Value);
        if (!Wait(900)) return false;
        var sel = FindSelectButton(Grab());
        if (sel == null)
        {
            Log(L.T("Nenašel jsem tlačítko pro nasazení návnady.", "Could not find the button to put the bait on."));
            CloseAllDialogs(normal);
            return false;
        }
        Click(sel.Value);
        if (!Wait(1200)) return false;
        CloseAllDialogs(normal);
        if (!Wait(800)) return false;
        bool ok = !BaitEmpty(Grab());
        Log(ok ? L.T("Nasadil jsem novou návnadu z batohu.", "Put on new bait from the bag.")
               : L.T("Návnadu se nepodařilo nasadit.", "Could not put the bait on."));
        return ok;
    }
}
