// The face picture (Resources\splash.png, built into the exe), cropped to its visible part.
// Loaded once and shared by the loading screen, the logo and the floating faces. Never dispose it.
using System;
using System.Drawing;
using System.Reflection;

public static class FaceImage
{
    static Bitmap face;
    static bool tried;

    public static Bitmap Get()
    {
        if (tried) return face;
        tried = true;
        try
        {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("splash.png"))
            {
                if (s == null) return null;
                using (var full = new Bitmap(s))
                {
                    // crop away the transparent border
                    int minX = full.Width, minY = full.Height, maxX = -1, maxY = -1;
                    for (int y = 0; y < full.Height; y += 2)
                        for (int x = 0; x < full.Width; x += 2)
                            if (full.GetPixel(x, y).A > 40) { minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y); }
                    if (maxX < 0) return null;
                    var r = Rectangle.FromLTRB(minX, minY, Math.Min(full.Width, maxX + 2), Math.Min(full.Height, maxY + 2));
                    face = full.Clone(r, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                }
            }
        }
        catch { face = null; }
        return face;
    }
}
