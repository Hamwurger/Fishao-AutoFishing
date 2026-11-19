// Rounded panel with an optional title (and icon) at the top.
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

public class Card : Panel
{
    public string Title;
    public string Hint;       // small grey text on the right side of the title
    public IconKind? Icon;
    public Color IconColor = Theme.Muted;

    public Card(string title, IconKind? icon = null)
    {
        Title = title; Icon = icon;
        DoubleBuffered = true; ResizeRedraw = true;
        BackColor = Theme.Card; ForeColor = Theme.Fg;
        Padding = new Padding(18, title == null ? 16 : 50, 18, 16);
        Margin = new Padding(0);
    }

    public void SetHint(string hint) { if (hint != Hint) { Hint = hint; Invalidate(new Rectangle(0, 0, Width, 48)); } }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        Theme.PaintParentBg(this, g, Theme.Bg);
        Theme.FillRound(g, new RectangleF(0, 0, Width - 1, Height - 1), 14, Theme.Card);
        using (var path = Theme.Round(new RectangleF(0.5f, 0.5f, Width - 2, Height - 2), 14))
        using (var pen = new Pen(Theme.Alpha(Theme.Line, 150))) g.DrawPath(pen, path);
        if (Title == null) return;
        int x = 18;
        if (Icon != null) { Icons.Draw(g, Icon.Value, 18, 17, 17, IconColor, 1.6f); x = 44; }
        using (var f = Theme.F(10.5f, true)) TextRenderer.DrawText(g, Title, f, new Point(x - 2, 15), Theme.Fg);
        if (!string.IsNullOrEmpty(Hint))
            using (var f = Theme.F(9f)) TextRenderer.DrawText(g, Hint, f, new Rectangle(0, 16, Width - 18, 22), Theme.Muted, TextFormatFlags.Right);
    }
}
