// On/off switch with a label on the left (replaces the old check box).
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

public class Toggle : Control
{
    bool on;
    public string Description = "";
    public event EventHandler CheckedChanged;

    public Toggle(string text, string description = "")
    {
        Text = text; Description = description;
        Height = description.Length > 0 ? 52 : 36; Cursor = Cursors.Hand;
        Font = Theme.F(10f);
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
    }

    public bool Checked
    {
        get { return on; }
        set { if (on != value) { on = value; Invalidate(); if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty); } }
    }

    protected override void OnClick(EventArgs e) { Checked = !Checked; base.OnClick(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        Theme.PaintParentBg(this, g, Theme.Card);
        int sw = 44, sh = 24, sx = Width - sw - 2, sy = (Height - sh) / 2;
        Theme.FillRound(g, new RectangleF(sx, sy, sw, sh), sh / 2f, on ? Theme.Accent : Theme.Card2);
        if (!on) using (var path = Theme.Round(new RectangleF(sx + 0.5f, sy + 0.5f, sw - 1, sh - 1), sh / 2f)) using (var pen = new Pen(Theme.Line)) g.DrawPath(pen, path);
        int k = sh - 6, kx = on ? sx + sw - k - 3 : sx + 3;
        using (var b = new SolidBrush(on ? Color.White : Theme.Muted)) g.FillEllipse(b, kx, sy + 3, k, k);

        var textR = new Rectangle(0, 0, sx - 12, Description.Length > 0 ? Height / 2 + 2 : Height);
        TextRenderer.DrawText(g, Text, Font, textR, Theme.Fg, TextFormatFlags.EndEllipsis | (Description.Length > 0 ? TextFormatFlags.Bottom : TextFormatFlags.VerticalCenter));
        if (Description.Length > 0)
            using (var f = Theme.F(8.5f)) TextRenderer.DrawText(g, Description, f, new Rectangle(0, Height / 2 + 2, sx - 12, Height / 2 - 2), Theme.Muted, TextFormatFlags.Top | TextFormatFlags.EndEllipsis);
    }
}
