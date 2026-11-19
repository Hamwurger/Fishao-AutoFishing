// How many fish had 5, 4, 3, 2, 1 stars - five horizontal bars.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

public class StarBars : Control
{
    public int[] Counts = new int[6];

    public StarBars()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Height = 150;
    }

    public void SetCounts(int[] c) { Counts = c; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Theme.Card);   // Clear fills every pixel fully (an anti-aliased fill would leave the edge pixels half done)
        int max = 1, sum = 0; for (int i = 1; i <= 5; i++) { max = Math.Max(max, Counts[i]); sum += Counts[i]; }
        int rowH = Height / 5, barX = 100, barW = Width - barX - 90;
        for (int s = 5; s >= 1; s--)
        {
            int y = (5 - s) * rowH, cy = y + rowH / 2;
            for (int i = 0; i < 5; i++) Theme.Star(g, 8 + i * 17, cy, 7, i < s);
            Theme.FillRound(g, new RectangleF(barX, cy - 5, barW, 10), 5, Theme.Card2);
            float w = Counts[s] == 0 ? 0 : Math.Max(10, barW * Counts[s] / (float)max);
            if (w > 0)
                using (var path = Theme.Round(new RectangleF(barX, cy - 5, w, 10), 5))
                using (var b = new LinearGradientBrush(new RectangleF(barX, 0, barW + 1, 10), Theme.Orange, Theme.Gold, 0f)) g.FillPath(b, path);
            using (var f = Theme.F(10f, true)) TextRenderer.DrawText(g, Counts[s].ToString(), f, new Rectangle(Width - 84, y, 36, rowH), Theme.Fg, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            string pct = sum == 0 ? "" : (100 * Counts[s] / sum) + " %";
            using (var f = Theme.F(8.5f)) TextRenderer.DrawText(g, pct, f, new Rectangle(Width - 46, y, 46, rowH), Theme.Muted, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        }
    }
}
