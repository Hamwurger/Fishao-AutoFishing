// "Nastavení" - cast points, timing, energy. Every change is saved to FishaoBot.ini right away.
using System;
using System.Drawing;
using System.Windows.Forms;

public class SettingsPage : Page
{
    readonly Label castLabel;
    public event Action PickCastPoints;
    public event Action LanguageChanged;   // the main window rebuilds itself in the new language
    public event Action FacesChanged;      // floating faces turned on / off
    public event Action PickBait;          // the main window captures the game and lets the user mark the bait
    PictureBox baitPreview;
    Label baitLabel;
    public event Action CheckUpdates;      // "Check now" - the main window looks on GitHub
    ModernButton updateBtn;
    Label updateLabel;
    // kept across the language rebuild of the page
    static string lastUpdateText = ""; static Color lastUpdateColor = Theme.Muted;

    public void SetUpdateStatus(string text, Color color)
    {
        lastUpdateText = text; lastUpdateColor = color;
        if (updateLabel == null) return;
        updateLabel.Text = text; updateLabel.ForeColor = color;
    }
    public bool UpdateButtonEnabled { set { if (updateBtn != null) updateBtn.Enabled = value; } }

    // shows the chosen bait (bait.png next to the program), or "not chosen yet"
    public void ShowBait()
    {
        if (baitPreview == null) return;
        var old = baitPreview.Image; baitPreview.Image = null; if (old != null) old.Dispose();
        try
        {
            if (FishaoBot.BaitChosen)
                using (var fs = System.IO.File.OpenRead(FishaoBot.BaitPictureFile)) using (var img = Image.FromStream(fs)) baitPreview.Image = new Bitmap(img);
        }
        catch { }
        bool has = baitPreview.Image != null;
        baitLabel.Text = has
            ? L.T("Vybraná návnada", "Chosen bait")
            : L.T("Návnada zatím není vybraná\nvyber ji v obchodě nebo v batohu", "No bait chosen yet\npick it in the shop or in the bag");
        baitLabel.ForeColor = has ? Theme.Fg : Theme.Orange;
    }

