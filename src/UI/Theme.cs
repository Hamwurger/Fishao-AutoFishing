// Colours, fonts and small drawing helpers shared by the whole window.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

public static class Theme
{
    // background layers (darkest -> lightest)
    public static readonly Color Bg = Color.FromArgb(11, 14, 20);
    public static readonly Color Side = Color.FromArgb(15, 19, 27);
    public static readonly Color Card = Color.FromArgb(20, 26, 36);
    public static readonly Color Card2 = Color.FromArgb(28, 35, 48);
    public static readonly Color Hover = Color.FromArgb(34, 43, 58);
    public static readonly Color Line = Color.FromArgb(36, 45, 60);

    // text
    public static readonly Color Fg = Color.FromArgb(236, 241, 247);
    public static readonly Color Muted = Color.FromArgb(132, 146, 166);
    public static readonly Color Dim = Color.FromArgb(82, 95, 114);

    // accents
    public static readonly Color Accent = Color.FromArgb(45, 212, 160);
    public static readonly Color Blue = Color.FromArgb(76, 141, 255);
    public static readonly Color Violet = Color.FromArgb(150, 120, 255);
    public static readonly Color Danger = Color.FromArgb(244, 92, 92);
    public static readonly Color Gold = Color.FromArgb(255, 196, 46);
    public static readonly Color Orange = Color.FromArgb(255, 145, 64);

    // Things drawn on the window background (the floating faces). Set by FloatingFaces, null when they are off.
    public static Action<Control, Graphics> Backdrop;

    // Fills a control with its parent's colour - and, when that is the window background, draws the floating
    // faces into it too, so rounded corners of cards/buttons show the faces behind them instead of a plain square.
    public static void PaintParentBg(Control c, Graphics g, Color fallback)
    {
        var bg = c.Parent != null ? c.Parent.BackColor : fallback;
        g.Clear(bg);   // Clear fills every pixel fully (an anti-aliased fill would leave the edge pixels half done)
        if (Backdrop != null && (bg == Bg || bg == Side)) Backdrop(c, g);
    }

    public static Font F(float size, bool bold = false) { return new Font(bold ? "Segoe UI Semibold" : "Segoe UI", size); }
    public static Font Mono(float size)
    {
        var f = new Font("Cascadia Mono", size);
        if (f.Name != "Cascadia Mono") { f.Dispose(); f = new Font("Consolas", size); }
        return f;
    }

    // colour mixed with another one (t = 0..1)
    public static Color Mix(Color a, Color b, float t)
    {
        return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
    }
    public static Color Alpha(Color c, int a) { return Color.FromArgb(a, c); }

    public static GraphicsPath Round(RectangleF r, float rad)
    {
        var p = new GraphicsPath();
        rad = Math.Min(rad, Math.Min(r.Width, r.Height) / 2f);   // a "pill" when the radius is half the height
        if (rad <= 0.5f) { p.AddRectangle(r); return p; }
        float d = rad * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure(); return p;
    }

    public static void FillRound(Graphics g, RectangleF r, float rad, Color c)
    {
        using (var path = Round(r, rad)) using (var b = new SolidBrush(c)) g.FillPath(b, path);
    }

    public static void Star(Graphics g, float cx, float cy, float rad, bool filled)
    {
        var pts = new PointF[10];
        for (int i = 0; i < 10; i++)
        {
            double a = -Math.PI / 2 + i * Math.PI / 5; float rr = i % 2 == 0 ? rad : rad * 0.45f;
            pts[i] = new PointF(cx + (float)(Math.Cos(a) * rr), cy + (float)(Math.Sin(a) * rr));
        }
        using (var b = new SolidBrush(filled ? Gold : Color.FromArgb(48, 58, 74))) g.FillPolygon(b, pts);
    }

    public static void Stars(Graphics g, float x, float cy, float rad, int n)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        for (int i = 0; i < 5; i++) Star(g, x + rad + i * rad * 2.3f, cy, rad, i < n);
    }
}
