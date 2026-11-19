// "Úlovky" - list of the caught fish: this session or the whole history (catches.csv).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

public class CatchesPage : Page
{
    readonly Segmented filter;
    readonly CatchList list;
    readonly Card card;
    List<CatchInfo> session = new List<CatchInfo>();
    List<CatchInfo> history;           // loaded from the file when first needed
    int historyFrom = -1;              // session size when the history was loaded

    public CatchesPage() : base(L.T("Úlovky", "Catches"), L.T("Každá ryba, kterou bot chytil", "Every fish the bot caught"))
    {
        var grid = Grid(1, 2);
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var bar = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Margin = new Padding(0, 0, 0, 14) };
        filter = new Segmented(L.T("Tahle relace", "This session"), L.T("Celá historie", "All time")) { Width = 300, Location = new Point(0, 0) };
        filter.SelectedChanged += delegate { Fill(); };
        bar.Controls.Add(filter);
        grid.Controls.Add(bar, 0, 0);

        card = new Card(L.T("Tahle relace", "This session"), IconKind.Fish) { Dock = DockStyle.Fill, Margin = new Padding(0) };
        list = new CatchList { Dock = DockStyle.Fill };
        card.Controls.Add(list);
        grid.Controls.Add(card, 0, 1);
        Controls.Add(grid);
    }

    public override void UpdateData(Snapshot s, bool catchesChanged)
    {
        if (!catchesChanged) return;
        session = s.Catches;
        Fill();
    }

    public override void OnShown() { Fill(); }

    void Fill()
    {
        bool all = filter.Selected == 1;
        List<CatchInfo> items;
        if (all)
        {
            // the file already contains this session's catches - reload when new ones arrive
            if (history == null || historyFrom != session.Count) { history = FishaoBot.AllCatches(); historyFrom = session.Count; }
            items = history;
        }
        else items = session;
        list.ShowDate = all;
        list.SetItems(items, all ? 2000 : 1000);
        int fish = 0, esc = 0;
        foreach (var c in items) if (c.Escaped) esc++; else fish++;
        card.Title = all ? L.T("Celá historie", "All time") : L.T("Tahle relace", "This session");
        card.SetHint(L.N(fish, "ryba", "ryby", "ryb", "fish", "fish") + (esc > 0 ? "  ·  " + L.N(esc, "utekla", "utekly", "uteklo", "escaped", "escaped") : ""));
        card.Invalidate();
    }
}
