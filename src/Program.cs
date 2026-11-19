// FishaoBot.exe - starts the window. The bot itself is in Bot\, the window in UI\.
using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        FishaoBot.DebugDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        FishaoBot.GuiMode = true;
        FishaoBot.LoadStats();
        AppSettings.Load();                                   // also sets the language
        FishaoBot.Status = L.T("Zastaveno", "Stopped");
        Updater.CleanupOld();                                 // the previous exe after an update
        int page = 0; // "FishaoBot.exe 3" opens the 4th tab (used for screenshots)
        if (args.Length > 0) int.TryParse(args[0], out page);
        if (args.Length == 0) Application.Run(new SplashForm());   // fun loading screen (skipped for screenshots)
        Application.Run(new MainForm(page));
    }
}
