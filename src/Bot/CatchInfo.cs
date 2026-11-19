// One catch (or escaped fish) - a line of catches.csv.
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

public class CatchInfo
{
    public DateTime Time;
    public string Name = "";
    public int Cm, Stars, Points;
    public bool Escaped;

    public string ToCsv()
    {
        return Time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + ";" + Name.Replace(";", ",") + ";" + Cm + ";" + Stars + ";" + Points + ";" + (Escaped ? 1 : 0);
    }

    public static CatchInfo FromCsv(string line)
    {
        var p = line.Split(';'); if (p.Length < 6) return null;
        var c = new CatchInfo();
        if (!DateTime.TryParseExact(p[0], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out c.Time)) return null;
        c.Name = p[1]; int.TryParse(p[2], out c.Cm); int.TryParse(p[3], out c.Stars); int.TryParse(p[4], out c.Points); c.Escaped = p[5] == "1";
        return c;
    }
}
