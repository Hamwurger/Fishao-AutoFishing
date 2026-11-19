// Simple line icons drawn with GDI+ (no image files needed). Each fits a size x size box at (x, y).
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

public enum IconKind { Home, Fish, Chart, Log, Gear, Play, Stop, Hook, Target, Copy, Trash, Trophy, Globe }

public static class Icons
{
    public static void Draw(Graphics g, IconKind kind, float x, float y, float size, Color color, float stroke = 1.7f)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float s = size / 20f; // icons are designed on a 20x20 grid
        var st = g.Save();
        g.TranslateTransform(x, y); g.ScaleTransform(s, s);
        // the pen is scaled together with the grid, so divide to keep the real line width = stroke pixels
        using (var pen = new Pen(color, stroke / s) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
        using (var brush = new SolidBrush(color))
        {
            switch (kind)
            {
                case IconKind.Home:
                    g.DrawLines(pen, new[] { new PointF(3, 9.5f), new PointF(10, 3.5f), new PointF(17, 9.5f) });
                    g.DrawLines(pen, new[] { new PointF(5, 8), new PointF(5, 17), new PointF(15, 17), new PointF(15, 8) });
                    g.DrawLines(pen, new[] { new PointF(8.5f, 17), new PointF(8.5f, 12), new PointF(11.5f, 12), new PointF(11.5f, 17) });
                    break;
                case IconKind.Fish:
                    using (var p = new GraphicsPath())
                    {
                        p.AddBezier(2.5f, 10, 6, 4.5f, 12, 4.5f, 15, 10);
                        p.AddBezier(15, 10, 12, 15.5f, 6, 15.5f, 2.5f, 10);
                        g.DrawPath(pen, p);
                    }
                    g.DrawLines(pen, new[] { new PointF(15, 10), new PointF(18, 7), new PointF(18, 13), new PointF(15, 10) });
                    g.FillEllipse(brush, 5.6f, 8.2f, 2f, 2f);
                    break;
                case IconKind.Chart:
                    g.DrawLine(pen, 3, 17, 17, 17);
                    g.DrawLine(pen, 5.5f, 17, 5.5f, 11); g.DrawLine(pen, 10, 17, 10, 5); g.DrawLine(pen, 14.5f, 17, 14.5f, 9);
                    break;
                case IconKind.Log:
                    using (var p = Theme.Round(new RectangleF(3.5f, 3, 13, 14), 2.5f)) g.DrawPath(pen, p);
                    g.DrawLine(pen, 7, 7.5f, 13, 7.5f); g.DrawLine(pen, 7, 10.5f, 13, 10.5f); g.DrawLine(pen, 7, 13.5f, 10.5f, 13.5f);
                    break;
                case IconKind.Gear:
                    for (int i = 0; i < 8; i++)
                    {
                        double a = i * Math.PI / 4;
                        g.DrawLine(pen, 10 + (float)Math.Cos(a) * 5.6f, 10 + (float)Math.Sin(a) * 5.6f, 10 + (float)Math.Cos(a) * 7.8f, 10 + (float)Math.Sin(a) * 7.8f);
                    }
                    g.DrawEllipse(pen, 4.6f, 4.6f, 10.8f, 10.8f);
                    g.DrawEllipse(pen, 7.6f, 7.6f, 4.8f, 4.8f);
                    break;
                case IconKind.Play:
                    using (var p = new GraphicsPath())
                    {
                        p.AddPolygon(new[] { new PointF(6, 3.5f), new PointF(16.5f, 10), new PointF(6, 16.5f) });
                        g.FillPath(brush, p); g.DrawPath(pen, p);
                    }
                    break;
                case IconKind.Stop:
                    using (var p = Theme.Round(new RectangleF(4.5f, 4.5f, 11, 11), 2)) { g.FillPath(brush, p); }
                    break;
                case IconKind.Hook:
                    g.DrawLine(pen, 12, 2.5f, 12, 12);
                    g.DrawArc(pen, 5, 8, 7, 9, 0, 180);
                    g.DrawLine(pen, 5, 12.5f, 7.2f, 10.3f);
                    g.DrawEllipse(pen, 10.6f, 1.5f, 2.8f, 2.8f);
                    break;
                case IconKind.Target:
                    g.DrawEllipse(pen, 3.5f, 3.5f, 13, 13); g.FillEllipse(brush, 8.3f, 8.3f, 3.4f, 3.4f);
                    g.DrawLine(pen, 10, 1, 10, 4); g.DrawLine(pen, 10, 16, 10, 19); g.DrawLine(pen, 1, 10, 4, 10); g.DrawLine(pen, 16, 10, 19, 10);
                    break;
                case IconKind.Copy:
                    using (var p = Theme.Round(new RectangleF(7, 7, 10, 10), 2)) g.DrawPath(pen, p);
                    g.DrawLines(pen, new[] { new PointF(13, 4.5f), new PointF(13, 3), new PointF(3, 3), new PointF(3, 13), new PointF(4.5f, 13) });
                    break;
                case IconKind.Trash:
                    g.DrawLine(pen, 3.5f, 5.5f, 16.5f, 5.5f);
                    g.DrawLines(pen, new[] { new PointF(5, 5.5f), new PointF(6, 17), new PointF(14, 17), new PointF(15, 5.5f) });
                    g.DrawLines(pen, new[] { new PointF(8, 5.5f), new PointF(8, 3), new PointF(12, 3), new PointF(12, 5.5f) });
                    break;
                case IconKind.Globe:
                    g.DrawEllipse(pen, 3, 3, 14, 14);
                    g.DrawEllipse(pen, 7, 3, 6, 14);
                    g.DrawLine(pen, 3, 10, 17, 10);
                    break;
                case IconKind.Trophy:
                    g.DrawLines(pen, new[] { new PointF(6, 3), new PointF(14, 3), new PointF(14, 8) });
                    g.DrawArc(pen, 6, 4, 8, 8, 0, 180);
                    g.DrawLine(pen, 6, 3, 6, 8);
                    g.DrawArc(pen, 2.5f, 4, 4, 5, 90, 180); g.DrawArc(pen, 13.5f, 4, 4, 5, 270, 180);
                    g.DrawLine(pen, 10, 12, 10, 15); g.DrawLine(pen, 6.5f, 17, 13.5f, 17);
                    break;
            }
        }
        g.Restore(st);
    }
}
