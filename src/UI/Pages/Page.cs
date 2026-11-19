// Base of every tab. The main window shows Title/Subtitle above it and calls UpdateData 4x a second.
using System.Drawing;
using System.Windows.Forms;

public abstract class Page : Panel
{
    public string Title, Subtitle;

    protected Page(string title, string subtitle)
    {
        Title = title; Subtitle = subtitle;
        Dock = DockStyle.Fill; BackColor = Theme.Bg; DoubleBuffered = true;
        Padding = new Padding(0);
    }

    // called on the UI thread; "changed" = the list of catches changed since the last call
    public virtual void UpdateData(Snapshot s, bool catchesChanged) { }
    public virtual void OnShown() { }

    // grid helper: TableLayoutPanel without its own background
    protected static TableLayoutPanel Grid(int cols, int rows)
    {
        return new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = cols, RowCount = rows, BackColor = Theme.Bg, Margin = new Padding(0), Padding = new Padding(0) };
    }

    // gap between grid cells: each cell gets half of it on the inner sides
    protected static Padding Gap(int col, int cols, int row, int rows, int gap = 14)
    {
        int h = gap / 2;
        return new Padding(col == 0 ? 0 : h, row == 0 ? 0 : h, col == cols - 1 ? 0 : h, row == rows - 1 ? 0 : h);
    }

    protected static T Place<T>(TableLayoutPanel grid, T c, int col, int row, int colSpan = 1) where T : Control
    {
        c.Dock = DockStyle.Fill;
        c.Margin = Gap(col, grid.ColumnCount, row, grid.RowCount);
        if (colSpan > 1) c.Margin = new Padding(c.Margin.Left, c.Margin.Top, Gap(col + colSpan - 1, grid.ColumnCount, row, grid.RowCount).Right, c.Margin.Bottom);
        grid.Controls.Add(c, col, row);
        if (colSpan > 1) grid.SetColumnSpan(c, colSpan);
        return c;
    }
}
