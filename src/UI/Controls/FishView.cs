// A fish shown big: optional picture + name + size + stars + points/time.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

public class FishView : Control
{
    public Image Picture;
    public CatchInfo Fish;
    public string Empty = L.T("Zatím žádný úlovek", "No catch yet");

    public FishView()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
    }

    public void SetFish(CatchInfo fish) { if (fish != Fish) { Fish = fish; Invalidate(); } }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.Clear(Theme.Card);   // Clear fills every pixel fully (an anti-aliased fill would leave the edge pixels half done)
        int textX = 0;
        if (Picture != null)
        {
            int h = Height, w = Math.Min(Width / 2, h * Picture.Width / Math.Max(1, Picture.Height));
            using (var path = Theme.Round(new RectangleF(0, 0, w, h), 12)) { g.SetClip(path); g.DrawImage(Picture, 0, 0, w, h); g.ResetClip(); }
            textX = w + 18;
        }
        else
        {   // placeholder chip with a fish icon
            int s = Math.Min(Height, 84);
            Theme.FillRound(g, new RectangleF(0, (Height - s) / 2f, s, s), 14, Theme.Card2);
            Icons.Draw(g, IconKind.Fish, s * 0.25f, (Height - s) / 2f + s * 0.25f, s * 0.5f, Fish != null ? Theme.Gold : Theme.Dim, 2f);
            textX = s + 18;
        }
        if (Fish == null)
        {
            using (var f = Theme.F(10f)) TextRenderer.DrawText(g, Empty, f, new Rectangle(textX, 0, Width - textX, Height), Theme.Muted, TextFormatFlags.VerticalCenter);
            return;
        }
        int top = Math.Max(0, (Height - 104) / 2);
        using (var f = Theme.F(11.5f, true)) TextRenderer.DrawText(g, Fish.Name.Length > 0 ? Fish.Name : "?", f, new Rectangle(textX, top, Width - textX, 24), Theme.Fg, TextFormatFlags.EndEllipsis);
        using (var f = Theme.F(24f, true)) TextRenderer.DrawText(g, Fish.Cm > 0 ? Fish.Cm + " cm" : L.T("? cm", "? cm"), f, new Point(textX - 4, top + 22), Theme.Gold);
        Theme.Stars(g, textX, top + 78, 8, Fish.Stars);
        using (var f = Theme.F(8.5f)) TextRenderer.DrawText(g, (Fish.Points > 0 ? Fish.Points + L.T(" BZ", " exp") + "  ·  " : "") + Fish.Time.ToString("d.M. HH:mm"), f, new Point(textX, top + 90), Theme.Muted);
    }
}
