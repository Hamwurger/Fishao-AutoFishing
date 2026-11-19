// List of catches - one row per fish: time, name, size, stars, points.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

public class CatchList : ListBox
{
    public bool ShowDate;   // "17.11. 14:05" instead of "14:05" (history of all catches)
    public bool Compact;    // smaller rows for the overview page

    public CatchList(bool compact = false)
    {
        Compact = compact;
        DrawMode = DrawMode.OwnerDrawFixed; ItemHeight = compact ? 40 : 48;
        BackColor = Theme.Card; ForeColor = Theme.Fg; BorderStyle = BorderStyle.None; IntegralHeight = false;
        SelectionMode = SelectionMode.None;
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        NativeLook.DarkScrollBars(this);
    }

    // newest first, at most "limit" rows
    public void SetItems(List<CatchInfo> catches, int limit)
    {
        BeginUpdate();
        Items.Clear();
        for (int i = catches.Count - 1; i >= 0 && Items.Count < limit; i--) Items.Add(catches[i]);
        EndUpdate();
        Invalidate();
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        var g = e.Graphics; var r = e.Bounds;
        using (var b = new SolidBrush(Theme.Card)) g.FillRectangle(b, r);   // before anti-aliasing: whole pixels, no thin lines
        g.SmoothingMode = SmoothingMode.AntiAlias;
        if (e.Index < 0 || e.Index >= Items.Count) return;
        var c = Items[e.Index] as CatchInfo; if (c == null) return;

        var row = new RectangleF(r.X, r.Y + 2, r.Width - 2, r.Height - 4);
        if (e.Index % 2 == 0) Theme.FillRound(g, row, 9, Theme.Mix(Theme.Card, Theme.Card2, 0.55f));
        int cy = r.Y + r.Height / 2, x = r.X + 10;

        // round chip with the fish icon (red for escaped fish)
        int chip = Compact ? 26 : 30;
        var chipColor = c.Escaped ? Theme.Danger : (c.Stars >= 4 ? Theme.Gold : Theme.Accent);
        Theme.FillRound(g, new RectangleF(x, cy - chip / 2f, chip, chip), chip / 2f, Theme.Alpha(chipColor, 36));
        Icons.Draw(g, IconKind.Fish, x + chip * 0.2f, cy - chip * 0.3f, chip * 0.6f, chipColor, 1.5f);
        x += chip + 12;

        string time = ShowDate ? c.Time.ToString("d.M. HH:mm") : c.Time.ToString("HH:mm");
        int timeW = ShowDate ? 82 : 44;
        using (var f = Theme.F(8.5f)) TextRenderer.DrawText(g, time, f, new Rectangle(x, r.Y, timeW, r.Height), Theme.Muted, TextFormatFlags.VerticalCenter);
        x += timeW + 6;

        if (c.Escaped)
        {
            using (var f = Theme.F(10f)) TextRenderer.DrawText(g, L.T("Ryba utekla", "Fish escaped"), f, new Rectangle(x, r.Y, r.Right - x, r.Height), Theme.Danger, TextFormatFlags.VerticalCenter);
            return;
        }

        bool narrow = r.Width < 420;
        int right = r.Right - 12;
        int ptsW = narrow ? 0 : 56, starsW = 5 * 13 + 4, cmW = 64;
        if (ptsW > 0)
            using (var f = Theme.F(9f)) TextRenderer.DrawText(g, c.Points > 0 ? c.Points + L.T(" BZ", " exp") : "", f, new Rectangle(right - ptsW, r.Y, ptsW, r.Height), Theme.Muted, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        int starsX = right - ptsW - starsW - (ptsW > 0 ? 12 : 0);
        Theme.Stars(g, starsX, cy, 5.6f, c.Stars);
        int cmX = starsX - cmW - 8;
        using (var f = Theme.F(10f, true)) TextRenderer.DrawText(g, c.Cm > 0 ? c.Cm + " cm" : "", f, new Rectangle(cmX, r.Y, cmW, r.Height), Theme.Gold, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        using (var f = Theme.F(10f, true)) TextRenderer.DrawText(g, c.Name.Length > 0 ? c.Name : "?", f, new Rectangle(x, r.Y, Math.Max(10, cmX - x - 6), r.Height), Theme.Fg, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}
