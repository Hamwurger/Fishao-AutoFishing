// Everything the pages show, computed once per refresh (4x a second) from the bot's state.
using System;
using System.Collections.Generic;

public class Snapshot
{
    public bool Running;
    public string Status = "";
    public TimeSpan RunTime;
    public List<CatchInfo> Catches;   // this session, oldest first
    public int Fish, Escaped, Points;
    public int[] Stars = new int[6];
    public CatchInfo Best;            // biggest fish of this session
    public CatchInfo Record;          // biggest fish ever
    public int AllTimeFish, Games, NoFish, EnergyBought, CastPoints;
    public bool GameFound;
    public bool CastUseArea, HasArea;   // casting into a drawn area instead of points

    public static Snapshot Take(TimeSpan runTime, bool gameFound)
    {
        var s = new Snapshot
        {
            Running = FishaoBot.IsRunning, Status = FishaoBot.Status ?? "", RunTime = runTime,
            Catches = FishaoBot.SessionCatches, Record = FishaoBot.Record, AllTimeFish = FishaoBot.AllTimeFish,
            Games = FishaoBot.Games, NoFish = FishaoBot.NoFish, EnergyBought = FishaoBot.EnergyBought,
            CastPoints = FishaoBot.CastPoints.Length, GameFound = gameFound,
            CastUseArea = FishaoBot.CastUseArea, HasArea = FishaoBot.CastArea.Length >= 3
        };
        foreach (var c in s.Catches)
        {
            if (c.Escaped) { s.Escaped++; continue; }
            s.Fish++; s.Points += c.Points; s.Stars[Math.Max(0, Math.Min(5, c.Stars))]++;
            if (s.Best == null || c.Cm > s.Best.Cm) s.Best = c;
        }
        return s;
    }

    public string RunTimeText { get { return ((int)RunTime.TotalHours).ToString() + RunTime.ToString(@"\:mm\:ss"); } }
    public string SuccessText { get { int n = Fish + Escaped; return n == 0 ? "–" : (100 * Fish / n) + " %"; } }
}
