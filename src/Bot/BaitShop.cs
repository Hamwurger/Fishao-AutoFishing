// Buying bait in the game's SHOP (when the chosen bait is not in the bag any more).
//   basket button on the bottom bar -> SHOP window -> "Bait" tab -> find the chosen bait (bait_shop.png),
//   dragging the list's scroll bar down if needed -> click its price (gold coins or green money)
//   -> close the shop. Then the bait is put on from the bag (Bait.cs).
// Positions inside the shop are taken relative to its close button (X), so they don't depend on the language.
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;

public static partial class FishaoBot
{
    public static int BaitPay = 0;              // 0 = green money, 1 = gold coins, 2 = either (green first)
    public static int BaitPacks = 1;            // how many packs to buy at once (a pack = e.g. 40 cherries)


    // relative to the shop's close button (X)
    static readonly Point ShopBaitTab = new Point(-578, 49);      // the "Bait" tab
    static readonly Point ShopScrollBar = new Point(-45, 290);    // the scroll bar right of the list
    const int ShopTrackTop = 90, ShopTrackBottom = 492;           // its track, from the close button's Y

    // the scroll bar's thumb (a white pill in the track), or null
    public static Rectangle? FindScrollThumb(Frame f, int x, int top, int bottom)
    {
        int first = -1, last = -1, r, g, b;
        for (int y = Math.Max(0, top); y <= Math.Min(f.H - 1, bottom); y++)
        {
            f.Get(x, y, out r, out g, out b);
            if (r > 245 && g > 245 && b > 238) { if (first < 0) first = y; last = y; }
            else if (first >= 0 && y - last > 3) break;
        }
        if (first < 0 || last - first < 6) return null;
        return new Rectangle(x - 4, first, 9, last - first + 1);
    }

    // press the left button at "from", move to "to" in small steps with the button held, release
    static void Drag(Point from, Point to)
    {
        FindWindow();
        PostMessage(hwnd, WM_MOUSEMOVE, IntPtr.Zero, LParam(from)); Thread.Sleep(40);
        PostMessage(hwnd, WM_LBUTTONDOWN, (IntPtr)1, LParam(from)); Thread.Sleep(60);
        const int steps = 8;
        for (int i = 1; i <= steps; i++)
        {
            var p = new Point(from.X + (to.X - from.X) * i / steps, from.Y + (to.Y - from.Y) * i / steps);
            PostMessage(hwnd, WM_MOUSEMOVE, (IntPtr)1, LParam(p));   // wParam 1 = left button held
            Thread.Sleep(25);
        }
        PostMessage(hwnd, WM_LBUTTONUP, IntPtr.Zero, LParam(to));
    }

    // basket (shop) button on the bottom bar - the bar sits at the bottom middle of the window
    static Point ShopButton(Frame f) { return new Point(f.W / 2 - 184, f.H - 47); }

    // The bar's buttons turn grey while the line is out (the shop can't be opened then); otherwise the basket
    // is coloured (pink/magenta).
    public static bool ShopButtonEnabled(Frame f)
    {
        var c = ShopButton(f); int n = 0, r, g, b;
        for (int y = c.Y - 20; y <= c.Y + 20; y += 2)
            for (int x = c.X - 22; x <= c.X + 22; x += 2)
            {
                if (x < 0 || y < 0 || x >= f.W || y >= f.H) continue;
                f.Get(x, y, out r, out g, out b);
                if (r > 170 && b > 110 && g < 120 && r - g > 80) n++;
            }
        return n >= 25;
    }

    static long baitShopRetryAt = 0;

    // the chosen bait in the shop list (only inside the list - left of the price buttons), or null
    public static Rectangle? FindShopBait(Frame f, Point shopX)
    {
        return FindBaitPicture(f, Rectangle.FromLTRB(shopX.X - 730, shopX.Y + 75, shopX.X - 250, shopX.Y + 500));
    }

    // The price buttons are in a column on the right of the list (found from the shop's close button X):
    // gold coins (orange) above the row's middle cy, green money below it. Bait sold only for gold has no green one.
    public static Point? FindPriceButton(Frame f, Point shopX, int cy, bool gold)
    {
        int x0 = Math.Max(0, shopX.X - 240), x1 = Math.Min(f.W - 1, shopX.X - 60);
        // gold coins button is above the row's middle, green money below it
        int y0 = Math.Max(0, gold ? cy - 90 : cy - 5), y1 = Math.Min(f.H - 1, gold ? cy + 5 : cy + 90);
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1, n = 0, r, g, b;
        for (int y = y0; y <= y1; y += 2)
            for (int x = x0; x <= x1; x += 2)
            {
                f.Get(x, y, out r, out g, out b);
                bool hit = gold ? (r > 230 && g > 140 && g < 215 && b < 70)          // orange button
                                : (g > 170 && r < 150 && b < 90 && g - r > 50);      // green button
                if (!hit) continue;
                n++; minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
            }
        if (n < 120 || maxX - minX < 40) return null;
        // the button's right part (the number) - the left part has the coin / money picture
        return new Point(minX + (maxX - minX) * 2 / 3, (minY + maxY) / 2);
    }

