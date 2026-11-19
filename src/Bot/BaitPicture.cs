// The chosen bait - ONE picture (bait.png), marked by the user either in the shop or in the bag.
// The bait's icon is about 1.5x bigger in the shop than in the bag, so the picture is searched in several
// sizes. Only the colourful pixels of the picture are compared (the cream/white background around the icon
// differs between the shop and the bag), so the same picture works in both windows.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

public static partial class FishaoBot
{
    public static string BaitPictureFile { get { return System.IO.Path.Combine(DebugDir, "bait.png"); } }

    static readonly float[] BaitScales = { 0.5f, 0.55f, 0.6f, 0.65f, 0.7f, 0.75f, 0.8f, 0.9f, 1f, 1.1f, 1.25f, 1.4f, 1.55f, 1.7f, 1.85f, 2f };
    static List<Template> baitSizes; static List<float> sizeScale = new List<float>(); static DateTime baitSizesTime;

    // older versions had a second picture for the shop (bait_shop.png): use the newer of the two
    static void MergeOldBaitPictures()
    {
        try
        {
            string shop = System.IO.Path.Combine(DebugDir, "bait_shop.png");
            if (!System.IO.File.Exists(shop)) return;
            if (!System.IO.File.Exists(BaitPictureFile) || System.IO.File.GetLastWriteTime(shop) > System.IO.File.GetLastWriteTime(BaitPictureFile))
            {
                if (System.IO.File.Exists(BaitPictureFile)) System.IO.File.Delete(BaitPictureFile);
                System.IO.File.Move(shop, BaitPictureFile);
            }
            else System.IO.File.Delete(shop);
        }
        catch { }
    }

    public static bool BaitChosen { get { MergeOldBaitPictures(); return System.IO.File.Exists(BaitPictureFile); } }

    static List<Template> BaitSizes()
    {
        try
        {
            MergeOldBaitPictures();
            if (!System.IO.File.Exists(BaitPictureFile)) return null;
            var time = System.IO.File.GetLastWriteTime(BaitPictureFile);
            if (baitSizes != null && time == baitSizesTime) return baitSizes;
            Bitmap pic;
            using (var fs = System.IO.File.OpenRead(BaitPictureFile)) using (var img = Image.FromStream(fs)) pic = new Bitmap(img);
            using (pic)
            using (var masked = MaskBackground(pic))
            {
                var list = new List<Template>(); sizeScale = new List<float>();
                foreach (float s in BaitScales)
                {
                    int w = Math.Max(8, (int)(masked.Width * s)), h = Math.Max(8, (int)(masked.Height * s));
                    using (var b = new Bitmap(w, h, PixelFormat.Format32bppArgb))
                    {
                        using (var g = Graphics.FromImage(b))
                        {
                            g.Clear(Color.Transparent);
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            g.DrawImage(masked, new Rectangle(0, 0, w, h));
                        }
                        var t = Template.FromBitmap(b, true);
                        if (t.Count >= 40) { list.Add(t); sizeScale.Add(s); }
                    }
                }
                baitSizes = list; baitSizesTime = time;
                return list;
            }
        }
        catch { return null; }
    }

    // keeps the icon (colourful or dark pixels), makes the plain light background transparent
    static Bitmap MaskBackground(Bitmap pic)
    {
        var m = new Bitmap(pic.Width, pic.Height, PixelFormat.Format32bppArgb);
        for (int y = 0; y < pic.Height; y++)
            for (int x = 0; x < pic.Width; x++)
            {
                var c = pic.GetPixel(x, y);
                int mx = Math.Max(c.R, Math.Max(c.G, c.B)), mn = Math.Min(c.R, Math.Min(c.G, c.B));
                bool icon = mx - mn >= 45 || mx < 110;
                m.SetPixel(x, y, icon ? Color.FromArgb(255, c) : Color.Transparent);
            }
        return m;
    }

    // Best place of the chosen bait inside "area" (any of the sizes), or null. The rectangle is the match.
    public static Rectangle? FindBaitPicture(Frame f, Rectangle area)
    {
        var sizes = BaitSizes();
        if (sizes == null) return null;
        Rectangle? best = null; double bestAvg = double.MaxValue;
        for (int i = 0; i < sizes.Count; i++)
        {
            var t = sizes[i];
            double avg;
            // an enlarged picture is blurry - allow a bit more difference for it
            var p = FindTemplateIn(f, t, sizeScale[i] > 1.05f ? 36 : 28, area, out avg);
            if (p != null && avg < bestAvg) { bestAvg = avg; best = new Rectangle(p.Value.X, p.Value.Y, t.W, t.H); }
        }
        return best;
    }
}
