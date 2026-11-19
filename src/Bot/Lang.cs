// Language of the window and of the log: Czech or English.
// Every text is written as L.T("česky", "in English") right where it is used.
using System.Globalization;

public static class L
{
    // default = Czech on Czech/Slovak Windows, otherwise English (FishaoBot.ini "lang=" overrides it)
    public static bool English = !(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "cs" || CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "sk");

    public static string Code { get { return English ? "en" : "cs"; } set { English = value == "en"; } }

    public static string T(string cs, string en) { return English ? en : cs; }

    // Czech plural: 1 ryba, 2-4 ryby, 5+ ryb
    public static string N(int n, string cs1, string cs234, string cs5, string en1, string enMany)
    {
        if (English) return n + " " + (n == 1 ? en1 : enMany);
        return n + " " + (n == 1 ? cs1 : (n >= 2 && n <= 4 ? cs234 : cs5));
    }
}
