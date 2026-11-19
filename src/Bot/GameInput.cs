// Finding the game window and sending clicks/keys straight to it (PostMessage).
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
    // ---------------- game window ----------------
    static void FindWindow()
    {
        RECT r;
        if (hwnd != IntPtr.Zero && GetClientRect(hwnd, out r)) return;
        hwnd = IntPtr.Zero;
        foreach (var p in Process.GetProcessesByName("FishaoGame"))
            if (p.MainWindowHandle != IntPtr.Zero) { hwnd = p.MainWindowHandle; break; }
        if (hwnd == IntPtr.Zero) throw new GameGoneException(L.T("Hra Fishao neběží - spadla nebo byla zavřena. Bot jsem zastavil.", "Fishao is not running - it crashed or was closed. The bot was stopped."));
    }

    // ---------------- input (posted straight to the game window) ----------------
    static IntPtr LParam(Point p) { return (IntPtr)((p.Y << 16) | (p.X & 0xFFFF)); }

    static void Click(Point p)
    {
        FindWindow();
        var lp = LParam(p);
        PostMessage(hwnd, WM_MOUSEMOVE, IntPtr.Zero, lp); Thread.Sleep(40);
        PostMessage(hwnd, WM_LBUTTONDOWN, (IntPtr)1, lp); Thread.Sleep(60);
        PostMessage(hwnd, WM_LBUTTONUP, IntPtr.Zero, lp);
    }

    // The game only casts when its rod-range circle is shown, and it shows the circle only after the mouse
    // has hovered over the water for a few frames. Otherwise a click on the water makes the character walk.
    static void HoverThenClick(Point p, int hoverMs)
    {
        FindWindow();
        var sw = Stopwatch.StartNew(); int i = 0;
        while (sw.ElapsedMilliseconds < hoverMs)
        {
            var q = new Point(p.X + (i % 2), p.Y); i++; // tiny wiggle so every message is a real move
            PostMessage(hwnd, WM_MOUSEMOVE, IntPtr.Zero, LParam(q));
            Thread.Sleep(35);
        }
        Click(p);
    }

    static void PostKey(int vk, bool down, bool repeat)
    {
        uint scan = MapVirtualKey((uint)vk, 0);
        long lp = 1 | (scan << 16);
        if (repeat) lp |= 1L << 30;
        if (!down) lp |= (1L << 30) | (1L << 31);
        PostMessage(hwnd, down ? WM_KEYDOWN : WM_KEYUP, (IntPtr)vk, (IntPtr)(int)lp);
    }

    static void Key(bool wantA, bool wantD)
    {
        if (hwnd == IntPtr.Zero) { aDown = dDown = false; return; }
        if (wantA != aDown) { PostKey(VK_A, wantA, false); aDown = wantA; }
        if (wantD != dDown) { PostKey(VK_D, wantD, false); dDown = wantD; }
        RepeatKeys();
    }

    // like keyboard auto-repeat: the game forgets held keys on focus changes, so keep re-sending them
    static void RepeatKeys()
    {
        if (clock.ElapsedMilliseconds - lastKeyRepeat < 120) return;
        lastKeyRepeat = clock.ElapsedMilliseconds;
        if (aDown) PostKey(VK_A, true, true);
        if (dDown) PostKey(VK_D, true, true);
    }

    static void Release() { Key(false, false); }
}
