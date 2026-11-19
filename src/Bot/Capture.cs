// Capturing the game window picture (PrintWindow) into a Frame.
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
    // ---------------- capture (straight from the game window) ----------------
    public class Frame
    {
        public int W, H, Stride; public byte[] Px;
        public void Get(int x, int y, out int r, out int g, out int b)
        { int i = y * Stride + x * 4; b = Px[i]; g = Px[i + 1]; r = Px[i + 2]; }
    }
    static Bitmap capBmp; static Graphics capG;

    // Fixed buffers that are overwritten again and again (a full frame is ~8 MB - allocating a new one
    // for every capture made the memory use grow to hundreds of MB).
    static Frame bufBefore, bufA, bufB, bufLoop;

    static Frame Grab() { return bufLoop = Grab(bufLoop); }

    // captures the whole game window into "reuse" (if it has the right size) and returns it
    static Frame Grab(Frame reuse)
    {
        Rectangle all;
        PrintGame(out all);
        var f = CopyRegion(all, reuse);
        WatchPicture(f);   // watchdog: a picture that stopped changing = frozen / disconnected game
        return f;
    }

    // captures the game window but copies only the given region (frame coordinates start at the region)
    static Frame GrabRegion(Rectangle region, Frame reuse)
    {
        Rectangle all;
        PrintGame(out all);
        region.Intersect(all);
        if (region.Width <= 0 || region.Height <= 0) region = all;
        return CopyRegion(region, reuse);
    }

    static void PrintGame(out Rectangle all)
    {
        FindWindow();
        if (IsIconic(hwnd)) throw new Exception("Okno hry je minimalizované - obnov ho (může zůstat schované za jinými okny).");
        RECT r; GetClientRect(hwnd, out r);
        int w = Math.Max(1, r.R), h = Math.Max(1, r.B);
        if (capBmp == null || capBmp.Width != w || capBmp.Height != h)
        {
            if (capG != null) capG.Dispose(); if (capBmp != null) capBmp.Dispose();
            capBmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            capG = Graphics.FromImage(capBmp);
        }
        IntPtr hdc = capG.GetHdc();
        PrintWindow(hwnd, hdc, PW_RENDERFULLCONTENT | PW_CLIENTONLY);
        capG.ReleaseHdc(hdc);
        all = new Rectangle(0, 0, w, h);
    }

    static Frame CopyRegion(Rectangle rc, Frame reuse)
    {
        var d = capBmp.LockBits(rc, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        int rowBytes = rc.Width * 4;
        var f = (reuse != null && reuse.Px != null && reuse.Px.Length == rowBytes * rc.Height) ? reuse : new Frame { Px = new byte[rowBytes * rc.Height] };
        f.W = rc.Width; f.H = rc.Height; f.Stride = rowBytes;
        for (int y = 0; y < rc.Height; y++) Marshal.Copy(IntPtr.Add(d.Scan0, y * d.Stride), f.Px, y * rowBytes, rowBytes);
        capBmp.UnlockBits(d);
        return f;
    }
}
