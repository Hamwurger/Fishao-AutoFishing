// A row of options where exactly one is selected (like iOS segmented control).
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

public class Segmented : Control
{
    readonly string[] items;
    int selected, hover = -1;
    public event EventHandler SelectedChanged;

    public Segmented(params string[] items)
    {
        this.items = items;
        Height = 34; Font = Theme.F(9.5f, true); Cursor = Cursors.Hand;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
    }

    public int Selected
    {
        get { return selected; }
        set { if (value != selected) { selected = value; Invalidate(); if (SelectedChanged != null) SelectedChanged(this, EventArgs.Empty); } }
    }

    int At(Point p) { if (items.Length == 0) return -1; int i = p.X * items.Length / Math.Max(1, Width); return i >= 0 && i < items.Length ? i : -1; }
    protected override void OnMouseMove(MouseEventArgs e) { int h = At(e.Location); if (h != hover) { hover = h; Invalidate(); } base.OnMouseMove(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseClick(MouseEventArgs e) { int i = At(e.Location); if (i >= 0) Selected = i; base.OnMouseClick(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        Theme.PaintParentBg(this, g, Theme.Bg);
        Theme.FillRound(g, new RectangleF(0, 0, Width - 1, Height - 1), 10, Theme.Card2);
        float w = (Width - 6) / (float)items.Length;
        for (int i = 0; i < items.Length; i++)
        {
            var r = new RectangleF(3 + i * w, 3, w, Height - 7);
            if (i == selected) Theme.FillRound(g, r, 8, Theme.Hover);
            TextRenderer.DrawText(g, items[i], Font, Rectangle.Round(r), i == selected ? Theme.Fg : (i == hover ? Theme.Fg : Theme.Muted),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }
}
