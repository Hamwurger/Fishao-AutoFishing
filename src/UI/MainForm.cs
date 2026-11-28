// The main window: side menu with tabs on the left, page title + status + Start/Stop on top, the page below.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

public class MainForm : Form
{
    const int SideW = 236;

    readonly Thread botThread;
    Panel side, header, pageHost;
    StatusChip status;
    ModernButton startBtn;
    readonly List<NavItem> navItems = new List<NavItem>();
    readonly List<Page> pages = new List<Page>();
    OverviewPage overview;
    CatchesPage catchesPage;
    StatsPage statsPage;
    LogPage logPage;
    SettingsPage settingsPage;
    readonly List<string> logLines = new List<string>();   // kept when the window is rebuilt in another language
    FloatingFaces faces;                                   // the fun floating faces in the background
    NotifyIcon tray;                                       // icon next to the clock (notifications)
    readonly System.Windows.Forms.Timer uiTimer;
    Page current;

    DateTime? startedAt; TimeSpan runTotal = TimeSpan.Zero;
    int tick, shownCatches = -1;
    bool gameFound;

    public MainForm(int startPage = 0)
    {
        Text = "Fishao Bot";
        BackColor = Theme.Bg; ForeColor = Theme.Fg; Font = Theme.F(9.5f);
        ClientSize = new Size(1280, 820);
        MinimumSize = new Size(1100, 720);
        StartPosition = FormStartPosition.CenterScreen;
        DoubleBuffered = true; KeyPreview = true;
        try { Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location); } catch { }

        BuildUi(startPage);
        faces = new FloatingFaces(this);
        AttachFaces();
        faces.Enabled = AppSettings.FloatingFaces;

        // ---------------- bot thread + events
        FishaoBot.LogMessage += OnLog;
        FishaoBot.StateChanged += OnState;
        FishaoBot.Problem += OnProblem;

        // icon next to the clock - shows a notification when the watchdog stops the bot
        tray = new NotifyIcon { Icon = Icon, Text = "Fishao Bot", Visible = true };
        tray.DoubleClick += delegate { ShowFromTray(); };
        tray.BalloonTipClicked += delegate { ShowFromTray(); };
        botThread = new Thread(FishaoBot.Run) { IsBackground = true, Name = "bot" };
        botThread.Start();

        uiTimer = new System.Windows.Forms.Timer { Interval = 250 };
        uiTimer.Tick += delegate { RefreshUi(); };
        uiTimer.Start();

