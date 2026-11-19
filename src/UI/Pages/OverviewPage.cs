// "Přehled" - the most important numbers, live picture of the game, biggest fish and the last catches.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

public class OverviewPage : Page
{
    Tile tFish, tPoints, tEscaped, tTime;
    PictureBox preview;
    Card previewCard, lastCard;
    FishView bestView;
    CatchList lastList;
    bool hasPicture;

    public OverviewPage() : base(L.T("Přehled", "Overview"), L.T("Co se právě děje ve hře", "What is happening in the game right now"))
    {
        var grid = Grid(1, 2);
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // ---- tiles
        var tiles = Grid(4, 1);
        for (int i = 0; i < 4; i++) tiles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        tFish = Place(tiles, new Tile(L.T("Ulovené ryby", "Fish caught"), Theme.Accent, IconKind.Fish), 0, 0);
        tPoints = Place(tiles, new Tile(L.T("Body (BZ)", "Points (exp)"), Theme.Gold, IconKind.Trophy), 1, 0);
        tEscaped = Place(tiles, new Tile(L.T("Utekly", "Escaped"), Theme.Danger, IconKind.Hook), 2, 0);
        tTime = Place(tiles, new Tile(L.T("Doba běhu", "Running time"), Theme.Blue, IconKind.Play), 3, 0);
        tiles.Margin = new Padding(0, 0, 0, 14);
        tiles.Dock = DockStyle.Fill;
        grid.Controls.Add(tiles, 0, 0);

        // ---- preview | (best fish + last catches)
        var bottom = Grid(2, 1);
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));
        previewCard = Place(bottom, new Card(L.T("Náhled hry", "Game preview"), IconKind.Target), 0, 0);
        preview = new PictureBox { Dock = DockStyle.Fill, BackColor = Theme.Card, SizeMode = PictureBoxSizeMode.Zoom };
        preview.Paint += PaintPreviewOverlay;
        previewCard.Controls.Add(preview);

        var side = Grid(1, 2);
        side.RowStyles.Add(new RowStyle(SizeType.Absolute, 184));
        side.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var bestCard = Place(side, new Card(L.T("Největší ryba relace", "Biggest fish this session"), IconKind.Trophy) { IconColor = Theme.Gold }, 0, 0);
        bestView = new FishView { Dock = DockStyle.Fill };
        bestCard.Controls.Add(bestView);
        lastCard = Place(side, new Card(L.T("Poslední úlovky", "Latest catches"), IconKind.Fish), 0, 1);
        lastList = new CatchList(true) { Dock = DockStyle.Fill };
        lastCard.Controls.Add(lastList);
        Place(bottom, side, 1, 0);
        bottom.Margin = new Padding(0);
        bottom.Dock = DockStyle.Fill;
        grid.Controls.Add(bottom, 0, 1);

        Controls.Add(grid);
    }

    // text over the empty preview
    void PaintPreviewOverlay(object sender, PaintEventArgs e)
    {
        if (hasPicture) return;
        var g = e.Graphics;
        var r = preview.ClientRectangle;
        Icons.Draw(g, IconKind.Target, r.Width / 2f - 20, r.Height / 2f - 50, 40, Theme.Dim, 2f);
        using (var f = Theme.F(11f, true)) TextRenderer.DrawText(g, L.T("Hra nebyla nalezena", "Game not found"), f, new Rectangle(0, r.Height / 2, r.Width, 26), Theme.Fg, TextFormatFlags.HorizontalCenter);
        using (var f = Theme.F(9f)) TextRenderer.DrawText(g, L.T("Spusť Fishao a nech okno otevřené (ne minimalizované).", "Start Fishao and keep its window open (not minimised)."), f, new Rectangle(0, r.Height / 2 + 26, r.Width, 22), Theme.Muted, TextFormatFlags.HorizontalCenter);
    }

    public override void UpdateData(Snapshot s, bool catchesChanged)
    {
        tFish.Set(s.Fish.ToString(), L.T("celkem ", "total ") + s.AllTimeFish);
        tPoints.Set(s.Points.ToString());
        tEscaped.Set(s.Escaped.ToString(), s.NoFish > 0 ? s.NoFish + L.T("× nezabrala", "× no bite") : "");
        tTime.Set(s.RunTimeText, s.Running ? L.T("běží", "running") : "");
        previewCard.SetHint(s.CastPoints == 0 ? L.T("místa nahození nejsou vybraná", "no cast points picked") : L.N(s.CastPoints, "místo nahození", "místa nahození", "míst nahození", "cast point", "cast points"));
        if (catchesChanged)
        {
            bestView.SetFish(s.Best);
            lastList.SetItems(s.Catches, 30);
            lastCard.SetHint(s.Catches.Count > 0 ? s.Catches.Count.ToString() : "");
        }
    }

    // shot = full game picture (disposed by the caller) or null when the game is not found
    public void SetPreview(Bitmap shot)
    {
        if (shot == null)
        {
            if (preview.Image != null) { var old0 = preview.Image; preview.Image = null; old0.Dispose(); }
            if (hasPicture) { hasPicture = false; preview.Invalidate(); }
            return;
        }
        // keep only a small copy (the full picture is ~8 MB)
        float k = Math.Min(1f, 900f / shot.Width);
        var small = new Bitmap(Math.Max(1, (int)(shot.Width * k)), Math.Max(1, (int)(shot.Height * k)));
        using (var g = Graphics.FromImage(small))
        {
            g.InterpolationMode = InterpolationMode.Bilinear;
            g.DrawImage(shot, 0, 0, small.Width, small.Height);
            g.ScaleTransform(k, k);
            var pts = FishaoBot.CastPoints;
            for (int i = 0; i < pts.Length; i++) CastMarker.Draw(g, pts[i], 1.8f / k, i + 1);
        }
        var old = preview.Image; preview.Image = small; hasPicture = true; if (old != null) old.Dispose();
    }
}
