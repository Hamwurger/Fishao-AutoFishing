// One number tile: coloured icon chip, caption, big value and an optional small line under it.
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

public class Tile : Control
{
    public string Caption, Value = "0", Sub = "";
    public Color Tint;
    public IconKind Icon;

    public Tile(string caption, Color tint, IconKind icon)
    {
        Caption = caption; Tint = tint; Icon = icon;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Height = 104; Margin = new Padding(0);
    }

    public void Set(string v, string sub = "") { if (v != Value || sub != Sub) { Value = v; Sub = sub; Invalidate(); } }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        Theme.PaintParentBg(this, g, Theme.Bg);
        var r = new RectangleF(0, 0, Width - 1, Height - 1);
        using (var path = Theme.Round(r, 14))
        {
            using (var b = new LinearGradientBrush(new RectangleF(0, 0, Width, Height + 1), Theme.Mix(Theme.Card, Tint, 0.07f), Theme.Card, 60f)) g.FillPath(b, path);
            using (var pen = new Pen(Theme.Alpha(Theme.Line, 150))) g.DrawPath(pen, path);
        }
        // icon chip
        Theme.FillRound(g, new RectangleF(16, 16, 34, 34), 10, Theme.Alpha(Tint, 38));
        Icons.Draw(g, Icon, 24, 24, 18, Tint, 1.7f);
        using (var f = Theme.F(9.5f)) TextRenderer.DrawText(g, Caption, f, new Rectangle(60, 16, Width - 70, 34), Theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        using (var f = Theme.F(20f, true)) TextRenderer.DrawText(g, Value, f, new Point(12, 56), Theme.Fg);
        if (Sub.Length > 0)
            using (var f = Theme.F(8.5f)) TextRenderer.DrawText(g, Sub, f, new Rectangle(0, 66, Width - 16, 24), Theme.Muted, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
    }
}
