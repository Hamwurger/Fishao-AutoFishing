// Fishao auto-fishing bot - background mode.
// Reads the picture straight from the game window (PrintWindow) and sends clicks/keys straight to it
// (PostMessage), so the real mouse/keyboard stay free and the game window may be covered by other windows.
// It must NOT be minimised. C# 5 compatible (compiled by Windows PowerShell 5.1 Add-Type).
// All coordinates are client coordinates of the game window.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;

public static partial class FishaoBot
{
    // ---------------- WinAPI ----------------
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] static extern bool ScreenToClient(IntPtr h, ref POINT p);
    [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern uint MapVirtualKey(uint code, uint type);

    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }

    const uint WM_KEYDOWN = 0x100, WM_KEYUP = 0x101, WM_MOUSEMOVE = 0x200, WM_LBUTTONDOWN = 0x201, WM_LBUTTONUP = 0x202;
    const uint PW_RENDERFULLCONTENT = 0x2, PW_CLIENTONLY = 0x1;
    const int VK_A = 0x41, VK_D = 0x44;
    const int VK_F7 = 0x76, VK_F8 = 0x77, VK_F9 = 0x78, VK_F10 = 0x79, VK_F12 = 0x7B;

    // ---------------- settings ----------------
    public static int BiteTimeoutMs = 90000;   // safety net: no minigame within this time -> click again (reel in / recast)
    public static int AfterGameDelayMs = 4000; // how long to wait for the result dialog after the minigame
    public static int CastHoverMs = 600;       // "hold the mouse" over the water this long before the cast click
    public static string DebugDir = ".";

    // ---------------- state ----------------
    static IntPtr hwnd = IntPtr.Zero;
    static volatile object closePointBox = null; // boxed Point, set from any thread
    static volatile Point[] castPoints = new Point[0]; // replaced as a whole, so it is safe to read from any thread
    static int lastCastIndex = -1;
    static Random rnd = new Random();
    static Point? castPoint; // the point used for the current cast
    static Point? closePoint { get { var o = closePointBox; return o == null ? (Point?)null : (Point)o; } set { closePointBox = value.HasValue ? (object)value.Value : null; } }
    static volatile bool running = false, quit = false;
    static bool aDown = false, dDown = false; static long lastKeyRepeat = 0;
    static int games = 0, noFish = 0, dialogsClosed = 0;
    static Dictionary<int, bool> prevKeys = new Dictionary<int, bool>();
    static Stopwatch clock = Stopwatch.StartNew();

    // ---------------- public API (used by the GUI) ----------------
    public static event Action<string> LogMessage;
    public static event Action StateChanged;
    public static bool GuiMode = false;
    public static volatile string Status = L.T("Zastaveno", "Stopped");
    public static bool IsRunning { get { return running; } }
    public static int Games { get { return games; } }
    public static int NoFish { get { return noFish; } }
    public static int DialogsClosed { get { return dialogsClosed; } }
    public static Point[] CastPoints { get { return (Point[])castPoints.Clone(); } set { castPoints = value == null ? new Point[0] : (Point[])value.Clone(); lastCastIndex = -1; Changed(); } }
    public static void AddCastPoint(Point p) { var l = new List<Point>(castPoints); l.Add(p); CastPoints = l.ToArray(); }

    // cast area: a drawn outline on the water - the bot casts to a random point inside it
    static volatile Point[] castArea = new Point[0];
    public static Point[] CastArea { get { return (Point[])castArea.Clone(); } set { castArea = value == null ? new Point[0] : (Point[])value.Clone(); Changed(); } }
    public static volatile bool CastUseArea = false;   // true = area, false = the picked points
    public static bool HasCastTarget { get { return CastUseArea ? castArea.Length >= 3 : castPoints.Length > 0; } }

    static bool InArea(Point[] poly, double x, double y)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            if ((poly[i].Y > y) != (poly[j].Y > y) &&
                x < (poly[j].X - poly[i].X) * (y - poly[i].Y) / (double)(poly[j].Y - poly[i].Y) + poly[i].X)
                inside = !inside;
        return inside;
    }

    static Point? RandomInArea(Point[] poly)
    {
        int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
        foreach (var p in poly) { x0 = Math.Min(x0, p.X); y0 = Math.Min(y0, p.Y); x1 = Math.Max(x1, p.X); y1 = Math.Max(y1, p.Y); }
        for (int t = 0; t < 500; t++)
        {
            int x = rnd.Next(x0, x1 + 1), y = rnd.Next(y0, y1 + 1);
            if (InArea(poly, x, y)) return new Point(x, y);
        }
        return new Point((x0 + x1) / 2, (y0 + y1) / 2);   // a very thin outline - its middle
    }

    // a random point, different from the previous one when there is a choice
    static Point? NextCastPoint()
    {
        if (CastUseArea) { var area = castArea; return area.Length >= 3 ? RandomInArea(area) : null; }
        var pts = castPoints;
        if (pts.Length == 0) return null;
        int i = rnd.Next(pts.Length);
        if (pts.Length > 1 && i == lastCastIndex) i = (i + 1 + rnd.Next(pts.Length - 1)) % pts.Length;
        lastCastIndex = i;
        return pts[i];
    }
    public static Point? ClosePoint { get { return closePoint; } set { closePoint = value; Changed(); } }
    public static void Start()
    {
        if (!HasCastTarget) { Log(L.T("Nejdřív vyber místa nebo oblast nahození (Nastavení).", "Pick cast points or a cast area first (Settings).")); return; }
        if (!running) { noMoneyUntil = 0; ResetWatchdog(); running = true; Log(L.T("SPUŠTĚNO", "STARTED")); Changed(); }
    }
    public static void Stop() { if (running) { running = false; SetStatus(L.T("Zastaveno", "Stopped")); Log(L.T("ZASTAVENO", "STOPPED")); Changed(); } }
    public static void Quit() { running = false; quit = true; }

    static void Changed() { var h = StateChanged; if (h != null) try { h(); } catch { } }
    static void SetStatus(string s) { Status = s; Changed(); }

    static void Log(string s)
    {
        string line = DateTime.Now.ToString("HH:mm:ss") + "  " + s;
        if (!GuiMode) Console.WriteLine(line);
        var h = LogMessage; if (h != null) try { h(line); } catch { }
    }

    static string lastError = ""; static long lastErrorAt = -999999;

    public static void Run()
    {
        SetProcessDPIAware();
        if (!GuiMode)
        {
            Log("Fishao bot (background mode). Hotkeys work from any window:");
            Log("  F8  = ADD a cast point (hover the mouse over water in rod range, in the game window)");
            Log("  F9  = record CLOSE point for the result dialog (only if the X is not found automatically)");
            Log("  F10 = start / pause");
            Log("  F7  = save debug screenshot");
            Log("  F12 = quit");
            Log("The game window may be covered by other windows, but must not be minimised.");
        }
        while (!quit)
        {
            HandleHotkeys();
            if (running)
            {
                try { Cycle(); if (!running) { Release(); SetStatus(L.T("Zastaveno", "Stopped")); } GC.Collect(); }
                catch (GameGoneException ex) { Release(); ReportProblem(ex.Message); }
                catch (GameFrozenException ex) { Release(); ReportProblem(ex.Message); }
                catch (Exception ex)
                {
                    // the same error again and again (e.g. the game minimised) is written only once a minute
                    if (ex.Message != lastError || clock.ElapsedMilliseconds - lastErrorAt > 60000)
                    { Log(L.T("Chyba: ", "Error: ") + ex.Message); lastError = ex.Message; lastErrorAt = clock.ElapsedMilliseconds; }
                    SetStatus(L.T("Chyba: ", "Error: ") + ex.Message); Release(); Thread.Sleep(1000);
                }
            }
            else { if (aDown || dDown) Release(); Thread.Sleep(30); }
        }
        Release();
        Log(L.T("Konec. Odehraných miniher: ", "End. Minigames played: ") + games);
    }

    // full picture of the game window for the GUI (separate bitmap, safe to call from the GUI thread)
    public static Bitmap CaptureBitmap()
    {
        IntPtr h = IntPtr.Zero;
        foreach (var p in Process.GetProcessesByName("FishaoGame"))
            if (p.MainWindowHandle != IntPtr.Zero) { h = p.MainWindowHandle; break; }
        if (h == IntPtr.Zero || IsIconic(h)) return null;
        RECT r; if (!GetClientRect(h, out r) || r.R <= 0 || r.B <= 0) return null;
        var bmp = new Bitmap(r.R, r.B, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp)) { IntPtr hdc = g.GetHdc(); PrintWindow(h, hdc, PW_RENDERFULLCONTENT | PW_CLIENTONLY); g.ReleaseHdc(hdc); }
        return bmp;
    }

    // ---------------- hotkeys ----------------
    static bool Pressed(int vk)
    {
        bool down = (GetAsyncKeyState(vk) & 0x8000) != 0;
        bool was; prevKeys.TryGetValue(vk, out was);
        prevKeys[vk] = down;
        return down && !was;
    }

    static Point? CursorInGame()
    {
        try { FindWindow(); } catch (Exception ex) { Log(ex.Message); return null; }
        POINT p; GetCursorPos(out p); ScreenToClient(hwnd, ref p);
        RECT r; GetClientRect(hwnd, out r);
        if (p.X < 0 || p.Y < 0 || p.X >= r.R || p.Y >= r.B) { Log("The mouse is not over the game window."); return null; }
        return new Point(p.X, p.Y);
    }

    static void HandleHotkeys()
    {
        if (Pressed(VK_F12)) { if (GuiMode) Stop(); else { quit = true; running = false; } }
        if (Pressed(VK_F8)) { var p = CursorInGame(); if (p != null) { AddCastPoint(p.Value); Log(L.T("Přidáno místo nahození ", "Added cast point ") + p.Value.X + "," + p.Value.Y + L.T(" (celkem ", " (total ") + castPoints.Length + ")"); } }
        if (Pressed(VK_F9)) { var p = CursorInGame(); if (p != null) { ClosePoint = p; Log(L.T("Místo zavření okna = ", "Close point = ") + p.Value.X + "," + p.Value.Y); } }
        if (Pressed(VK_F7)) SaveDebug();
        if (Pressed(VK_F10))
        {
            if (running) Stop(); else Start();
            Release();
        }
    }

    // sleeps while still reacting to hotkeys and keeping held keys alive; returns false if paused/quit
    static bool Wait(int ms)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ms) { HandleHotkeys(); if (!running) return false; RepeatKeys(); Thread.Sleep(15); }
        return true;
    }
}
