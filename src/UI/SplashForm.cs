// Fun "loading" screen shown for ~17 s before the main window. Click, Esc, Enter or Space skips it.
// A little scene: the face bobs in the water, fish swim and jump, bubbles rise. On the way the face spins,
// jumps out of the water, a fish steals part of the progress bar, the bar "gets stuck" at 99 %, and at the end
// a hook comes down and pulls the face out. The picture is Resources\splash.png, built into the exe.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;

public class SplashForm : Form
{
    // ---- timeline (milliseconds)
    const int DurationMs = 15000;                 // the bar reaches 100 % here
    const int FinaleMs = 2200;                    // then the hook pulls the face out, then the bot opens
    const double SpinAt = 4.0, SpinLen = 0.9;     // the face turns around once
    const double TheftAt = 8.0;                   // a fish steals part of the bar
    const double JumpAt = 10.2, JumpLen = 1.0;    // the face jumps out of the water
    const double StuckAt = 13.0;                  // "stuck" at 99 %
    const float WaterY = 334;                     // water line

    readonly Bitmap picture;
    readonly Timer timer = new Timer { Interval = 16 };
    readonly DateTime start = DateTime.Now;
    readonly string[] messages;
    readonly Random rnd = new Random();
    double lastT;
    bool splashedIn, splashedOut;

    class Particle { public float X, Y, Vx, Vy, R, Life, Age; public bool Drop; }
    readonly List<Particle> parts = new List<Particle>();

    // swimming fish: speed (px/s, negative = to the left), depth, colour, phase of its jumps
    static readonly float[] FishSpeed = { 70, -55, 95 };
    static readonly float[] FishDepth = { 16, 34, 52 };
    static readonly Color[] FishColor = { Color.FromArgb(255, 150, 60), Color.FromArgb(80, 200, 255), Color.FromArgb(255, 205, 70) };

