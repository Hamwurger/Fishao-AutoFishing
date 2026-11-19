// Faces slowly floating up behind the whole window, just for fun (can be turned off in the settings).
// They live in window coordinates and are drawn on every dark background panel ("layer"), under the cards,
// so they never cover any text or number.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

public class FloatingFaces : IDisposable
{
    class Face
    {
        public float X, Y, Size, Vy, Phase, Swing, Spin, Angle;
        public Bitmap Sprite;    // pre-scaled, semi-transparent copy of the face (fast to draw)
    }

    public const int Count = 20;
    readonly Form form;
    readonly List<Face> faces = new List<Face>();
    readonly List<Control> layers = new List<Control>();
    readonly List<Control> corners = new List<Control>();   // rounded controls - only their corners are repainted
    readonly Timer timer = new Timer { Interval = 40 };   // ~25 frames a second
    readonly Random rnd = new Random();
    bool enabled;
    float t;

    public FloatingFaces(Form form)
    {
        this.form = form;
        Theme.Backdrop = delegate (Control c, Graphics g) { DrawOn(c, g); };
        timer.Tick += delegate { Step(); };
    }

    public bool Enabled
    {
        get { return enabled; }
        set
        {
            if (value == enabled) return;
            enabled = value;
            if (enabled && faces.Count == 0) Create();
            if (enabled) timer.Start(); else timer.Stop();
            foreach (var c in layers) c.Invalidate();
            foreach (var c in corners) c.Invalidate();
        }
    }

    // finds the dark panels of the window (again after the window was rebuilt) and draws the faces on them
    public void Attach()
    {
        layers.Clear(); corners.Clear();
        Collect(form);
        foreach (var c in layers) c.Invalidate();
    }

    void Collect(Control parent)
    {
        foreach (Control c in parent.Controls)
        {
            // rounded things lying on the window background: the faces show in their corners (Theme.PaintParentBg),
            // only the corners need to be refreshed while the faces move
            bool onBg = c.Parent != null && (c.Parent.BackColor == Theme.Bg || c.Parent.BackColor == Theme.Side);
            if (onBg && (c is Card || c is Tile || c is ModernButton || c is StatusChip || c is Segmented) && !corners.Contains(c)) corners.Add(c);
            if (c is Card || c is ListBox) continue;           // inside the cards everything stays solid
            bool layer = (c is Panel || c is TableLayoutPanel || c is FlowLayoutPanel) && (c.BackColor == Theme.Bg || c.BackColor == Theme.Side);
            if (c is NavItem && !layers.Contains(c)) { layers.Add(c); continue; }   // draws the faces itself (Theme.Backdrop)
            if (layer && !layers.Contains(c))
            {
                layers.Add(c);
                SmoothPaint(c);
                c.Paint += PaintLayer;
            }
            Collect(c);
        }
    }

    static void SmoothPaint(Control c)
    {
        try { typeof(Control).GetProperty("DoubleBuffered", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(c, true, null); } catch { }
    }

    void Create()
    {
        var img = FaceImage.Get();
        if (img == null) return;
        for (int i = 0; i < Count; i++)
        {
            var f = new Face
            {
                Size = 46 + (float)rnd.NextDouble() * 84,
                Vy = 0.35f + (float)rnd.NextDouble() * 0.9f,
                Phase = (float)(rnd.NextDouble() * Math.PI * 2),
                Swing = 10 + (float)rnd.NextDouble() * 26,
                Spin = ((float)rnd.NextDouble() - 0.5f) * 1.2f,
            };
            f.X = (float)rnd.NextDouble() * Math.Max(400, form.ClientSize.Width);
            f.Y = (float)rnd.NextDouble() * Math.Max(300, form.ClientSize.Height);
            int alpha = 80 + rnd.Next(70);                     // 30-58 % opacity - well visible, but still behind everything
            f.Sprite = MakeSprite(img, f.Size, alpha);
            faces.Add(f);
        }
    }

    static Bitmap MakeSprite(Bitmap img, float size, int alpha)
    {
        int h = (int)size, w = Math.Max(1, (int)(size * img.Width / img.Height));
        var b = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(b))
        using (var ia = new ImageAttributes())
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            var cm = new ColorMatrix { Matrix33 = alpha / 255f };
            ia.SetColorMatrix(cm);
            g.DrawImage(img, new Rectangle(0, 0, w, h), 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, ia);
        }
        return b;
    }

    void Step()
    {
        if (!form.Visible || form.WindowState == FormWindowState.Minimized) return;
        t += 0.04f;
        int W = form.ClientSize.Width, H = form.ClientSize.Height;
        foreach (var f in faces)
        {
            f.Y -= f.Vy;
            f.Angle += f.Spin;
            if (f.Y < -f.Size * 1.5f) { f.Y = H + f.Size; f.X = (float)rnd.NextDouble() * W; }
        }
        foreach (var c in layers) if (c.Visible && !c.IsDisposed) c.Invalidate(false);
        foreach (var c in corners) if (c.Visible && !c.IsDisposed) InvalidateCorners(c);
    }

    static void InvalidateCorners(Control c)
    {
        int r = Math.Min(Math.Max(16, c.Height / 2 + 1), Math.Min(c.Width, c.Height));
        c.Invalidate(new Rectangle(0, 0, r, r), false); c.Invalidate(new Rectangle(c.Width - r, 0, r, r), false);
        c.Invalidate(new Rectangle(0, c.Height - r, r, r), false); c.Invalidate(new Rectangle(c.Width - r, c.Height - r, r, r), false);
    }

    void PaintLayer(object sender, PaintEventArgs e) { DrawOn((Control)sender, e.Graphics); }

    // draws the faces that overlap control c (its position in the window decides which part is visible)
    void DrawOn(Control c, Graphics g)
    {
        if (!enabled || faces.Count == 0) return;
        Point off;
        try { off = form.PointToClient(c.PointToScreen(Point.Empty)); } catch { return; }
        g.InterpolationMode = InterpolationMode.Bilinear;
        foreach (var f in faces)
        {
            float x = f.X + (float)Math.Sin(t * 1.3 + f.Phase) * f.Swing - off.X;
            float y = f.Y - off.Y;
            float r = f.Size;   // rough bounds to skip faces that are not on this panel
            if (x + r < 0 || y + r < 0 || x - r > c.Width || y - r > c.Height) continue;
            var st = g.Save();
            g.TranslateTransform(x, y);
            g.RotateTransform(f.Angle + (float)Math.Sin(t * 2 + f.Phase) * 8);
            g.DrawImage(f.Sprite, -f.Sprite.Width / 2f, -f.Sprite.Height / 2f, f.Sprite.Width, f.Sprite.Height);
            g.Restore(st);
        }
    }

    public void Dispose()
    {
        Theme.Backdrop = null;
        timer.Dispose();
        foreach (var f in faces) f.Sprite.Dispose();
    }
}
