// "Where to cast" window, two ways:
//   Terčíky (points) - left click adds a point, right click removes the nearest one; the bot alternates between them
//   Oblast (area)    - hold the left button and draw an outline on the water; the bot casts to a random spot inside it
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

public class PickForm : Form
{
    readonly Bitmap shot;
    public readonly List<Point> Points;
    public List<Point> Area;              // the outline (closed automatically)
    public bool UseArea;
    List<Point> drawing;                  // the outline being drawn right now
    Point? hover;
    readonly PictureBox box;
    readonly Label countLabel, hint;

    public PickForm(Bitmap shot, Point[] current, Point[] area, bool useArea)
    {
        this.shot = shot;
        Points = new List<Point>(current ?? new Point[0]);
        Area = new List<Point>(area ?? new Point[0]);
        UseArea = useArea;
        Text = L.T("Kam nahazovat", "Where to cast");
        BackColor = Theme.Bg;
        StartPosition = FormStartPosition.CenterParent;
        var wa = Screen.FromControl(this).WorkingArea;
        double scale = Math.Min(1.0, Math.Min((wa.Width - 60) / (double)shot.Width, (wa.Height - 140) / (double)shot.Height));
        ClientSize = new Size(Math.Max(900, (int)(shot.Width * scale)), (int)(shot.Height * scale) + 52);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;

        box = new PictureBox { Dock = DockStyle.Fill, Image = shot, SizeMode = PictureBoxSizeMode.Zoom, Cursor = Cursors.Cross, BackColor = Theme.Bg };
        box.Paint += OnPaintBox;
        box.MouseDown += OnDown;
        box.MouseMove += OnMove;
        box.MouseUp += OnUp;
        box.MouseLeave += delegate { hover = null; box.Invalidate(); };

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 52, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8, 8, 8, 8), BackColor = Theme.Bg, WrapContents = false };
        var ok = new ModernButton(L.T("Uložit", "Save"), Theme.Accent) { Width = 110, Height = 34, ForeColor = Theme.Bg }; ok.DialogResult = DialogResult.OK;
        var cancel = ModernButton.GhostButton(L.T("Zrušit", "Cancel")); cancel.Width = 110; cancel.Height = 34; cancel.DialogResult = DialogResult.Cancel;
        var clear = new ModernButton(L.T("Vymazat", "Clear"), Theme.Danger) { Width = 110, Height = 34 };
        clear.Click += delegate { if (UseArea) Area.Clear(); else Points.Clear(); UpdateTexts(); box.Invalidate(); };
        countLabel = new Label { AutoSize = true, ForeColor = Color.White, Font = Theme.F(10f, true), Margin = new Padding(10, 9, 10, 0) };
        hint = new Label { AutoSize = true, ForeColor = Theme.Muted, Margin = new Padding(0, 10, 10, 0) };
        var mode = new Segmented(L.T("Terčíky", "Points"), L.T("Oblast", "Area")) { Width = 220, Height = 34, Margin = new Padding(0, 0, 8, 0) };
        mode.Selected = UseArea ? 1 : 0;
        mode.SelectedChanged += delegate { UseArea = mode.Selected == 1; drawing = null; UpdateTexts(); box.Invalidate(); };
        bottom.Controls.Add(ok); bottom.Controls.Add(cancel); bottom.Controls.Add(clear); bottom.Controls.Add(countLabel); bottom.Controls.Add(hint);

        // the mode switch on the left, the rest on the right
        var bar = new Panel { Dock = DockStyle.Bottom, Height = 52, BackColor = Theme.Bg, Padding = new Padding(240, 0, 0, 0) };
        mode.Location = new Point(10, 9);
        bottom.Dock = DockStyle.Fill;
        bar.Controls.Add(bottom); bar.Controls.Add(mode);
        AcceptButton = ok; CancelButton = cancel;

        Controls.Add(box);
        Controls.Add(bar);
        UpdateTexts();
    }

    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); NativeLook.DarkTitleBar(this); }

    void UpdateTexts()
    {
        if (UseArea)
        {
            countLabel.Text = Area.Count >= 3 ? L.T("Oblast nakreslena", "Area drawn") : L.T("Žádná oblast", "No area");
            hint.Text = L.T("Drž levé tlačítko a obtáhni kus vody v dosahu prutu. Bot nahodí pokaždé jinam uvnitř.", "Hold the left button and outline some water in rod range. The bot casts somewhere else inside each time.");
        }
        else
        {
            countLabel.Text = L.T("Míst: ", "Points: ") + Points.Count;
            hint.Text = L.T("Levé tlačítko přidá terčík, pravé odebere. Bot je náhodně střídá.", "Left click adds a point, right click removes one. The bot alternates at random.");
        }
    }

    void OnDown(object sender, MouseEventArgs e)
    {
        var p = ToImage(e.Location);
        if (p == null) return;
        if (UseArea)
        {
            if (e.Button == MouseButtons.Left) { drawing = new List<Point> { p.Value }; }
            else if (e.Button == MouseButtons.Right) { Area.Clear(); UpdateTexts(); }
            box.Invalidate();
            return;
        }
        if (e.Button == MouseButtons.Left) Points.Add(p.Value);
        else if (e.Button == MouseButtons.Right && Points.Count > 0)
        {
            // remove the nearest point (if the click was reasonably close to it)
            int best = -1; double bestD = double.MaxValue;
            for (int i = 0; i < Points.Count; i++)
            {
                double dx = Points[i].X - p.Value.X, dy = Points[i].Y - p.Value.Y, d = dx * dx + dy * dy;
                if (d < bestD) { bestD = d; best = i; }
            }
            if (best >= 0 && bestD < 60 * 60) Points.RemoveAt(best);
        }
        UpdateTexts();
        box.Invalidate();
    }

    void OnMove(object sender, MouseEventArgs e)
    {
        hover = ToImage(e.Location);
        if (drawing != null && hover != null)
        {
            var last = drawing[drawing.Count - 1];
            int dx = hover.Value.X - last.X, dy = hover.Value.Y - last.Y;
            if (dx * dx + dy * dy >= 36) drawing.Add(hover.Value);   // a point every ~6 px is plenty
        }
        box.Invalidate();
    }

    void OnUp(object sender, MouseEventArgs e)
    {
        if (drawing == null) return;
        if (drawing.Count >= 3 && Extent(drawing) >= 20) Area = drawing;   // ignore a plain click
        drawing = null;
        UpdateTexts(); box.Invalidate();
    }

    static int Extent(List<Point> pts)
    {
        int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
        foreach (var p in pts) { x0 = Math.Min(x0, p.X); y0 = Math.Min(y0, p.Y); x1 = Math.Max(x1, p.X); y1 = Math.Max(y1, p.Y); }
        return Math.Min(x1 - x0, y1 - y0);
    }

    // PictureBox Zoom mapping
    RectangleF ImageRect()
    {
        float s = Math.Min(box.ClientSize.Width / (float)shot.Width, box.ClientSize.Height / (float)shot.Height);
        float w = shot.Width * s, h = shot.Height * s;
        return new RectangleF((box.ClientSize.Width - w) / 2, (box.ClientSize.Height - h) / 2, w, h);
    }

    Point? ToImage(Point p)
    {
        var r = ImageRect();
        if (!r.Contains(p)) return null;
        return new Point((int)((p.X - r.X) * shot.Width / r.Width), (int)((p.Y - r.Y) * shot.Height / r.Height));
    }

    void OnPaintBox(object sender, PaintEventArgs e)
    {
        var r = ImageRect(); float s = r.Width / shot.Width;
        var g = e.Graphics;
        var st = g.Save();
        g.TranslateTransform(r.X, r.Y); g.ScaleTransform(s, s);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        if (UseArea)
        {
            if (drawing != null && drawing.Count >= 2)
                using (var pen = new Pen(Color.FromArgb(230, 90, 190, 255), 2.5f / s) { LineJoin = LineJoin.Round }) g.DrawLines(pen, drawing.ToArray());
            else CastMarker.DrawArea(g, Area.ToArray(), 2.5f / s);
        }
        else
        {
            for (int i = 0; i < Points.Count; i++) CastMarker.Draw(g, Points[i], 3f / s, i + 1);
        }
        if (hover != null && drawing == null)
            using (var pen = new Pen(Color.FromArgb(160, 255, 255, 255), 1.5f / s))
                g.DrawEllipse(pen, hover.Value.X - 14, hover.Value.Y - 14, 28, 28);
        g.Restore(st);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) shot.Dispose();
        base.Dispose(disposing);
    }
}
