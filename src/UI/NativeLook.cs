// Windows 10/11 tweaks: dark title bar coloured like the window, dark scroll bars.
using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public static class NativeLook
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr hwnd, string app, string idList);

    const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19, DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    const int DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWA_BORDER_COLOR = 34, DWMWA_CAPTION_COLOR = 35, DWMWA_TEXT_COLOR = 36;

    static int Ref(System.Drawing.Color c) { return c.R | (c.G << 8) | (c.B << 16); }

    public static void DarkTitleBar(Form f)
    {
        try
        {
            int on = 1;
            if (DwmSetWindowAttribute(f.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, 4) != 0)
                DwmSetWindowAttribute(f.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref on, 4);
            // Windows 11 only (ignored on 10): title bar in the window colour, rounded corners
            int cap = Ref(Theme.Side), txt = Ref(Theme.Fg), border = Ref(Theme.Line), round = 2;
            DwmSetWindowAttribute(f.Handle, DWMWA_CAPTION_COLOR, ref cap, 4);
            DwmSetWindowAttribute(f.Handle, DWMWA_TEXT_COLOR, ref txt, 4);
            DwmSetWindowAttribute(f.Handle, DWMWA_BORDER_COLOR, ref border, 4);
            DwmSetWindowAttribute(f.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, 4);
        }
        catch { }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct FLASHWINFO { public uint cbSize; public IntPtr hwnd; public uint dwFlags; public uint uCount; public uint dwTimeout; }
    [DllImport("user32.dll")] static extern bool FlashWindowEx(ref FLASHWINFO info);

    // blinks the window's taskbar button until the user looks at the window
    public static void Flash(Form f)
    {
        try
        {
            var fi = new FLASHWINFO { hwnd = f.Handle, dwFlags = 3 | 12, uCount = uint.MaxValue, dwTimeout = 0 };   // ALL | TIMERNOFG
            fi.cbSize = (uint)Marshal.SizeOf(fi);
            FlashWindowEx(ref fi);
        }
        catch { }
    }

    // dark scroll bars on list boxes etc.
    public static void DarkScrollBars(Control c)
    {
        try
        {
            if (c.IsHandleCreated) SetWindowTheme(c.Handle, "DarkMode_Explorer", null);
            else c.HandleCreated += delegate { SetWindowTheme(c.Handle, "DarkMode_Explorer", null); };
        }
        catch { }
    }
}
