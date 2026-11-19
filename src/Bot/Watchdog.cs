// Watchdog: stops the bot and tells the user (Problem event -> Windows notification in the window app) when
//   1) the game window is gone (the game crashed or was closed),
//   2) the game picture has not changed for a long time (frozen / disconnected - normally something always
//      moves, the game draws ~30 different pictures a second),
//   3) a window in the game can't be closed again and again (e.g. a "connection lost" message).
using System;

public static partial class FishaoBot
{
    public static event Action<string> Problem;    // the reason, already translated

    public class GameGoneException : Exception { public GameGoneException(string m) : base(m) { } }
    public class GameFrozenException : Exception { public GameFrozenException(string m) : base(m) { } }

    const int FrozenMs = 60000;          // no change in the picture for this long = frozen
    const int FrozenMinGrabs = 30;       // ... seen on at least this many pictures (long waits don't count)
    const int MaxDialogFailures = 3;     // windows that could not be closed, in a row

    static long picHash, picChangedAt; static int picSameCount;
    static int dialogFailures;

    static void ResetWatchdog() { picHash = 0; picChangedAt = clock.ElapsedMilliseconds; picSameCount = 0; dialogFailures = 0; }

    // stops the bot and lets the window show a notification
    static void ReportProblem(string message)
    {
        Log(L.T("POZOR: ", "WARNING: ") + message);
        Stop();
        SetStatus(message);
        var h = Problem; if (h != null) try { h(message); } catch { }
    }

    // called for every full picture of the game: notices a picture that stopped changing
    static void WatchPicture(Frame f)
    {
        long h = 17;
        for (int i = 0; i < f.Px.Length; i += 257) h = h * 31 + f.Px[i];
        long now = clock.ElapsedMilliseconds;
        if (h != picHash) { picHash = h; picChangedAt = now; picSameCount = 0; return; }
        picSameCount++;
        if (running && picSameCount >= FrozenMinGrabs && now - picChangedAt > FrozenMs)
        {
            picChangedAt = now; picSameCount = 0;
            throw new GameFrozenException(L.T("Hra se už minutu vůbec nehýbe - nejspíš je odpojená nebo zamrzlá. Bot jsem zastavil.",
                                              "The game hasn't moved at all for a minute - probably disconnected or frozen. The bot was stopped."));
        }
    }

    // a window in the game could not be closed; several in a row = something is wrong (e.g. disconnected)
    static void DialogNotClosed()
    {
        if (++dialogFailures >= MaxDialogFailures)
        {
            dialogFailures = 0;
            ReportProblem(L.T("Ve hře opakovaně visí okno, které neumím zavřít (třeba odpojení od serveru). Bot jsem zastavil.",
                              "A window in the game can't be closed again and again (e.g. disconnected from the server). The bot was stopped."));
        }
    }

    static void DialogsOk() { dialogFailures = 0; }
}
