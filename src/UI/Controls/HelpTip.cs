// The "?" bubble next to a setting: hovering it shows a longer explanation in a dark tooltip.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

public static class HelpTip
{
    const int MaxW = 360, Pad = 12;
    static ToolTip tip;
    static Control shownOn;

    static ToolTip Tip
    {
        get
        {
            if (tip != null) return tip;
            tip = new ToolTip { OwnerDraw = true, UseAnimation = false, UseFading = false };
            tip.Popup += (s, e) =>
            {
                var sz = TextRenderer.MeasureText(current, Theme.F(9.5f),
                                                  new Size(MaxW - Pad * 2, 0), TextFormatFlags.WordBreak);
                e.ToolTipSize = new Size(Math.Min(MaxW, sz.Width + Pad * 2 + 2), sz.Height + Pad * 2);
            };
            tip.Draw += (s, e) =>
            {
                var g = e.Graphics;
                using (var bg = new SolidBrush(Theme.Card2)) g.FillRectangle(bg, e.Bounds);
                using (var pen = new Pen(Theme.Line)) g.DrawRectangle(pen, 0, 0, e.Bounds.Width - 1, e.Bounds.Height - 1);
                using (var bar = new SolidBrush(Theme.Blue)) g.FillRectangle(bar, 0, 0, 3, e.Bounds.Height);
                TextRenderer.DrawText(g, e.ToolTipText, Theme.F(9.5f), new Rectangle(Pad, Pad, e.Bounds.Width - Pad * 2, e.Bounds.Height - Pad * 2),
                                      Theme.Fg, TextFormatFlags.WordBreak);
            };
            return tip;
        }
    }
    static string current = "";

    public static void Show(Control c, string text, Point below)
    {
        if (shownOn == c && current == text) return;
        current = text; shownOn = c;
        Tip.Show(text, c, below.X, below.Y, 60000);
    }

    public static void Hide(Control c)
    {
        if (shownOn != c || tip == null) return;
        tip.Hide(c); shownOn = null; current = "";
    }

    // small circled "?" - returns its rectangle
    public static Rectangle DrawIcon(Graphics g, int x, int centerY, bool hover)
    {
        var r = new Rectangle(x, centerY - 8, 16, 16);
        var old = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var b = new SolidBrush(hover ? Theme.Blue : Theme.Card2)) g.FillEllipse(b, r);
        using (var p = new Pen(hover ? Theme.Blue : Theme.Line)) g.DrawEllipse(p, r);
        g.SmoothingMode = old;
        using (var f = Theme.F(8f, true))
            TextRenderer.DrawText(g, "?", f, r, hover ? Color.White : Theme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        return r;
    }
}