        OnLog(DateTime.Now.ToString("HH:mm:ss") + "  " + (!FishaoBot.HasCastTarget
            ? L.T("Vyber místa nahození (Nastavení) a zmáčkni Start.", "Pick cast points (Settings) and press Start.")
            : L.T("Připraveno - zmáčkni Start nebo F10.", "Ready - press Start or F10.")));
        RefreshUi();
    }

    // Creates everything inside the window (again after the language changes - the bot keeps running).
    void BuildUi(int startPage)
    {
        SuspendLayout();
        var oldControls = new List<Control>();
        foreach (Control c in Controls) oldControls.Add(c);
        Controls.Clear();
        foreach (var c in oldControls) c.Dispose();
        navItems.Clear(); pages.Clear(); current = null;

        // ---------------- pages
        overview = new OverviewPage();
        catchesPage = new CatchesPage();
        statsPage = new StatsPage();
        logPage = new LogPage();
        settingsPage = new SettingsPage();
        settingsPage.PickCastPoints += PickPoints;
        settingsPage.PickBait += PickBait;
        settingsPage.CheckUpdates += delegate { CheckForUpdate(true); };
        logPage.TestNotification += delegate
        {
            string msg = L.T("Tohle je jen zkouška upozornění - bot normálně pokračuje.", "This is only a test of the notification - the bot carries on.");
            OnLog(DateTime.Now.ToString("HH:mm:ss") + L.T("  POZOR (zkouška): ", "  WARNING (test): ") + msg);
            OnProblem(msg);
        };
        settingsPage.LanguageChanged += delegate { BeginInvoke(new Action(ChangeLanguage)); };
        settingsPage.FacesChanged += delegate { if (faces != null) faces.Enabled = AppSettings.FloatingFaces; };
        pages.AddRange(new Page[] { overview, catchesPage, statsPage, logPage, settingsPage });

        // ---------------- side menu
        side = new Panel { Dock = DockStyle.Left, Width = SideW, BackColor = Theme.Side };
        side.Paint += PaintSide;
        var navDefs = new[] {
            new { Text = L.T("Přehled", "Overview"), Icon = IconKind.Home },
            new { Text = L.T("Úlovky", "Catches"), Icon = IconKind.Fish },
            new { Text = L.T("Statistiky", "Statistics"), Icon = IconKind.Chart },
            new { Text = L.T("Záznam", "Log"), Icon = IconKind.Log },
            new { Text = L.T("Nastavení", "Settings"), Icon = IconKind.Gear } };
        for (int i = 0; i < navDefs.Length; i++)
        {
            var item = new NavItem(navDefs[i].Text, navDefs[i].Icon) { Location = new Point(0, 104 + i * 48), Width = SideW - 1 };
            int index = i;
            item.Click += delegate { ShowPage(index); };
            navItems.Add(item); side.Controls.Add(item);
        }

        // ---------------- header above the page
        var content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Padding = new Padding(30, 0, 30, 26) };
        header = new Panel { Dock = DockStyle.Top, Height = 98, BackColor = Theme.Bg };
        header.Paint += PaintHeader;
        status = new StatusChip { Width = 330 };
        startBtn = new ModernButton("Start", Theme.Accent, IconKind.Play) { Width = 150, Height = 44, Font = Theme.F(11f, true), Radius = 12 };
        startBtn.ForeColor = Theme.Bg;
        startBtn.Click += delegate { if (FishaoBot.IsRunning) FishaoBot.Stop(); else FishaoBot.Start(); };
        header.Controls.Add(status); header.Controls.Add(startBtn);
        header.Resize += delegate { LayoutHeader(); };

        pageHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
        foreach (var p in pages) { p.Visible = false; pageHost.Controls.Add(p); }

        content.Controls.Add(pageHost); content.Controls.Add(header);
        Controls.Add(content); Controls.Add(side);

        foreach (var line in logLines) logPage.Add(line);
        ShowPage(Math.Max(0, Math.Min(pages.Count - 1, startPage)));
        LayoutHeader();
        ResumeLayout(true);
    }

    // faces first, then the logo / title / footer on top of them (paint handlers run in the order they were added)
    void AttachFaces()
    {
        faces.Attach();
        side.Paint -= PaintSide; side.Paint += PaintSide;
        header.Paint -= PaintHeader; header.Paint += PaintHeader;
    }

    void ChangeLanguage()
    {
        if (!FishaoBot.IsRunning) FishaoBot.Status = L.T("Zastaveno", "Stopped");
        BuildUi(pages.IndexOf(current));
        AttachFaces();   // the panels are new - draw the faces on them again
        shownCatches = -1; tick = -1;
        RefreshUi();
    }

    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); NativeLook.DarkTitleBar(this); }

    // Ctrl+1..5 switch tabs
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if ((keyData & Keys.Control) == Keys.Control)
        {
            int k = (int)(keyData & Keys.KeyCode) - (int)Keys.D1;
            if (k >= 0 && k < pages.Count) { ShowPage(k); return true; }
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    void ShowPage(int index)
    {
        var p = pages[index];
        if (p == current) return;
        SuspendLayout();
        p.Visible = true; p.BringToFront();
        if (current != null) current.Visible = false;
        current = p;
        for (int i = 0; i < navItems.Count; i++) navItems[i].Selected = i == index;
        ResumeLayout();
        header.Invalidate();
        p.OnShown();
        if (p == overview) tick = -1; // refresh the preview right away
    }

    void LayoutHeader()
    {
        int y = 28;
        startBtn.Location = new Point(header.Width - startBtn.Width, y);
        status.Location = new Point(startBtn.Left - status.Width - 12, y + (startBtn.Height - status.Height) / 2);
    }

    void PaintHeader(object sender, PaintEventArgs e)
    {
        if (current == null) return;
        var g = e.Graphics;
        int maxW = Math.Max(100, status.Left - 20);
        using (var f = Theme.F(20f, true)) TextRenderer.DrawText(g, current.Title, f, new Rectangle(-3, 22, maxW, 40), Theme.Fg, TextFormatFlags.EndEllipsis);
        using (var f = Theme.F(9.5f)) TextRenderer.DrawText(g, current.Subtitle, f, new Rectangle(0, 60, maxW, 22), Theme.Muted, TextFormatFlags.EndEllipsis);
    }

    void PaintSide(object sender, PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var pen = new Pen(Theme.Line)) g.DrawLine(pen, side.Width - 1, 0, side.Width - 1, side.Height);

        // logo: the face on a coloured rounded square (a fish when the picture is missing)
        var logo = new RectangleF(22, 28, 44, 44);
        using (var path = Theme.Round(logo, 13))
        {
            using (var b = new LinearGradientBrush(logo, Theme.Accent, Theme.Blue, 45f)) g.FillPath(b, path);
            var face = FaceImage.Get();
            if (face != null)
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SetClip(path);
                float h = 50, w = h * face.Width / face.Height;
                g.DrawImage(face, logo.X + (logo.Width - w) / 2, logo.Y + 2, w, h);
                g.ResetClip();
            }
            else Icons.Draw(g, IconKind.Fish, 30, 36, 28, Color.White, 2.2f);
        }
        using (var f = Theme.F(14f, true)) TextRenderer.DrawText(g, "Fishao Bot", f, new Point(76, 28), Theme.Fg);
        using (var f = Theme.F(8.5f)) TextRenderer.DrawText(g, L.T("chytání na pozadí", "fishing in the background"), f, new Point(78, 54), Theme.Muted);

        using (var f = Theme.F(8f, true)) TextRenderer.DrawText(g, "MENU", f, new Point(24, 86), Theme.Dim);

        // footer: is the game there?
        int fy = side.Height - 112;
        Theme.FillRound(g, new RectangleF(14, fy, side.Width - 29, 92), 12, Theme.Card);
        var dot = gameFound ? Theme.Accent : Theme.Danger;
        using (var b = new SolidBrush(dot)) g.FillEllipse(b, 30, fy + 21, 9, 9);
        using (var f = Theme.F(9.5f, true)) TextRenderer.DrawText(g, gameFound ? L.T("Hra připojena", "Game connected") : L.T("Hra nenalezena", "Game not found"), f, new Point(46, fy + 15), Theme.Fg);
        using (var f = Theme.F(8.5f)) TextRenderer.DrawText(g, gameFound ? L.T("okno Fishao je otevřené", "the Fishao window is open") : L.T("spusť Fishao (ne minimalizovat)", "start Fishao (don't minimise)"), f, new Point(30, fy + 40), Theme.Muted);
        // F10 key cap
        var key = new RectangleF(30, fy + 62, 34, 20);
        Theme.FillRound(g, key, 5, Theme.Card2);
        using (var f = Theme.F(8f, true)) TextRenderer.DrawText(g, "F10", f, Rectangle.Round(key), Theme.Fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        using (var f = Theme.F(8.5f)) TextRenderer.DrawText(g, L.T("start / stop odkudkoli", "start / stop from anywhere"), f, new Point(70, fy + 64), Theme.Muted);
    }

    // ---------------------------------------------------------------- events from the bot thread
    void OnLog(string line)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { try { BeginInvoke(new Action<string>(OnLog), line); } catch { } return; }
        logLines.Add(line);
        if (logLines.Count > 1000) logLines.RemoveAt(0);
        logPage.Add(line);
    }

    // the watchdog stopped the bot (game crashed / frozen / disconnected): Windows notification + blinking taskbar
    void OnProblem(string message)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { try { BeginInvoke(new Action<string>(OnProblem), message); } catch { } return; }
        try { tray.ShowBalloonTip(15000, L.T("Fishao Bot se zastavil", "Fishao Bot stopped"), message, ToolTipIcon.Warning); } catch { }
        NativeLook.Flash(this);
        ShowPage(3);   // the log, so the reason is right there
    }

    void ShowFromTray()
    {
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Show(); Activate();
    }

    void OnState()
    {
        if (IsDisposed) return;
        if (InvokeRequired) { try { BeginInvoke(new Action(OnState)); } catch { } return; }
        RefreshUi();
    }

    void RefreshUi()
    {
        bool run = FishaoBot.IsRunning;
        if (run && startedAt == null) startedAt = DateTime.Now;
        if (!run && startedAt != null) { runTotal += DateTime.Now - startedAt.Value; startedAt = null; }
        var total = runTotal + (startedAt != null ? DateTime.Now - startedAt.Value : TimeSpan.Zero);

        // game picture: ~1x a second on the overview, otherwise every 5 s just to know whether the game is there
        tick++;
        bool onOverview = current == overview;
        if (tick % (onOverview ? 4 : 20) == 0)
        {
            Bitmap shot = null;
            try { shot = FishaoBot.CaptureBitmap(); } catch { }
            bool found = shot != null;
            if (found != gameFound) { gameFound = found; side.Invalidate(new Rectangle(0, side.Height - 120, side.Width, 120)); }
            if (onOverview) overview.SetPreview(shot);
            if (shot != null) shot.Dispose();
        }

        var s = Snapshot.Take(total, gameFound);
        bool changed = s.Catches.Count != shownCatches;
        shownCatches = s.Catches.Count;

        startBtn.Set(run ? "Stop" : "Start", run ? Theme.Danger : Theme.Accent, run ? IconKind.Stop : IconKind.Play);
        startBtn.ForeColor = run ? Color.White : Theme.Bg;
        Color dot = run ? Theme.Accent : (s.Status.StartsWith("Chyba") || s.Status.StartsWith("Error") ? Theme.Danger : Theme.Muted);
        if (run && (s.Status.StartsWith("Došl") || s.Status.Contains("zavři") || s.Status.Contains("Čekám") || s.Status.StartsWith("Out of") || s.Status.Contains("close it") || s.Status.StartsWith("Waiting for energy"))) dot = Theme.Orange;
        status.Set(s.Status, dot, run);
        navItems[1].SetBadge(s.Fish > 0 ? s.Fish.ToString() : "");

        foreach (var p in pages)
            if (p == current || changed) p.UpdateData(s, changed);
    }

    // ---------------------------------------------------------------- updates (GitHub Releases)
    bool updateBusy;

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (Updater.AutoCheck) CheckForUpdate(false);
    }

    // manual = the user pressed "Check now" (then also "you have the newest" / errors are shown)
    void CheckForUpdate(bool manual)
    {
        if (updateBusy) return;
        if (!Updater.Configured)
        {
            if (manual) settingsPage.SetUpdateStatus(L.T("GitHub repozitář není nastavený.", "No GitHub repository set."), Theme.Orange);
            return;
        }
        updateBusy = true; settingsPage.UpdateButtonEnabled = false;
        settingsPage.SetUpdateStatus(L.T("Hledám novou verzi…", "Looking for a new version…"), Theme.Muted);
        new Thread(delegate ()
        {
            Updater.Release r = null; string err = null;
            try { r = Updater.CheckForNewer(); } catch (Exception ex) { err = ex.Message; }
            try { BeginInvoke(new Action(delegate { UpdateChecked(r, err, manual); })); } catch { }
        }) { IsBackground = true }.Start();
    }

    void UpdateChecked(Updater.Release r, string err, bool manual)
    {
        updateBusy = false; settingsPage.UpdateButtonEnabled = true;
        if (err != null)
        {
            settingsPage.SetUpdateStatus(L.T("Nepodařilo se spojit s GitHubem.", "Could not reach GitHub."), manual ? Theme.Danger : Theme.Muted);
            if (manual) OnLog(DateTime.Now.ToString("HH:mm:ss") + L.T("  Aktualizace: ", "  Update: ") + err);
            return;
        }
        if (r == null) { settingsPage.SetUpdateStatus(L.T("Máš nejnovější verzi.", "You have the newest version."), Theme.Accent); return; }

        settingsPage.SetUpdateStatus(L.T("Nová verze ", "New version ") + r.Version + L.T(" je k dispozici.", " is available."), Theme.Orange);
        string notes = r.Notes.Length > 700 ? r.Notes.Substring(0, 700) + "…" : r.Notes;
        string msg = L.T("Je tu nová verze Fishao Bota: ", "A new version of Fishao Bot is here: ") + r.Version +
                     L.T("  (máš " , "  (you have ") + Updater.Version + ")\n\n" +
                     (notes.Length > 0 ? L.T("Co je nového:\n", "What's new:\n") + notes + "\n\n" : "") +
                     L.T("Stáhnout a restartovat teď?", "Download and restart now?");
        if (MessageBox.Show(this, msg, "Fishao Bot", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes) return;
        InstallUpdate(r);
    }

    void InstallUpdate(Updater.Release r)
    {
        updateBusy = true; settingsPage.UpdateButtonEnabled = false;
        if (FishaoBot.IsRunning) FishaoBot.Stop();
        OnLog(DateTime.Now.ToString("HH:mm:ss") + L.T("  Stahuji verzi ", "  Downloading version ") + r.Version + "…");
        new Thread(delegate ()
        {
            string exe = null, err = null; int lastPct = -1;
            try
            {
                exe = Updater.Download(r, delegate (int pct)
                {
                    if (pct == lastPct) return; lastPct = pct;
                    try { BeginInvoke(new Action(delegate { settingsPage.SetUpdateStatus(L.T("Stahuji… ", "Downloading… ") + pct + " %", Theme.Blue); })); } catch { }
                });
            }
            catch (Exception ex) { err = ex.Message; }
            try
            {
                BeginInvoke(new Action(delegate
                {
                    updateBusy = false; settingsPage.UpdateButtonEnabled = true;
                    if (err != null)
                    {
                        settingsPage.SetUpdateStatus(L.T("Aktualizace se nepovedla.", "The update failed."), Theme.Danger);
                        OnLog(DateTime.Now.ToString("HH:mm:ss") + L.T("  Aktualizace se nepovedla: ", "  The update failed: ") + err);
                        return;
                    }
                    try { Updater.Restart(exe); Close(); }
                    catch (Exception ex) { OnLog(DateTime.Now.ToString("HH:mm:ss") + "  " + ex.Message); }
                }));
            }
            catch { }
        }) { IsBackground = true }.Start();
    }

    // ---------------------------------------------------------------- picking the cast points
    // the user marks the wanted bait in a picture of the game (the inventory or the shop must be open there)
    void PickBait()
    {
        var shot = FishaoBot.CaptureBitmap();
        if (shot == null) { MessageBox.Show(this, L.T("Hra neběží nebo je minimalizovaná.", "The game is not running or is minimised."), "Fishao Bot", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        using (var dlg = new BaitPickForm(shot))   // the form disposes the shot
        {
            if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Result != null)
            {
                try
                {
                    dlg.Result.Save(FishaoBot.BaitPictureFile, System.Drawing.Imaging.ImageFormat.Png);
                    OnLog(DateTime.Now.ToString("HH:mm:ss") + L.T("  Návnada vybrána.", "  Bait chosen."));
                }
                catch (Exception ex) { OnLog(DateTime.Now.ToString("HH:mm:ss") + "  " + ex.Message); }
                dlg.Result.Dispose();
                settingsPage.ShowBait();
            }
        }
    }

    void PickPoints()
    {
        var shot = FishaoBot.CaptureBitmap();
        if (shot == null) { MessageBox.Show(this, L.T("Hra neběží nebo je minimalizovaná.", "The game is not running or is minimised."), "Fishao Bot", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        using (var dlg = new PickForm(shot, FishaoBot.CastPoints, FishaoBot.CastArea, FishaoBot.CastUseArea))
        {
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                FishaoBot.CastPoints = dlg.Points.ToArray();
                FishaoBot.CastArea = dlg.Area.ToArray();
                FishaoBot.CastUseArea = dlg.UseArea;
                FishaoBot.SaveCastView(shot);   // to notice later that the bot runs at another place
                OnLog(DateTime.Now.ToString("HH:mm:ss") + (dlg.UseArea
                    ? (dlg.Area.Count >= 3 ? L.T("  Uložena oblast nahazování - bot nahodí náhodně uvnitř.", "  Cast area saved - the bot casts at random inside it.")
                                           : L.T("  Oblast nahazování není nakreslená.", "  No cast area drawn."))
                    : L.T("  Uloženo míst nahození: ", "  Cast points saved: ") + dlg.Points.Count));
                AppSettings.Save();
            }
        }
        RefreshUi();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        uiTimer.Stop();
        if (faces != null) faces.Dispose();
        FishaoBot.Problem -= OnProblem;
        if (tray != null) { tray.Visible = false; tray.Dispose(); }
        FishaoBot.Stop();
        FishaoBot.Quit();
        AppSettings.Save();
        if (botThread != null) botThread.Join(1500);
        base.OnFormClosing(e);
    }
}
