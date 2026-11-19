// "Statistiky" - all the numbers, the record fish (with its picture) and the stars of the catches.
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

public class StatsPage : Page
{
    Tile tFish, tEscaped, tPoints, tGames, tNoFish, tEnergy, tTime, tSuccess;
    FishView recordView, bestView;
    StarBars stars;
    DateTime recordImageTime = DateTime.MinValue;

    public StatsPage() : base(L.T("Statistiky", "Statistics"), L.T("Čísla za tuhle relaci a rekord za celou dobu", "Numbers for this session and the all-time record"))
    {
        var grid = Grid(1, 3);
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 104 * 2 + 14 + 14));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 196));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var tiles = Grid(4, 2);
        for (int i = 0; i < 4; i++) tiles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        for (int i = 0; i < 2; i++) tiles.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        tFish = Place(tiles, new Tile(L.T("Ulovené ryby", "Fish caught"), Theme.Accent, IconKind.Fish), 0, 0);
        tPoints = Place(tiles, new Tile(L.T("Body (BZ)", "Points (exp)"), Theme.Gold, IconKind.Trophy), 1, 0);
        tEscaped = Place(tiles, new Tile(L.T("Utekly", "Escaped"), Theme.Danger, IconKind.Hook), 2, 0);
        tSuccess = Place(tiles, new Tile(L.T("Úspěšnost miniher", "Minigame success"), Theme.Violet, IconKind.Chart), 3, 0);
        tGames = Place(tiles, new Tile(L.T("Minihry", "Minigames"), Theme.Orange, IconKind.Target), 0, 1);
        tNoFish = Place(tiles, new Tile(L.T("Nezabraly", "No bite"), Theme.Muted, IconKind.Hook), 1, 1);
        tEnergy = Place(tiles, new Tile(L.T("Koupeno energie", "Energy bought"), Theme.Blue, IconKind.Play), 2, 1);
        tTime = Place(tiles, new Tile(L.T("Doba běhu", "Running time"), Theme.Accent, IconKind.Log), 3, 1);
        tiles.Margin = new Padding(0, 0, 0, 14);
        grid.Controls.Add(tiles, 0, 0);

        var fishRow = Grid(2, 1);
        fishRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        fishRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        var recCard = Place(fishRow, new Card(L.T("Rekord - největší ryba vůbec", "Record - biggest fish ever"), IconKind.Trophy) { IconColor = Theme.Gold }, 0, 0);
        recordView = new FishView { Dock = DockStyle.Fill };
        recCard.Controls.Add(recordView);
        var bestCard = Place(fishRow, new Card(L.T("Největší ryba v téhle relaci", "Biggest fish this session"), IconKind.Fish), 1, 0);
        bestView = new FishView { Dock = DockStyle.Fill };
        bestCard.Controls.Add(bestView);
        fishRow.Margin = new Padding(0, 0, 0, 14);
        grid.Controls.Add(fishRow, 0, 1);

        var starCard = new Card(L.T("Hvězdičky úlovků v téhle relaci", "Stars of this session's catches"), IconKind.Chart) { Dock = DockStyle.Fill, Margin = new Padding(0), IconColor = Theme.Gold };
        stars = new StarBars { Dock = DockStyle.Fill };
        starCard.Controls.Add(stars);
        grid.Controls.Add(starCard, 0, 2);

        Controls.Add(grid);
    }

    public override void UpdateData(Snapshot s, bool catchesChanged)
    {
        tFish.Set(s.Fish.ToString(), L.T("celkem ", "total ") + s.AllTimeFish);
        tEscaped.Set(s.Escaped.ToString());
        tPoints.Set(s.Points.ToString());
        tSuccess.Set(s.SuccessText, L.T("chycené / všechny", "caught / all bites"));
        tGames.Set(s.Games.ToString());
        tNoFish.Set(s.NoFish.ToString(), L.T("nahození bez záběru", "casts without a bite"));
        tEnergy.Set(s.EnergyBought.ToString());
        tTime.Set(s.RunTimeText);
        if (catchesChanged)
        {
            stars.SetCounts(s.Stars);
            bestView.SetFish(s.Best);
            UpdateRecord(s.Record);
        }
    }

    void UpdateRecord(CatchInfo rec)
    {
        recordView.Fish = rec;
        try
        {
            var path = FishaoBot.RecordImageFile;
            if (File.Exists(path) && File.GetLastWriteTime(path) != recordImageTime)
            {
                recordImageTime = File.GetLastWriteTime(path);
                using (var fs = File.OpenRead(path)) using (var img = Image.FromStream(fs))
                {
                    var old = recordView.Picture; recordView.Picture = new Bitmap(img); if (old != null) old.Dispose();
                }
            }
        }
        catch { }
        recordView.Invalidate();
    }
}
