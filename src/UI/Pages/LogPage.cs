// "Záznam" - what the bot did, line by line.
using System;
using System.Drawing;
using System.Windows.Forms;

public class LogPage : Page
{
    readonly LogView log;
    readonly Card card;
    public event Action TestNotification;   // the main window shows a test notification

    public LogPage() : base(L.T("Záznam", "Log"), L.T("Co bot dělal - nejnovější dole", "What the bot did - newest at the bottom"))
    {
        var grid = Grid(1, 2);
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Margin = new Padding(0, 0, 0, 14), WrapContents = false };
        var copy = ModernButton.GhostButton(L.T("Kopírovat", "Copy"), IconKind.Copy); copy.Width = 140; copy.Margin = new Padding(0, 0, 10, 0);
        copy.Click += delegate { try { string t = log.AllText(); if (t.Length > 0) Clipboard.SetText(t); } catch { } };
        var clear = ModernButton.GhostButton(L.T("Vymazat", "Clear"), IconKind.Trash); clear.Width = 130; clear.Margin = new Padding(0, 0, 10, 0);
        clear.Click += delegate { log.Items.Clear(); card.SetHint(""); };
        // shows the same notification as when the watchdog stops the bot - but only as a test
        var test = ModernButton.GhostButton(L.T("Vyzkoušet upozornění", "Test the notification"), IconKind.Play); test.Width = 230; test.Margin = new Padding(0);
        test.Click += delegate { if (TestNotification != null) TestNotification(); };
        bar.Controls.Add(copy); bar.Controls.Add(clear); bar.Controls.Add(test);
        grid.Controls.Add(bar, 0, 0);

        card = new Card(L.T("Záznam", "Log"), IconKind.Log) { Dock = DockStyle.Fill, Margin = new Padding(0) };
        log = new LogView { Dock = DockStyle.Fill };
        card.Controls.Add(log);
        grid.Controls.Add(card, 0, 1);
        Controls.Add(grid);
    }

    public void Add(string line)
    {
        log.Add(line);
        card.SetHint(L.N(log.Items.Count, "řádek", "řádky", "řádků", "line", "lines"));
    }

    public string LastLine { get { return log.Items.Count == 0 ? "" : log.Items[log.Items.Count - 1].ToString(); } }
}
