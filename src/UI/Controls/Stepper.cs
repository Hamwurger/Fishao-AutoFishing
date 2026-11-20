// Number setting:  label (+ grey description) ........  [ − ]  value unit  [ + ]
// Replaces the old NumericUpDown. Mouse wheel over it changes the value too.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

public class Stepper : Control
{
    int value, min, max, step;
    public string Unit, Description;
    public string Help = "";              // longer explanation behind the "?" next to the label
    Rectangle helpRect; bool helpHover;
    public event EventHandler ValueChanged;
    int hoverBtn = 0; // -1 minus, +1 plus
    readonly Timer repeat = new Timer { Interval = 380 };
    int repeatDir = 0;

    const int BtnW = 32, ValW = 74;

    public Stepper(string text, string description, int min, int max, int value, int step, string unit)
    {
        Text = text; Description = description ?? ""; Unit = unit ?? "";
        this.min = min; this.max = max; this.step = step; this.value = Clamp(value);
        Height = Description.Length > 0 ? 52 : 40; Font = Theme.F(10f);
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        repeat.Tick += delegate { repeat.Interval = 70; Value = this.value + repeatDir * this.step; };
    }

    int Clamp(int v) { return Math.Max(min, Math.Min(max, v)); }

    public int Value
    {
        get { return value; }
        set { int v = Clamp(value); if (v != this.value) { this.value = v; Invalidate(); if (ValueChanged != null) ValueChanged(this, EventArgs.Empty); } }
    }

    Rectangle Box { get { int w = BtnW * 2 + ValW; return new Rectangle(Width - w - 1, (Height - 34) / 2, w, 34); } }
    Rectangle Minus { get { var b = Box; return new Rectangle(b.X, b.Y, BtnW, b.Height); } }
    Rectangle Plus { get { var b = Box; return new Rectangle(b.Right - BtnW, b.Y, BtnW, b.Height); } }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int h = Minus.Contains(e.Location) ? -1 : (Plus.Contains(e.Location) ? 1 : 0);
        if (h != hoverBtn) { hoverBtn = h; Invalidate(); }
        bool hh = Help.Length > 0 && helpRect.Contains(e.Location);
        if (hh != helpHover)
        {
            helpHover = hh; Invalidate();
            if (hh) HelpTip.Show(this, Help, new Point(helpRect.X, helpRect.Bottom + 6)); else HelpTip.Hide(this);
        }
        Cursor = h != 0 || hh ? Cursors.Hand : Cursors.Default;
        base.OnMouseMove(e);
    }
    protected override void OnMouseLeave(EventArgs e) { hoverBtn = 0; helpHover = false; HelpTip.Hide(this); StopRepeat(); Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        if (e.Button == MouseButtons.Left && hoverBtn != 0) { Value = value + hoverBtn * step; repeatDir = hoverBtn; repeat.Interval = 380; repeat.Start(); }
        base.OnMouseDown(e);
    }
    protected override void OnMouseUp(MouseEventArgs e) { StopRepeat(); base.OnMouseUp(e); }
    protected override void OnMouseWheel(MouseEventArgs e) { Value = value + Math.Sign(e.Delta) * step; base.OnMouseWheel(e); }
    void StopRepeat() { repeat.Stop(); repeatDir = 0; }

    protected override void Dispose(bool disposing) { if (disposing) repeat.Dispose(); base.Dispose(disposing); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        Theme.PaintParentBg(this, g, Theme.Card);
        var box = Box;
        int textW = box.X - 12;
        if (Help.Length > 0)
        {   // "?" right after the label
            int tw = TextRenderer.MeasureText(g, Text, Font, Size.Empty, TextFormatFlags.NoPadding).Width;
            int cy = Description.Length > 0 ? Height / 2 - 8 : Height / 2;
            helpRect = HelpTip.DrawIcon(g, Math.Min(tw + 10, textW - 18), cy, helpHover);
        }
        if (Description.Length > 0)
        {
            TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 0, textW, Height / 2 + 2), Theme.Fg, TextFormatFlags.Bottom | TextFormatFlags.EndEllipsis);
            using (var f = Theme.F(8.5f)) TextRenderer.DrawText(g, Description, f, new Rectangle(0, Height / 2 + 2, textW, Height / 2 - 2), Theme.Muted, TextFormatFlags.Top | TextFormatFlags.EndEllipsis);
        }
        else TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 0, textW, Height), Theme.Fg, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        Theme.FillRound(g, box, 10, Theme.Card2);
        using (var path = Theme.Round(new RectangleF(box.X + 0.5f, box.Y + 0.5f, box.Width - 1, box.Height - 1), 10))
        using (var pen = new Pen(Focused ? Theme.Alpha(Theme.Accent, 140) : Theme.Line)) g.DrawPath(pen, path);
        if (hoverBtn == -1) Theme.FillRound(g, new RectangleF(Minus.X + 3, Minus.Y + 3, Minus.Width - 6, Minus.Height - 6), 7, Theme.Hover);
        if (hoverBtn == 1) Theme.FillRound(g, new RectangleF(Plus.X + 3, Plus.Y + 3, Plus.Width - 6, Plus.Height - 6), 7, Theme.Hover);
        using (var pen = new Pen(value > min ? Theme.Fg : Theme.Dim, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            var m = Minus; g.DrawLine(pen, m.X + 11, m.Y + m.Height / 2f, m.Right - 11, m.Y + m.Height / 2f);
        }
        using (var pen = new Pen(value < max ? Theme.Fg : Theme.Dim, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            var p = Plus; float cx = p.X + p.Width / 2f, cy = p.Y + p.Height / 2f;
            g.DrawLine(pen, cx - 5, cy, cx + 5, cy); g.DrawLine(pen, cx, cy - 5, cx, cy + 5);
        }
        var valR = new Rectangle(box.X + BtnW, box.Y, ValW, box.Height);
        using (var f = Theme.F(10.5f, true))
        {
            string v = value.ToString();
            var vs = TextRenderer.MeasureText(g, v, f, Size.Empty, TextFormatFlags.NoPadding);
            using (var fu = Theme.F(8.5f))
            {
                var us = Unit.Length > 0 ? TextRenderer.MeasureText(g, " " + Unit, fu, Size.Empty, TextFormatFlags.NoPadding) : Size.Empty;
                int x = valR.X + (valR.Width - vs.Width - us.Width) / 2;
                TextRenderer.DrawText(g, v, f, new Rectangle(x, valR.Y, vs.Width + 2, valR.Height), Theme.Fg, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                if (Unit.Length > 0) TextRenderer.DrawText(g, " " + Unit, fu, new Rectangle(x + vs.Width, valR.Y + 2, us.Width + 4, valR.Height), Theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }
    }
}
