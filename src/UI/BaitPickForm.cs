// "Which bait?" window: shows a picture of the game (with the bait window open) and the user marks the bait
// they want - a click marks a square around the point, dragging marks any rectangle.
// The marked part is saved as a small picture; the bot later finds that bait by it (any game language).
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

public class BaitPickForm : Form
{
    const int ClickSize = 56;            // a plain click marks this big square (game pixels)
    readonly Bitmap shot;
    readonly PictureBox box;
    readonly ModernButton ok;
    Point? dragFrom, hover;
    Rectangle? marked;                    // in game picture pixels

    public Bitmap Result;                 // the marked part (caller owns it), set when OK was pressed

    public BaitPickForm(Bitmap shot)
    {
        this.shot = shot;
        Text = L.T("Vyber návnadu - klikni na ni (nebo ji táhnutím označ)", "Pick the bait - click it (or drag around it)");
        BackColor = Theme.Bg;
        StartPosition = FormStartPosition.CenterParent;
        var wa = Screen.FromControl(this).WorkingArea;
        double scale = Math.Min(1.0, Math.Min((wa.Width - 60) / (double)shot.Width, (wa.Height - 140) / (double)shot.Height));
        ClientSize = new Size((int)(shot.Width * scale), (int)(shot.Height * scale) + 52);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;

        box = new PictureBox { Dock = DockStyle.Fill, Image = shot, SizeMode = PictureBoxSizeMode.Zoom, Cursor = Cursors.Cross };
        box.Paint += OnPaintBox;
        box.MouseDown += delegate (object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) dragFrom = ToImage(e.Location); };
        box.MouseMove += delegate (object s, MouseEventArgs e) { hover = ToImage(e.Location); box.Invalidate(); };
        box.MouseUp += OnMouseUpBox;

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 52, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8), BackColor = Theme.Bg };
        ok = new ModernButton(L.T("Uložit", "Save"), Theme.Accent) { Width = 110, Height = 34, ForeColor = Theme.Bg, Enabled = false };
        ok.Click += delegate { Save(); };
        var cancel = ModernButton.GhostButton(L.T("Zrušit", "Cancel")); cancel.Width = 110; cancel.Height = 34; cancel.DialogResult = DialogResult.Cancel;
        var hint = new Label
        {
            Text = L.T("Ve hře musí být otevřený obchod (záložka Bait) nebo batoh s návnadami. Klikni na obrázek návnady.",
                       "The shop (Bait tab) or the bag with bait must be open in the game. Click the picture of the bait."),
            AutoSize = true, ForeColor = Theme.Muted, Margin = new Padding(0, 10, 10, 0)
        };
        bottom.Controls.Add(ok); bottom.Controls.Add(cancel); bottom.Controls.Add(hint);
        CancelButton = cancel;

        Controls.Add(box);
        Controls.Add(bottom);
    }

    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); NativeLook.DarkTitleBar(this); }

    void OnMouseUpBox(object sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || dragFrom == null) return;
        var to = ToImage(e.Location) ?? dragFrom.Value;
        var a = dragFrom.Value; dragFrom = null;
        Rectangle r = Math.Abs(to.X - a.X) < 8 && Math.Abs(to.Y - a.Y) < 8
            ? new Rectangle(a.X - ClickSize / 2, a.Y - ClickSize / 2, ClickSize, ClickSize)        // a click
            : Rectangle.FromLTRB(Math.Min(a.X, to.X), Math.Min(a.Y, to.Y), Math.Max(a.X, to.X), Math.Max(a.Y, to.Y));
        r.Intersect(new Rectangle(0, 0, shot.Width, shot.Height));
        if (r.Width < 12 || r.Height < 12) return;
        marked = r; ok.Enabled = true; ok.Invalidate();
        box.Invalidate();
    }

    void Save()
    {
        if (marked == null) return;
        var m = marked.Value;
        Result = new Bitmap(m.Width, m.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);   // own copy - the shot is disposed
        using (var g = Graphics.FromImage(Result)) g.DrawImage(shot, new Rectangle(0, 0, m.Width, m.Height), m, GraphicsUnit.Pixel);
        DialogResult = DialogResult.OK;
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
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        var st = g.Save();
        g.TranslateTransform(r.X, r.Y); g.ScaleTransform(s, s);
        Rectangle? show = marked;
        if (dragFrom != null && hover != null)
            show = Rectangle.FromLTRB(Math.Min(dragFrom.Value.X, hover.Value.X), Math.Min(dragFrom.Value.Y, hover.Value.Y), Math.Max(dragFrom.Value.X, hover.Value.X), Math.Max(dragFrom.Value.Y, hover.Value.Y));
        else if (marked == null && hover != null)
            show = new Rectangle(hover.Value.X - ClickSize / 2, hover.Value.Y - ClickSize / 2, ClickSize, ClickSize);
        if (show != null)
            using (var pen = new Pen(marked != null && dragFrom == null ? Theme.Accent : Color.FromArgb(200, 255, 255, 255), 2.5f / s))
                g.DrawRectangle(pen, show.Value);
        g.Restore(st);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) shot.Dispose();
        base.Dispose(disposing);
    }
}
