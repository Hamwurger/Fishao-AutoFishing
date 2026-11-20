// The red cross-hair with a number that marks a cast point on a game picture.
using System.Drawing;
using System.Drawing.Drawing2D;

public static class CastMarker
{
    // the cast area: see-through blue fill + outline (w = line width in picture pixels)
    public static void DrawArea(Graphics g, Point[] area, float w)
    {
        if (area == null || area.Length < 3) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var fill = new SolidBrush(Color.FromArgb(70, 90, 190, 255))) g.FillPolygon(fill, area);
        using (var pen = new Pen(Color.FromArgb(230, 90, 190, 255), w) { LineJoin = LineJoin.Round }) g.DrawPolygon(pen, area);
    }

    // w = line width in picture pixels
    public static void Draw(Graphics g, Point p, float w, int number)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var pen = new Pen(Color.FromArgb(255, 70, 70), w))
        {
            g.DrawEllipse(pen, p.X - 14, p.Y - 14, 28, 28);
            g.DrawLine(pen, p.X - 22, p.Y, p.X - 8, p.Y); g.DrawLine(pen, p.X + 8, p.Y, p.X + 22, p.Y);
            g.DrawLine(pen, p.X, p.Y - 22, p.X, p.Y - 8); g.DrawLine(pen, p.X, p.Y + 8, p.X, p.Y + 22);
        }
        if (number <= 0) return;
        using (var f = new Font("Segoe UI", 8f * w / 1.5f, FontStyle.Bold))
        {
            var s = number.ToString(); var sz = g.MeasureString(s, f);
            var r = new RectangleF(p.X + 14, p.Y - 14 - sz.Height, sz.Width + 2, sz.Height);
            using (var path = Theme.Round(r, sz.Height / 3)) using (var bg = new SolidBrush(Color.FromArgb(220, 200, 40, 40))) g.FillPath(bg, path);
            g.DrawString(s, f, Brushes.White, r.X + 1, r.Y);
        }
    }
}