    public SettingsPage() : base(L.T("Nastavení", "Settings"), L.T("Změny se ukládají hned", "Changes are saved right away"))
    {
        // three rows of cards; the page scrolls when the window is too low for all of them
        var grid = Grid(2, 3);
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        int[] rowH = { 260, 262, 410 };
        foreach (int h in rowH) grid.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
        grid.Dock = DockStyle.Top; grid.Height = rowH[0] + rowH[1] + rowH[2];

        // ---- cast points
        var castCard = Place(grid, new Card(L.T("Místa nahození", "Cast points"), IconKind.Target), 0, 0);
        var castBody = Stack(castCard);
        var info = new Label { Text = L.T("Klikni na místa na vodě, kam má bot nahazovat. Když jich vybereš víc, střídá je náhodně - vypadá to přirozeněji a ryby nedojdou.", "Click the spots on the water where the bot should cast. With more of them it alternates at random - looks more natural and the fish don't run out."), AutoSize = false, Height = 64, ForeColor = Theme.Muted, BackColor = Theme.Card, Margin = new Padding(0, 0, 0, 10) };
        castLabel = new Label { AutoSize = false, Height = 30, ForeColor = Theme.Fg, BackColor = Theme.Card, Font = Theme.F(11f, true), Margin = new Padding(0, 0, 0, 12), TextAlign = ContentAlignment.MiddleLeft };
        var pick = new ModernButton(L.T("Vybrat místa na vodě", "Pick spots on the water"), Theme.Blue, IconKind.Target) { Height = 42, Margin = new Padding(0) };
        pick.Click += delegate { if (PickCastPoints != null) PickCastPoints(); };
        castBody.Controls.Add(info); castBody.Controls.Add(castLabel); castBody.Controls.Add(pick);

        // ---- timing
        var timeCard = Place(grid, new Card(L.T("Časování", "Timing"), IconKind.Gear), 1, 0);
        var timeBody = Stack(timeCard);
        var hover = new Stepper(L.T("Najetí nad vodu", "Hover over water"), L.T("jak dlouho „držet“ myš před nahozením", "how long to hold the mouse before the cast"), 200, 3000, FishaoBot.CastHoverMs, 100, "ms");
        hover.ValueChanged += delegate { FishaoBot.CastHoverMs = hover.Value; AppSettings.Save(); };
        var timeout = new Stepper(L.T("Max. čekání na záběr", "Max. wait for a bite"), L.T("pojistka - pak nahodí znovu", "safety net - then it casts again"), 20, 600, FishaoBot.BiteTimeoutMs / 1000, 10, "s");
        timeout.ValueChanged += delegate { FishaoBot.BiteTimeoutMs = timeout.Value * 1000; AppSettings.Save(); };
        var after = new Stepper(L.T("Čekání na výsledek", "Wait for the result"), L.T("po minihře, než se objeví okno s rybou", "after the minigame, before the fish window shows"), 1, 20, FishaoBot.AfterGameDelayMs / 1000, 1, "s");
        after.ValueChanged += delegate { FishaoBot.AfterGameDelayMs = after.Value * 1000; AppSettings.Save(); };
        foreach (var c in new Control[] { hover, timeout, after }) { c.Margin = new Padding(0, 0, 0, 14); timeBody.Controls.Add(c); }

        // ---- energy
        var enCard = Place(grid, new Card(L.T("Energie", "Energy"), IconKind.Play) { IconColor = Theme.Blue }, 0, 1);
        var enBody = Stack(enCard);
        var buy = new Toggle(L.T("Kupovat energii", "Buy energy"), L.T("když dojde, koupí vybraný balíček", "when it runs out, buys the pack picked below")) { Checked = FishaoBot.AutoBuyEnergy, Margin = new Padding(0, 0, 0, 10) };
        buy.CheckedChanged += delegate { FishaoBot.AutoBuyEnergy = buy.Checked; AppSettings.Save(); };

        // which pack: 5 / 15 / 30 / 60 / 100 energy
        var packNames = new string[FishaoBot.EnergyPacks.Length];
        for (int i = 0; i < packNames.Length; i++) packNames[i] = FishaoBot.EnergyPacks[i].ToString();
        var pack = new Segmented(packNames) { Height = 36, Margin = new Padding(0, 0, 0, 4) };
        pack.Selected = Math.Max(0, Math.Min(packNames.Length - 1, FishaoBot.EnergyPack));
        var price = new Label { AutoSize = false, Height = 26, ForeColor = Theme.Muted, BackColor = Theme.Card, Font = Theme.F(9f), Margin = new Padding(0, 0, 0, 12) };
        Action showPrice = delegate
        {
            int i = pack.Selected;
            price.Text = L.T(FishaoBot.EnergyPacks[i] + " energie za " + FishaoBot.EnergyPrices[i] + " zelených",
                             FishaoBot.EnergyPacks[i] + " energy for " + FishaoBot.EnergyPrices[i] + " green money");
        };
        showPrice();
        pack.SelectedChanged += delegate { FishaoBot.EnergyPack = pack.Selected; showPrice(); AppSettings.Save(); };
        var wait = new Stepper(L.T("Čekat na energii", "Wait for energy"), L.T("jen když nejde přečíst odpočet ve hře", "only when the game's countdown can't be read"), 1, 60, FishaoBot.EnergyWaitMinutes, 1, "min");
        wait.Margin = new Padding(0);
        wait.ValueChanged += delegate { FishaoBot.EnergyWaitMinutes = wait.Value; AppSettings.Save(); };
        enBody.Controls.Add(buy); enBody.Controls.Add(pack); enBody.Controls.Add(price); enBody.Controls.Add(wait);

        // ---- bait
        var baitCard = Place(grid, new Card(L.T("Návnady", "Bait"), IconKind.Hook) { IconColor = Theme.Orange }, 0, 2);
        var baitBody = Stack(baitCard);
        // 1) put on bait automatically
        var autoBait = new Toggle(L.T("Nasazovat návnadu automaticky", "Put on bait automatically"), L.T("když v rámečku návnad zůstane jen háček", "when the bait slot shows only a hook"))
            { Checked = FishaoBot.AutoBait, Margin = new Padding(0, 0, 0, 8) };
        autoBait.CheckedChanged += delegate { FishaoBot.AutoBait = autoBait.Checked; AppSettings.Save(); };

        // 2) which bait: ONE picture, marked in the shop or in the bag (the bot finds it in both)
        var which = new TableLayoutPanel { Height = 60, ColumnCount = 3, RowCount = 1, BackColor = Theme.Card, Margin = new Padding(0, 0, 0, 10) };
        which.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 68));
        which.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        which.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        baitPreview = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Theme.Card2, Margin = new Padding(0, 0, 12, 0) };
        baitLabel = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Theme.Fg, BackColor = Theme.Card, Font = Theme.F(9.5f) };
        var pickBait = new ModernButton(L.T("Vybrat návnadu", "Pick the bait"), Theme.Orange, IconKind.Hook) { Dock = DockStyle.Fill, Margin = new Padding(0, 9, 0, 9), ForeColor = Theme.Bg };
        pickBait.Click += delegate { if (PickBait != null) PickBait(); };
        which.Controls.Add(baitPreview, 0, 0); which.Controls.Add(baitLabel, 1, 0); which.Controls.Add(pickBait, 2, 0);

        // 3) buy it when the bag has none
        var buyBait = new Toggle(L.T("Dokoupit v obchodě", "Buy more in the shop"), L.T("když v batohu žádná nezbude - koupí ji a hned nasadí", "when the bag has none left - buys it and puts it on"))
            { Checked = FishaoBot.BaitFromShop, Margin = new Padding(0, 0, 0, 8) };

        // 4) paying with green money / gold coins / either, and how many packs
        var payLabel = new Label { Text = L.T("Platit", "Pay with"), AutoSize = false, Height = 22, ForeColor = Theme.Muted, BackColor = Theme.Card, Font = Theme.F(9f), Margin = new Padding(0) };
        var pay = new Segmented(L.T("Zelené", "Green"), L.T("Zlaté", "Gold"), L.T("Čímkoli", "Either")) { Height = 36, Margin = new Padding(0, 0, 0, 10) };
        pay.Selected = Math.Max(0, Math.Min(2, FishaoBot.BaitPay));
        pay.SelectedChanged += delegate { FishaoBot.BaitPay = pay.Selected; AppSettings.Save(); };
        var packs = new Stepper(L.T("Kolik balíčků koupit", "Packs to buy"), L.T("najednou (třešně = 40 kusů v balíčku)", "at once (cherries = 40 in a pack)"), 1, 10, FishaoBot.BaitPacks, 1, "×");
        packs.Margin = new Padding(0);
        packs.ValueChanged += delegate { FishaoBot.BaitPacks = packs.Value; AppSettings.Save(); };

        Action showShop = delegate { payLabel.Visible = pay.Visible = packs.Visible = buyBait.Checked; };
        showShop();
        buyBait.CheckedChanged += delegate { FishaoBot.BaitFromShop = buyBait.Checked; showShop(); AppSettings.Save(); };
        ShowBait();
        foreach (var c in new Control[] { autoBait, which, buyBait, payLabel, pay, packs }) baitBody.Controls.Add(c);

        // ---- language (+ floating faces)
        var langCard = Place(grid, new Card("Jazyk  ·  Language", IconKind.Globe), 1, 1);
        var funny = new Toggle(L.T("Plovoucí rybičky", "Floating fish"), L.T("na pozadí okna", "in the window background"))
            { Dock = DockStyle.Top, Checked = AppSettings.FloatingFaces };
        funny.CheckedChanged += delegate { AppSettings.FloatingFaces = funny.Checked; AppSettings.Save(); if (FacesChanged != null) FacesChanged(); };
        langCard.Controls.Add(funny);
        langCard.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 12, BackColor = Theme.Card });   // gap
        var lang = new Segmented("Čeština", "English") { Dock = DockStyle.Top, Height = 38 };
        lang.Selected = L.English ? 1 : 0;
        lang.SelectedChanged += delegate
        {
            L.English = lang.Selected == 1;
            AppSettings.Save();
            if (LanguageChanged != null) LanguageChanged();
        };
        langCard.Controls.Add(lang);

        // ---- updates (top) + tips (bottom) share the last cell
        var split = Place(grid, new TableLayoutPanel { ColumnCount = 1, RowCount = 2, BackColor = Theme.Bg, Padding = new Padding(0) }, 1, 2);
        split.RowStyles.Add(new RowStyle(SizeType.Absolute, 196));
        split.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var updCard = new Card(L.T("Aktualizace", "Updates"), IconKind.Globe) { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 7), IconColor = Theme.Blue };
        updCard.SetHint(L.T("verze ", "version ") + Updater.Version);
        var updBody = Stack(updCard);
        var autoUpd = new Toggle(L.T("Hledat novou verzi při spuštění", "Look for a new version on start"), L.T("stáhne ji z GitHubu a sám se restartuje", "downloads it from GitHub and restarts itself"))
            { Checked = Updater.AutoCheck, Margin = new Padding(0, 0, 0, 8) };
        autoUpd.CheckedChanged += delegate { Updater.AutoCheck = autoUpd.Checked; AppSettings.Save(); };
        var updRow = new TableLayoutPanel { Height = 40, ColumnCount = 2, RowCount = 1, BackColor = Theme.Card, Margin = new Padding(0) };
        updRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        updRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        updateBtn = new ModernButton(L.T("Zkontrolovat teď", "Check now"), Theme.Blue, IconKind.Globe) { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 12, 0) };
        updateBtn.Click += delegate { if (CheckUpdates != null) CheckUpdates(); };
        updateLabel = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Theme.Muted, BackColor = Theme.Card, Font = Theme.F(9f) };
        updRow.Controls.Add(updateBtn, 0, 0); updRow.Controls.Add(updateLabel, 1, 0);
        updBody.Controls.Add(autoUpd); updBody.Controls.Add(updRow);
        SetUpdateStatus(lastUpdateText, lastUpdateColor);
        split.Controls.Add(updCard, 0, 0);

        var tipCard = new Card(L.T("Tipy", "Tips"), IconKind.Log) { Dock = DockStyle.Fill, Margin = new Padding(0, 7, 0, 0) };
        tipCard.Controls.Add(new Label
        {
            Dock = DockStyle.Fill, ForeColor = Theme.Muted, BackColor = Theme.Card, Font = Theme.F(9.5f),
            Text = L.T(
                "•  Hra může být zakrytá jinými okny - jen ne minimalizovaná.\n" +
                "•  Myš ani klávesnici bot nepoužívá, můžeš normálně pracovat.\n" +
                "•  F10 spustí / zastaví bota odkudkoli.\n" +
                "•  Úlovky se ukládají do catches.csv vedle programu.",
                "•  The game may be covered by other windows - just not minimised.\n" +
                "•  The bot doesn't use your mouse or keyboard, you can keep working.\n" +
                "•  F10 starts / stops the bot from anywhere.\n" +
                "•  Catches are saved to catches.csv next to the program.")
        });
        split.Controls.Add(tipCard, 0, 1);

        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Bg };
        scroll.Controls.Add(grid);
        NativeLook.DarkScrollBars(scroll);
        Controls.Add(scroll);
    }

    // vertical list inside a card whose items keep the card's width
    static FlowLayoutPanel Stack(Card card)
    {
        var p = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Theme.Card, Margin = new Padding(0), Padding = new Padding(0) };
        EventHandler fit = delegate { foreach (Control c in p.Controls) c.Width = p.ClientSize.Width; };
        p.Resize += fit;
        p.ControlAdded += delegate (object s, ControlEventArgs e) { e.Control.Width = p.ClientSize.Width; };
        card.Controls.Add(p);
        return p;
    }

    public override void UpdateData(Snapshot s, bool catchesChanged)
    {
        int n = s.CastPoints;
        string t = n == 0 ? L.T("Zatím nic nevybráno", "Nothing picked yet") : (n == 1 ? L.T("1 místo  (doporučuji víc)", "1 spot  (more is better)") : L.N(n, "místo", "místa", "míst", "spot", "spots") + L.T("  ·  střídá je náhodně", "  ·  picked at random"));
        if (castLabel.Text != t) { castLabel.Text = t; castLabel.ForeColor = n == 0 ? Theme.Orange : Theme.Fg; }
    }
}

