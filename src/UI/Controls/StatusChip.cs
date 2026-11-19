// Rounded "● text" badge showing what the bot is doing. The dot pulses while the bot runs.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

public class StatusChip : Control
{
    public Color Dot = Theme.Muted;
    public bool Pulse;
    int phase;

    public StatusChip()
    {
        Height = 38; Font = Theme.F(10f, true);
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
    }

    public void Set(string text, Color dot, bool pulse)
    {
        if (pulse) phase++;
        if (text == Text && dot == Dot && pulse == Pulse && !pulse) return;
        Text = text; Dot = dot; Pulse = pulse; Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        Theme.PaintParentBg(this, g, Theme.Bg);
        var r = new RectangleF(0, 0, Width - 1, Height - 1);
        Theme.FillRound(g, r, Height / 2f, Theme.Card);
        using (var path = Theme.Round(new RectangleF(0.5f, 0.5f, Width - 2, Height - 2), Height / 2f)) using (var pen = new Pen(Theme.Line)) g.DrawPath(pen, path);
        float cx = 20, cy = Height / 2f;
        if (Pulse)
        {
            float t = (phase % 8) / 8f;     // 0..1 every 2 s (refresh is 4x a second)
            float rad = 5 + t * 6;
            using (var b = new SolidBrush(Theme.Alpha(Dot, (int)(110 * (1 - t))))) g.FillEllipse(b, cx - rad, cy - rad, rad * 2, rad * 2);
        }
        using (var b = new SolidBrush(Dot)) g.FillEllipse(b, cx - 5, cy - 5, 10, 10);
        TextRenderer.DrawText(g, Text, Font, new Rectangle(36, 0, Width - 50, Height), Theme.Fg, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}
