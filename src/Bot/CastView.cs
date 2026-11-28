// Remembers what the game looked like when the cast spots were picked (a tiny grey picture, castview.png).
// When the bot is started somewhere else (another fishing spot, another window size), the saved spots point
// at sand, piers or other players - the character then walks away instead of casting. So on start the bot
// compares the view with the saved one and stops with a clear message when it's clearly another place.
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

public static partial class FishaoBot
{
    public static string CastViewFile { get { return Path.Combine(DebugDir, "castview.png"); } }
    const int ThumbW = 96;
    public static double OtherPlaceLimit = 21;   // mean grey difference (0-255) above which it's another place
    static bool checkView;                        // set on Start: compare the first picture with castview.png

    // block averages, ThumbW wide (height keeps the window's shape)
    static byte[,] Thumb(Frame f)
    {
        int tw = ThumbW, th = Math.Max(1, f.H * ThumbW / Math.Max(1, f.W));
        var t = new byte[tw, th];
        for (int ty = 0; ty < th; ty++)
            for (int tx = 0; tx < tw; tx++)
            {
                int x0 = tx * f.W / tw, x1 = Math.Max(x0 + 1, (tx + 1) * f.W / tw);
                int y0 = ty * f.H / th, y1 = Math.Max(y0 + 1, (ty + 1) * f.H / th);
                long s = 0; int n = 0;
                for (int y = y0; y < y1; y += 2)
                    for (int x = x0; x < x1; x += 2)
                    { int i = y * f.Stride + x * 4; s += f.Px[i] + f.Px[i + 1] * 2 + f.Px[i + 2]; n++; }
                t[tx, ty] = (byte)(n == 0 ? 0 : s / (n * 4));
            }
        return t;
    }

    static Frame FrameOf(Bitmap bmp)
    {
        using (var b = bmp.Clone(new Rectangle(0, 0, bmp.Width, bmp.Height), PixelFormat.Format32bppArgb))
        {
            var d = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var f = new Frame { W = b.Width, H = b.Height, Stride = b.Width * 4, Px = new byte[b.Width * 4 * b.Height] };
            for (int y = 0; y < b.Height; y++) Marshal.Copy(IntPtr.Add(d.Scan0, y * d.Stride), f.Px, y * f.Stride, f.Stride);
            b.UnlockBits(d);
            return f;
        }
    }

    // called when new cast spots are saved (shot = the picture they were picked on)
    public static void SaveCastView(Bitmap shot)
    {
        try
        {
            var t = Thumb(FrameOf(shot));
            using (var b = new Bitmap(t.GetLength(0), t.GetLength(1), PixelFormat.Format24bppRgb))
            {
                for (int y = 0; y < b.Height; y++) for (int x = 0; x < b.Width; x++) { int v = t[x, y]; b.SetPixel(x, y, Color.FromArgb(v, v, v)); }
                b.Save(CastViewFile, ImageFormat.Png);
            }
        }
        catch { }
    }

    // how different the view is from the one the spots were picked on (0 = same). -1 = nothing saved.
    static double CastViewDifference(Frame now)
    {
        if (!File.Exists(CastViewFile)) return -1;
        byte[,] saved;
        try
        {
            using (var b = new Bitmap(CastViewFile))
            {
                saved = new byte[b.Width, b.Height];
                for (int y = 0; y < b.Height; y++) for (int x = 0; x < b.Width; x++) saved[x, y] = b.GetPixel(x, y).R;
            }
        }
        catch { return -1; }
        var cur = Thumb(now);
        if (Math.Abs(cur.GetLength(1) - saved.GetLength(1)) > 2) return 255;   // other window shape
        int w = ThumbW, h = Math.Min(cur.GetLength(1), saved.GetLength(1));
        // only the middle: the game's buttons and bars at the edges are the same everywhere
        // compared in 4x4 blocks of the small picture: moving waves and walking players average out,
        // a different place (other ground, other water, other shape of the shore) does not
        const int B = 4;
        long s = 0; int n = 0;
        for (int y = h * 10 / 100; y + B <= h * 86 / 100; y += B)
            for (int x = w * 5 / 100; x + B <= w * 88 / 100; x += B)
            {
                int a = 0, c = 0;
                for (int yy = 0; yy < B; yy++) for (int xx = 0; xx < B; xx++) { a += cur[x + xx, y + yy]; c += saved[x + xx, y + yy]; }
                s += Math.Abs(a - c) / (B * B); n++;
            }
        return n == 0 ? -1 : (double)s / n;
    }

    // first picture after Start: still the place where the spots were picked?
    static bool CheckCastView(Frame before)
    {
        if (!checkView) return true;
        checkView = false;
        double d = CastViewDifference(before);
        if (d < 0) return true;   // spots picked by an older version - nothing to compare with
        if (d <= OtherPlaceLimit) return true;
        ReportProblem(L.T("Vypadá to, že jsi na jiném místě, než kde jsi vybíral místa nahození (nebo má okno hry jinou velikost). Vyber je znovu: Nastavení → Vybrat místa / oblast na vodě.",
                          "It looks like you are at another place than where the cast spots were picked (or the game window has another size). Pick them again: Settings → Pick points / area on the water."));
        return false;
    }
}
