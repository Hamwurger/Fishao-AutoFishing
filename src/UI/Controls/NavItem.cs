// One entry of the side menu (icon + label, highlighted when selected).
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

public class NavItem : Control
{
    public IconKind Icon;
    public string Badge = "";       // small number on the right (e.g. catches)
    bool selected, hover;

    public NavItem(string text, IconKind icon)
    {
        Text = text; Icon = icon;
        Height = 44; Cursor = Cursors.Hand;
        Font = Theme.F(10.5f);
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
    }

    public bool Selected { get { return selected; } set { if (selected != value) { selected = value; Invalidate(); } } }
    public void SetBadge(string b) { if (b != Badge) { Badge = b; Invalidate(); } }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Theme.Side);   // Clear fills every pixel fully (an anti-aliased fill would leave the edge pixels half done)
        if (Theme.Backdrop != null) Theme.Backdrop(this, g);   // floating faces under the icon and text
        var r = new RectangleF(10, 2, Width - 20, Height - 4);
        if (selected) Theme.FillRound(g, r, 10, Theme.Alpha(Theme.Accent, 30));
        else if (hover) Theme.FillRound(g, r, 10, Theme.Card);
        if (selected) Theme.FillRound(g, new RectangleF(0, 12, 4, Height - 24), 2, Theme.Accent);

        var fg = selected ? Theme.Accent : (hover ? Theme.Fg : Theme.Muted);
        Icons.Draw(g, Icon, 24, (Height - 20) / 2f, 20, fg, 1.7f);
        using (var f = selected ? Theme.F(10.5f, true) : Theme.F(10.5f))
            TextRenderer.DrawText(g, Text, f, new Rectangle(56, 0, Width - 100, Height), selected ? Theme.Fg : fg, TextFormatFlags.VerticalCenter);

        if (Badge.Length > 0)
            using (var f = Theme.F(8.5f, true))
            {
                var sz = TextRenderer.MeasureText(Badge, f);
                var br = new RectangleF(Width - 22 - sz.Width, (Height - 20) / 2f, sz.Width + 8, 20);
                Theme.FillRound(g, br, 10, selected ? Theme.Accent : Theme.Card2);
                TextRenderer.DrawText(g, Badge, f, Rectangle.Round(br), selected ? Theme.Bg : Theme.Fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
    }
}
