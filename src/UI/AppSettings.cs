// FishaoBot.ini next to the exe - the user's settings. Reads/writes the bot's fields directly.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

public static class AppSettings
{
    public static bool FloatingFaces = true;   // the fun faces in the window background

    static string FilePath { get { return Path.Combine(FishaoBot.DebugDir, "FishaoBot.ini"); } }

    public static void Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return;
            foreach (var line in File.ReadAllLines(FilePath))
            {
                int i = line.IndexOf('='); if (i < 0) continue;
                string k = line.Substring(0, i).Trim(), v = line.Substring(i + 1).Trim();
                int n; bool num = int.TryParse(v, out n);
                switch (k)
                {
                    case "cast":
                        var list = new List<Point>();
                        foreach (var item in v.Split(';'))
                        {
                            var parts = item.Split(','); int x, y;
                            if (parts.Length == 2 && int.TryParse(parts[0], out x) && int.TryParse(parts[1], out y)) list.Add(new Point(x, y));
                        }
                        FishaoBot.CastPoints = list.ToArray();
                        break;
                    case "castarea":
                        var area = new List<Point>();
                        foreach (var item in v.Split(';'))
                        {
                            var parts = item.Split(','); int x, y;
                            if (parts.Length == 2 && int.TryParse(parts[0], out x) && int.TryParse(parts[1], out y)) area.Add(new Point(x, y));
                        }
                        FishaoBot.CastArea = area.ToArray();
                        break;
                    case "castmode": FishaoBot.CastUseArea = v == "area"; break;
                    case "hover": if (num) FishaoBot.CastHoverMs = Clamp(n, 200, 3000); break;
                    case "timeout": if (num) FishaoBot.BiteTimeoutMs = Clamp(n, 20, 600) * 1000; break;
                    case "after": if (num) FishaoBot.AfterGameDelayMs = Clamp(n, 1, 20) * 1000; break;
                    case "buyenergy": FishaoBot.AutoBuyEnergy = v == "1"; break;
                    case "energywait": if (num) FishaoBot.EnergyWaitMinutes = Clamp(n, 1, 60); break;
                    case "energypack": if (num) { int pi = Array.IndexOf(FishaoBot.EnergyPacks, n); if (pi >= 0) FishaoBot.EnergyPack = pi; } break;
                    case "lang": L.Code = v; break;
                    case "faces": FloatingFaces = v != "0"; break;
                    case "autobait": FishaoBot.AutoBait = v != "0"; break;
                    case "baitsource": FishaoBot.BaitFromShop = v == "shop"; break;
                    case "baitpay": FishaoBot.BaitPay = v == "gold" ? 1 : v == "any" ? 2 : 0; break;
                    case "baitpacks": if (num) FishaoBot.BaitPacks = Clamp(n, 1, 10); break;
                    case "autoupdate": Updater.AutoCheck = v != "0"; break;
                    case "updaterepo": if (v.Length > 0) Updater.Repo = v; break;
                }
            }
        }
        catch { }
    }

    public static void Save()
    {
        try
        {
            var parts = new List<string>();
            foreach (var cp in FishaoBot.CastPoints) parts.Add(cp.X + "," + cp.Y);
            var areaParts = new List<string>();
            foreach (var ap in FishaoBot.CastArea) areaParts.Add(ap.X + "," + ap.Y);
            File.WriteAllLines(FilePath, new[] {
                "cast=" + string.Join(";", parts.ToArray()),
                "castarea=" + string.Join(";", areaParts.ToArray()),
                "castmode=" + (FishaoBot.CastUseArea ? "area" : "points"),
                "hover=" + FishaoBot.CastHoverMs,
                "timeout=" + FishaoBot.BiteTimeoutMs / 1000,
                "after=" + FishaoBot.AfterGameDelayMs / 1000,
                "buyenergy=" + (FishaoBot.AutoBuyEnergy ? 1 : 0),
                "energywait=" + FishaoBot.EnergyWaitMinutes,
                "energypack=" + FishaoBot.EnergyPacks[FishaoBot.EnergyPack],
                "lang=" + L.Code,
                "faces=" + (FloatingFaces ? 1 : 0),
                "autobait=" + (FishaoBot.AutoBait ? 1 : 0),
                "baitsource=" + (FishaoBot.BaitFromShop ? "shop" : "bag"),
                "baitpay=" + (FishaoBot.BaitPay == 1 ? "gold" : FishaoBot.BaitPay == 2 ? "any" : "green"),
                "baitpacks=" + FishaoBot.BaitPacks,
                "autoupdate=" + (Updater.AutoCheck ? 1 : 0),
                "updaterepo=" + (Updater.Repo == Updater.DefaultRepo ? "" : Updater.Repo) });
        }
        catch { }
    }

    static int Clamp(int v, int min, int max) { return Math.Max(min, Math.Min(max, v)); }
}
