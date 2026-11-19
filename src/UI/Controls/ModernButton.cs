// Flat rounded button with an optional icon. "Ghost" buttons are dark with a light border.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

public class ModernButton : Button
{
    public Color Fill;
    public bool Ghost;
    public IconKind? Icon;
    public float Radius = 10;
    bool hover, down;

    public ModernButton(string text, Color fill, IconKind? icon = null)
    {
        Text = text; Fill = fill; Icon = icon;
        FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
        FlatAppearance.MouseOverBackColor = Color.Transparent; FlatAppearance.MouseDownBackColor = Color.Transparent;
        ForeColor = Color.White; Cursor = Cursors.Hand; Font = Theme.F(10f, true);
        Height = 38;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
    }

    public static ModernButton GhostButton(string text, IconKind? icon = null)
    {
        return new ModernButton(text, Theme.Card2, icon) { Ghost = true, ForeColor = Theme.Fg };
    }

    public void Set(string text, Color fill, IconKind? icon)
    {
        if (text == Text && fill == Fill && icon == Icon) return;
        Text = text; Fill = fill; Icon = icon; Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        Theme.PaintParentBg(this, g, Theme.Bg);
        var c = Fill;
        if (hover) c = Theme.Mix(c, Color.White, Ghost ? 0.05f : 0.12f);
        if (down) c = Theme.Mix(c, Color.Black, 0.15f);
        if (!Enabled) c = Theme.Card2;
        var r = new RectangleF(0, 0, Width - 1, Height - 1);
        Theme.FillRound(g, r, Math.Min(Radius, Height / 2f), c);
        if (Ghost)
            using (var path = Theme.Round(new RectangleF(0.5f, 0.5f, Width - 2, Height - 2), Math.Min(Radius, Height / 2f)))
            using (var pen = new Pen(hover ? Theme.Dim : Theme.Line)) g.DrawPath(pen, path);

        var fg = Enabled ? ForeColor : Theme.Dim;
        var size = TextRenderer.MeasureText(g, Text, Font, Size.Empty, TextFormatFlags.NoPadding);
        int icon = Icon != null ? (int)(Font.Size * 1.55f) : 0, gap = Icon != null && Text.Length > 0 ? 8 : 0;
        int total = icon + gap + size.Width, x = (Width - total) / 2;
        if (Icon != null) Icons.Draw(g, Icon.Value, x, (Height - icon) / 2f, icon, fg, 1.8f);
        TextRenderer.DrawText(g, Text, Font, new Rectangle(x + icon + gap, 0, size.Width + 4, Height), fg, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}
