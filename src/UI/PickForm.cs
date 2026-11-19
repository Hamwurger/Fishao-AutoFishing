// "Click on the water" window: left click adds a cast point, right click removes the nearest one.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

public class PickForm : Form
{
    readonly Bitmap shot;
    public readonly List<Point> Points;
    Point? hover;
    readonly PictureBox box;
    readonly Label countLabel;

    public PickForm(Bitmap shot, Point[] current)
    {
        this.shot = shot;
        Points = new List<Point>(current ?? new Point[0]);
        Text = L.T("Místa nahození - levé tlačítko přidá, pravé odebere", "Cast points - left click adds, right click removes");
        BackColor = Theme.Bg;
        StartPosition = FormStartPosition.CenterParent;
        var wa = Screen.FromControl(this).WorkingArea;
        double scale = Math.Min(1.0, Math.Min((wa.Width - 60) / (double)shot.Width, (wa.Height - 140) / (double)shot.Height));
        ClientSize = new Size((int)(shot.Width * scale), (int)(shot.Height * scale) + 52);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;

        box = new PictureBox { Dock = DockStyle.Fill, Image = shot, SizeMode = PictureBoxSizeMode.Zoom, Cursor = Cursors.Cross };
        box.Paint += OnPaintBox;
        box.MouseMove += delegate (object s, MouseEventArgs e) { hover = ToImage(e.Location); box.Invalidate(); };
        box.MouseLeave += delegate { hover = null; box.Invalidate(); };
        box.MouseClick += OnClickBox;

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 52, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8, 8, 8, 8), BackColor = Theme.Bg };
        var ok = new ModernButton(L.T("Uložit", "Save"), Theme.Accent) { Width = 110, Height = 34, ForeColor = Theme.Bg }; ok.DialogResult = DialogResult.OK;
        var cancel = ModernButton.GhostButton(L.T("Zrušit", "Cancel")); cancel.Width = 110; cancel.Height = 34; cancel.DialogResult = DialogResult.Cancel;
        var clear = new ModernButton(L.T("Vymazat vše", "Clear all"), Theme.Danger) { Width = 120, Height = 34 };
        clear.Click += delegate { Points.Clear(); UpdateCount(); box.Invalidate(); };
        countLabel = new Label { AutoSize = true, ForeColor = Color.White, Font = Theme.F(10f, true), Margin = new Padding(10, 9, 10, 0) };
        var hint = new Label { Text = L.T("Klikej na vodu uvnitř kruhu dosahu prutu. Bot bude nahazovat náhodně střídavě.", "Click on the water inside the rod's range circle. The bot will alternate between them at random."), AutoSize = true, ForeColor = Theme.Muted, Margin = new Padding(0, 10, 10, 0) };
        bottom.Controls.Add(ok); bottom.Controls.Add(cancel); bottom.Controls.Add(clear); bottom.Controls.Add(countLabel); bottom.Controls.Add(hint);
        AcceptButton = ok; CancelButton = cancel;

        Controls.Add(box);
        Controls.Add(bottom);
        UpdateCount();
    }

    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); NativeLook.DarkTitleBar(this); }

    void UpdateCount() { countLabel.Text = L.T("Míst: ", "Points: ") + Points.Count; }

    void OnClickBox(object sender, MouseEventArgs e)
    {
        var p = ToImage(e.Location);
        if (p == null) return;
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
        UpdateCount();
        box.Invalidate();
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
        var st = e.Graphics.Save();
        e.Graphics.TranslateTransform(r.X, r.Y); e.Graphics.ScaleTransform(s, s);
        for (int i = 0; i < Points.Count; i++) CastMarker.Draw(e.Graphics, Points[i], 3f / s, i + 1);
        if (hover != null)
            using (var pen = new Pen(Color.FromArgb(160, 255, 255, 255), 1.5f / s))
                e.Graphics.DrawEllipse(pen, hover.Value.X - 14, hover.Value.Y - 14, 28, 28);
        e.Graphics.Restore(st);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) shot.Dispose();
        base.Dispose(disposing);
    }
}