    public SplashForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(460, 540);
        BackColor = Theme.Bg; ShowInTaskbar = true; Text = "Fishao Bot";
        DoubleBuffered = true; KeyPreview = true;
        try { Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location); } catch { }

        messages = L.English
            ? new[] { "Untangling the fishing line…", "Digging up worms…", "Teaching fish to bite…", "Bribing the lake…", "Checking the float…", "Counting the fish in the lake…", "Polishing the hooks…", "Asking the fish nicely…", "Feeding the ducks…", "Waiting for the weather…", "Loading even more fish…", "Calibrating the worm…" }
            : new[] { "Rozmotávám vlasec…", "Vykopávám žížaly…", "Učím ryby zabírat…", "Podplácím rybník…", "Kontroluji splávek…", "Počítám ryby v rybníku…", "Leštím háčky…", "Slušně prosím ryby…", "Krmím kachny…", "Čekám na lepší počasí…", "Načítám ještě víc ryb…", "Kalibruji žížalu…" };

        picture = FaceImage.Get();
        timer.Tick += delegate
        {
            double ms = (DateTime.Now - start).TotalMilliseconds;
            if (ms >= DurationMs + FinaleMs) { timer.Stop(); Close(); return; }
            Step(ms / 1000.0);
            Invalidate();
        };
        Click += delegate { Close(); };
        // only these keys skip - so screenshot keys (PrintScreen, Win+Shift+S) don't close it
        KeyDown += delegate (object s, KeyEventArgs e) { if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) Close(); };
    }

    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); NativeLook.DarkTitleBar(this); } // rounded corners on Win 11
    protected override void OnShown(EventArgs e) { base.OnShown(e); timer.Start(); }

    // ------------------------------------------------------------------ progress with a story
    static float Progress(double t)
    {
        if (t < TheftAt) return (float)(0.60 * t / TheftAt);
        if (t < TheftAt + 0.6) { double u = (t - TheftAt) / 0.6; return (float)(0.60 - 0.22 * (1 - Math.Pow(1 - u, 3))); }
        if (t < StuckAt) return (float)(0.38 + 0.61 * (t - TheftAt - 0.6) / (StuckAt - TheftAt - 0.6));
        if (t < DurationMs / 1000.0) return 0.99f;
        return 1f;
    }

    string MessageAt(double t)
    {
        double end = DurationMs / 1000.0;
        if (t >= end) return L.T("Hotovo! Jdeme na ryby!", "Done! Let's go fishing!");
        if (t >= StuckAt) return L.T("Ještě kousek… 99 %… věř mi…", "Almost… 99 %… trust me…");
        if (t >= TheftAt && t < TheftAt + 1.8) return L.T("Ups! Ryba utekla i s kusem načítání…", "Oops! A fish ran off with part of the loading bar…");
        if (t >= JumpAt && t < JumpAt + JumpLen + 0.3) return L.T("Jůůů!", "Wheee!");
        if (t >= SpinAt && t < SpinAt + SpinLen + 0.2) return L.T("Točí se mi hlava…", "I'm getting dizzy…");
        return messages[(int)(t / 1.1) % messages.Length];
    }

    // ------------------------------------------------------------------ face position
    // returns centre offset (dx, dy), rotation and scale of the face at time t
    void FacePose(double t, out float dx, out float dy, out float ang, out float scale)
    {
        dx = 0; dy = (float)Math.Sin(t * 3.2) * 10; ang = (float)Math.Sin(t * 2.1) * 6;
        double pop = Math.Min(1, t / 0.35); scale = (float)(1 - (1 - pop) * (1 - pop));   // grows in at the start

        if (t >= SpinAt && t < SpinAt + SpinLen)
        {
            double u = (t - SpinAt) / SpinLen;
            ang += (float)(360 * (u < 0.5 ? 2 * u * u : 1 - Math.Pow(-2 * u + 2, 2) / 2));   // ease in-out full turn
        }
        if (t >= TheftAt && t < TheftAt + 0.8)                   // shocked shaking
            dx = (float)Math.Sin(t * 90) * 6 * (float)(1 - (t - TheftAt) / 0.8);
        if (t >= JumpAt && t < JumpAt + JumpLen)
        {
            double u = (t - JumpAt) / JumpLen;
            dy -= (float)(150 * Math.Sin(Math.PI * u));
            ang += (float)(Math.Sin(Math.PI * 2 * u) * 15);
        }
        double end = DurationMs / 1000.0;
        if (t >= end + 0.6)                                      // pulled up by the hook
        {
            double u = Math.Min(1, (t - end - 0.6) / 1.1);
            dy -= (float)(560 * u * u);
            ang = (float)(Math.Sin(t * 14) * 10);
        }
    }

    // ------------------------------------------------------------------ particles (bubbles, splashes)
    void Step(double t)
    {
        float dt = (float)Math.Max(0, Math.Min(0.05, t - lastT)); lastT = t;
        int W = ClientSize.Width, H = ClientSize.Height;

        // bubbles rise from the bottom
        if (rnd.NextDouble() < dt * 7)
            parts.Add(new Particle { X = 20 + (float)rnd.NextDouble() * (W - 40), Y = H + 4, Vy = -(25 + (float)rnd.NextDouble() * 35), R = 2 + (float)rnd.NextDouble() * 4, Life = 6 });

        // splashes when the face leaves and hits the water
        if (!splashedOut && t >= JumpAt) { splashedOut = true; Splash(W / 2f, 18); }
        if (!splashedIn && t >= JumpAt + JumpLen * 0.92) { splashedIn = true; Splash(W / 2f, 26); }

        for (int i = parts.Count - 1; i >= 0; i--)
        {
            var p = parts[i];
            p.Age += dt;
            if (p.Drop) { p.Vy += 520 * dt; p.X += p.Vx * dt; p.Y += p.Vy * dt; }
            else { p.Y += p.Vy * dt; p.X += (float)Math.Sin(p.Age * 3 + p.R) * 12 * dt; }
            bool gone = p.Age > p.Life || (p.Drop && p.Y > WaterY + 30) || (!p.Drop && p.Y < WaterY + 6);
            if (gone) parts.RemoveAt(i);
        }
    }

    void Splash(float x, int n)
    {
        for (int i = 0; i < n; i++)
        {
            double a = Math.PI * (0.15 + 0.7 * rnd.NextDouble());   // upwards fan
            float v = 140 + (float)rnd.NextDouble() * 200;
            parts.Add(new Particle { Drop = true, X = x + ((float)rnd.NextDouble() - 0.5f) * 90, Y = WaterY, Vx = (float)Math.Cos(a) * v * (rnd.Next(2) == 0 ? 1 : -1), Vy = -(float)Math.Sin(a) * v, R = 2 + (float)rnd.NextDouble() * 3, Life = 2 });
        }
    }

    // ------------------------------------------------------------------ drawing
    static void DrawFish(Graphics g, float x, float y, float size, bool left, Color c)
    {
        var st = g.Save();
        g.TranslateTransform(x, y);
        if (left) g.ScaleTransform(-1, 1);
        using (var b = new SolidBrush(c))
        {
            g.FillEllipse(b, -size, -size * 0.45f, size * 2, size * 0.9f);
            g.FillPolygon(b, new[] { new PointF(-size * 0.8f, 0), new PointF(-size * 1.6f, -size * 0.55f), new PointF(-size * 1.6f, size * 0.55f) });
        }
        using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, size * 0.35f, -size * 0.25f, size * 0.36f, size * 0.36f);
        using (var b = new SolidBrush(Color.FromArgb(20, 25, 35))) g.FillEllipse(b, size * 0.45f, -size * 0.17f, size * 0.18f, size * 0.18f);
        g.Restore(st);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        double ms = (DateTime.Now - start).TotalMilliseconds, t = ms / 1000.0, end = DurationMs / 1000.0;
        float p = Progress(t);
        int W = ClientSize.Width, H = ClientSize.Height;

        // background: dark sky, water below the line
        using (var b = new LinearGradientBrush(new Rectangle(0, 0, W, H), Theme.Side, Theme.Mix(Theme.Bg, Theme.Blue, 0.18f), 90f)) g.FillRectangle(b, 0, 0, W, H);
        using (var b = new SolidBrush(Theme.Alpha(Theme.Blue, 22))) g.FillRectangle(b, 0, WaterY, W, H - WaterY);
        using (var pen = new Pen(Theme.Line)) g.DrawRectangle(pen, 0, 0, W - 1, H - 1);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // bubbles under the water
        foreach (var q in parts)
            if (!q.Drop)
                using (var pen = new Pen(Theme.Alpha(Color.FromArgb(150, 210, 255), (int)(110 * (1 - q.Age / q.Life))), 1.3f))
                    g.DrawEllipse(pen, q.X - q.R, q.Y - q.R, q.R * 2, q.R * 2);

        // swimming fish (each jumps out every few seconds)
        for (int i = 0; i < FishSpeed.Length; i++)
        {
            float span = W + 100, x = (float)((FishSpeed[i] * t + i * 157) % span); if (x < 0) x += span; x -= 50;
            bool left = FishSpeed[i] < 0;
            float y = WaterY + FishDepth[i] + (float)Math.Sin(t * 2 + i) * 3;
            double jt = (t + i * 1.7) % 4.5;                       // jump phase
            if (jt < 0.8) y -= (float)(70 * Math.Sin(Math.PI * jt / 0.8)) + FishDepth[i] * (float)Math.Sin(Math.PI * jt / 0.8);
            DrawFish(g, x, y, 11, left, Theme.Alpha(FishColor[i], 210));
        }

        // the thief: a fish swims away with the stolen piece of the bar
        var bar = new RectangleF(60, 480, W - 120, 8);
        if (t >= TheftAt && t < TheftAt + 1.6)
        {
            double u = (t - TheftAt) / 1.6;
            float fx = bar.X + bar.Width * 0.6f + (float)(u * u * 320), fy = bar.Y + 4 + (float)Math.Sin(t * 16) * 3;
            using (var path = Theme.Round(new RectangleF(fx - 6 - bar.Width * 0.22f, fy - 4, bar.Width * 0.22f, 8), 4))
            using (var b = new LinearGradientBrush(new RectangleF(fx - 120, 0, 130, 10), Theme.Accent, Theme.Blue, 0f)) g.FillPath(b, path);
            DrawFish(g, fx + 8, fy, 12, false, FishColor[0]);
        }

        float fdx, fdy, fang, fscale; FacePose(t, out fdx, out fdy, out fang, out fscale);
        float faceH = 250, faceTop = 90 + fdy;

        // the face
        if (picture != null && faceTop < H)
        {
            float w = faceH * picture.Width / picture.Height;
            var st = g.Save();
            g.TranslateTransform(W / 2f + fdx, 90 + faceH / 2 + fdy);
            g.RotateTransform(fang); g.ScaleTransform(fscale, fscale);
            g.DrawImage(picture, -w / 2, -faceH / 2, w, faceH);
            g.Restore(st);
            // shadow on the water - smaller when the face is high up
            float lift = Math.Max(0, -fdy - 10), sw = Math.Max(20, 120 - lift * 0.6f);
            using (var b = new SolidBrush(Theme.Alpha(Color.Black, (int)Math.Max(15, 70 - lift * 0.3f)))) g.FillEllipse(b, W / 2f - sw / 2, 356, sw, 14);
        }

        // hook and line coming down at the end (in front of the face - it holds it by the hair)
        if (t >= end)
        {
            double u = Math.Min(1, (t - end) / 0.6);
            float hookY = t < end + 0.6 ? (float)(-30 + (faceTop + 26 + 30) * (1 - Math.Pow(1 - u, 3))) : faceTop + 26;
            float hx = W / 2f;
            using (var pen = new Pen(Color.FromArgb(200, 220, 230, 240), 1.5f)) g.DrawLine(pen, hx, 0, hx, hookY - 10);
            using (var pen = new Pen(Color.FromArgb(220, 200, 205, 215), 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(pen, hx, hookY - 12, hx, hookY + 4);
                g.DrawArc(pen, hx - 12, hookY - 6, 12, 14, 0, 180);
            }
        }

        // waves over everything in the water line
        for (int k = 0; k < 3; k++)
        {
            var pts = new PointF[W / 8 + 2];
            float baseY = WaterY - 4 + k * 12;
            for (int i = 0; i < pts.Length; i++)
            {
                float x = i * 8;
                pts[i] = new PointF(x, baseY + (float)Math.Sin(x / 38.0 + t * 2.4 + k * 1.3) * (5 - k));
            }
            using (var pen = new Pen(Theme.Alpha(Theme.Blue, 70 - k * 18), 2f)) g.DrawCurve(pen, pts);
        }

        // splash drops
        foreach (var q in parts)
            if (q.Drop)
                using (var b = new SolidBrush(Theme.Alpha(Color.FromArgb(150, 210, 255), (int)(220 * (1 - q.Age / q.Life)))))
                    g.FillEllipse(b, q.X - q.R, q.Y - q.R, q.R * 2, q.R * 2);

        // texts
        using (var f = Theme.F(20f, true)) TextRenderer.DrawText(g, "Fishao Bot", f, new Rectangle(0, 392, W, 40), Theme.Fg, TextFormatFlags.HorizontalCenter);
        using (var f = Theme.F(10.5f)) TextRenderer.DrawText(g, MessageAt(t), f, new Rectangle(0, 434, W, 26), Theme.Muted, TextFormatFlags.HorizontalCenter);

        // progress bar (shakes while "stuck" at 99 %)
        float shake = t >= StuckAt && t < end ? (float)Math.Sin(t * 40) * 2 : 0;
        var barNow = new RectangleF(bar.X + shake, bar.Y, bar.Width, bar.Height);
        Theme.FillRound(g, barNow, 4, Theme.Card2);
        if (p > 0.01f)
            using (var path = Theme.Round(new RectangleF(barNow.X, barNow.Y, barNow.Width * p, barNow.Height), 4))
            using (var b = new LinearGradientBrush(barNow, Theme.Accent, Theme.Blue, 0f)) g.FillPath(b, path);
        using (var f = Theme.F(8.5f)) TextRenderer.DrawText(g, (int)(p * 100) + " %", f, new Rectangle((int)shake, 496, W, 20), Theme.Dim, TextFormatFlags.HorizontalCenter);
        using (var f = Theme.F(8f)) TextRenderer.DrawText(g, L.T("klikni pro přeskočení", "click to skip"), f, new Rectangle(0, H - 24, W - 12, 20), Theme.Dim, TextFormatFlags.Right);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) timer.Dispose();   // the picture is shared (FaceImage) - not disposed here
        base.Dispose(disposing);
    }
}
