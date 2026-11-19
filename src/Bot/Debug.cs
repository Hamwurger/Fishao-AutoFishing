// Debug screenshots.
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
    // ---------------- debug ----------------
    static void SaveDebug()
    {
        try
        {
            var f = Grab();
            var bar = FindBar(f);
            var xb = FindCloseButton(f);
            using (var bmp = (Bitmap)capBmp.Clone())
            using (var g = Graphics.FromImage(bmp))
            {
                string info = "bar: none";
                if (bar != null)
                {
                    g.DrawRectangle(Pens.Magenta, bar.L, bar.Y - 6, bar.W, 12);
                    int top = FindTop(f, bar);
                    double frac; double px = FindPointer(f, top, bar.Y, bar.L, bar.R, out frac);
                    if (px >= 0) g.DrawLine(Pens.Cyan, (float)px, bar.Y - 20, (float)px, bar.Y + 20);
                    info = "bar L=" + bar.L + " R=" + bar.R + " top=" + top + " mid=" + bar.Y + " pointer=" + px.ToString("0") + " barFrac=" + frac.ToString("0.00");
                }
                if (xb != null) { g.DrawEllipse(Pens.Magenta, xb.Value.X - 15, xb.Value.Y - 15, 30, 30); info += ", close X at " + xb.Value.X + "," + xb.Value.Y; }
                foreach (var cp in castPoints) g.DrawRectangle(Pens.Red, cp.X - PatchRadius, cp.Y - PatchRadius, PatchRadius * 2, PatchRadius * 2);
                string path = System.IO.Path.Combine(DebugDir, "debug_" + DateTime.Now.ToString("HHmmss") + ".png");
                bmp.Save(path, ImageFormat.Png);
                Log("Saved " + path + "  (" + info + ")");
            }
        }
        catch (Exception ex) { Log("Debug failed: " + ex.Message); }
    }
}