    // returns true when at least one pack was bought
    static bool BuyBait()
    {
        if (clock.ElapsedMilliseconds < baitShopRetryAt) return false;
        if (!BaitChosen)
        {
            Log(L.T("Návnada není vybraná (Nastavení → Návnady → Vybrat návnadu).", "No bait chosen (Settings → Bait → Pick the bait)."));
            return false;
        }
        var f0 = Grab();
        // a grey basket = the line is still out; wait a moment for it
        for (int i = 0; i < 10 && !ShopButtonEnabled(f0); i++) { if (!Wait(1000)) return false; f0 = Grab(); }
        if (!ShopButtonEnabled(f0))
        {
            Log(L.T("Obchod teď nejde otevřít (tlačítko je šedé).", "The shop can't be opened now (the button is grey)."));
            baitShopRetryAt = clock.ElapsedMilliseconds + 60000L;
            return false;
        }
        double normal = OuterBrightness(f0);
        SetStatus(L.T("Kupuji návnadu", "Buying bait"));
        Click(ShopButton(f0));
        if (!Wait(2000)) return false;

        var f = Grab();
        var x = FindCloseButton(f);
        if (x == null || !IsDimmed(f))
        {
            Log(L.T("Obchod se neotevřel.", "The shop did not open."));
            CloseAllDialogs(normal);
            baitShopRetryAt = clock.ElapsedMilliseconds + 5 * 60000L;
            return false;
        }
        var X = x.Value;
        Click(new Point(X.X + ShopBaitTab.X, X.Y + ShopBaitTab.Y));
        if (!Wait(1000)) return false;

        // Find the bait. The mouse wheel sent in the background does not scroll the list, so the scroll bar's
        // thumb is dragged instead: first to the very top (the shop may remember the last position), then down
        // a bit at a time, looking for the bait after every step.
        int trackX = X.X + ShopScrollBar.X, trackTop = X.Y + ShopTrackTop, trackBottom = X.Y + ShopTrackBottom;
        f = Grab();
        var thumb = FindScrollThumb(f, trackX, trackTop, trackBottom);
        if (thumb != null && thumb.Value.Top > trackTop + 2)
        {
            Drag(new Point(trackX, thumb.Value.Y + thumb.Value.Height / 2), new Point(trackX, trackTop));
            if (!Wait(500)) return false;
        }
        Rectangle? item = null;
        for (int i = 0; i < 40 && item == null; i++)
        {
            f = Grab();
            item = FindShopBait(f, X);
            if (item != null) break;
            thumb = FindScrollThumb(f, trackX, trackTop, trackBottom);
            if (thumb == null || thumb.Value.Bottom >= trackBottom - 2) break;          // no scroll bar / at the end
            int from = thumb.Value.Y + thumb.Value.Height / 2;
            int step = Math.Max(8, thumb.Value.Height * 3 / 4);                         // ~ most of one visible page
            Drag(new Point(trackX, from), new Point(trackX, from + step));
            if (!Wait(450)) return false;
        }
        if (item == null)
        {
            Log(L.T("Vybranou návnadu jsem v obchodě nenašel.", "Could not find the chosen bait in the shop."));
            CloseAllDialogs(normal);
            baitShopRetryAt = clock.ElapsedMilliseconds + 10 * 60000L;
            return false;
        }
        // price: green money / gold coins / either (green first) - some bait is sold only for gold
        int cy = item.Value.Y + item.Value.Height / 2;
        Point? green = FindPriceButton(f, X, cy, false), gold = FindPriceButton(f, X, cy, true);
        Point? price = BaitPay == 0 ? green : BaitPay == 1 ? gold : (green ?? gold);
        bool payGold = price != null && price == gold && price != green;
        if (price == null)
        {
            if (BaitPay == 0 && gold != null)
                Log(L.T("Tahle návnada se dá koupit jen za zlaté mince - v Nastavení → Návnady zvol platbu „Zlaté“ nebo „Čímkoli“.",
                        "This bait can only be bought for gold coins - choose \"Gold\" or \"Either\" payment in Settings → Bait."));
            else if (BaitPay == 1 && green != null)
                Log(L.T("Tahle návnada se dá koupit jen za zelené peníze - v Nastavení → Návnady zvol platbu „Zelené“ nebo „Čímkoli“.",
                        "This bait can only be bought for green money - choose \"Green\" or \"Either\" payment in Settings → Bait."));
            else Log(L.T("Nenašel jsem tlačítko s cenou.", "Could not find the price button."));
            CloseAllDialogs(normal);
            baitShopRetryAt = clock.ElapsedMilliseconds + 10 * 60000L;
            return false;
        }

        int bought = 0;
        for (int k = 0; k < Math.Max(1, BaitPacks); k++)
        {
            Click(price.Value);
            if (!Wait(1800)) return false;
            // still in the shop (its X at the same place) = bought; otherwise e.g. the bank opened (no money)
            var after = Grab();
            var x2 = FindCloseButton(after);
            if (x2 == null || Math.Abs(x2.Value.X - X.X) > 6 || Math.Abs(x2.Value.Y - X.Y) > 6)
            {
                Log(L.T("Nákup návnady se nepovedl - nejspíš nemáš dost " + (payGold ? "zlatých mincí." : "zelených peněz."),
                        "Buying bait failed - probably not enough " + (payGold ? "gold coins." : "green money.")));
                baitShopRetryAt = clock.ElapsedMilliseconds + 30 * 60000L;
                break;
            }
            bought++;
        }
        if (bought > 0) Log(L.T("Koupeno balíčků návnady: " + bought + ".", "Bait packs bought: " + bought + "."));
        CloseAllDialogs(normal);
        Wait(800);
        return bought > 0;
    }
}
