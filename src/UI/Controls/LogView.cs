// The bot's log: "HH:mm:ss  message", catches green, problems red, the rest normal.
using System;
using System.Drawing;
using System.Windows.Forms;

public class LogView : ListBox
{
    const int MaxLines = 1000;

    public LogView()
    {
        DrawMode = DrawMode.OwnerDrawFixed; ItemHeight = 24;
        BackColor = Theme.Card; ForeColor = Theme.Fg; BorderStyle = BorderStyle.None; IntegralHeight = false;
        Font = Theme.Mono(9.5f); HorizontalScrollbar = false;
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        NativeLook.DarkScrollBars(this);
    }

    public void Add(string line)
    {
        bool atEnd = TopIndex >= Items.Count - Math.Max(1, ClientSize.Height / ItemHeight) - 1;
        Items.Add(line);
        while (Items.Count > MaxLines) Items.RemoveAt(0);
        if (atEnd) TopIndex = Math.Max(0, Items.Count - 1);
    }

    public string AllText()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var o in Items) sb.AppendLine(o.ToString());
        return sb.ToString();
    }

    static Color ColorOf(string msg)
    {
        string m = msg.ToUpperInvariant();
        if (m.StartsWith("ÚLOVEK") || m.Contains("NOVÁ ÚROVEŇ") || m.Contains("REKORD") || m.StartsWith("CATCH:") || m.StartsWith("LEVEL UP") || m.Contains("NEW RECORD")) return Theme.Accent;
        if (m.StartsWith("POZOR") || m.StartsWith("WARNING") || m.Contains("CHYBA") || m.Contains("NEJDE") || m.Contains("NENÍ") || m.Contains("DOŠL") || m.Contains("UTEKL") || m.Contains("UTRHL") || m.StartsWith("ERROR") || m.StartsWith("OUT OF") || m.StartsWith("COULD NOT") || m.Contains("GOT AWAY") || m.Contains("FAILED")) return Theme.Danger;
        if (m.Contains("ENERGI") || m.Contains("ENERGY")) return Theme.Blue;
        if (msg.StartsWith("SPUŠTĚNO") || msg.StartsWith("ZASTAVENO") || msg.StartsWith("STARTED") || msg.StartsWith("STOPPED")) return Theme.Gold;
        return Theme.Fg;
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        var g = e.Graphics; var r = e.Bounds;
        using (var b = new SolidBrush(Theme.Card)) g.FillRectangle(b, r);
        if (e.Index < 0 || e.Index >= Items.Count) return;
        string s = Items[e.Index].ToString();
        // lines look like "14:05:33  text"
        string time = "", msg = s;
        if (s.Length > 10 && s[2] == ':' && s[5] == ':') { time = s.Substring(0, 8); msg = s.Substring(8).TrimStart(); }
        int x = r.X + 8;
        if (time.Length > 0)
        {
            TextRenderer.DrawText(g, time, Font, new Rectangle(x, r.Y, 80, r.Height), Theme.Dim, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            x += TextRenderer.MeasureText(g, time + "  ", Font, Size.Empty, TextFormatFlags.NoPadding).Width;
        }
        var c = ColorOf(msg);
        if (c != Theme.Fg) using (var b = new SolidBrush(c)) g.FillEllipse(b, x, r.Y + r.Height / 2 - 3, 6, 6);
        x += 14;
        TextRenderer.DrawText(g, msg, Font, new Rectangle(x, r.Y, r.Right - x - 4, r.Height), c == Theme.Fg ? Theme.Fg : Theme.Mix(c, Theme.Fg, 0.25f),
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
    }
}
