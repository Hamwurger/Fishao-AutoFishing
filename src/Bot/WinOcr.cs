using System; using System.Drawing; using System.Drawing.Imaging; using System.IO; using System.Linq; using System.Reflection; using System.Threading.Tasks;

// Windows 10/11 built-in text recognition (Windows.Media.Ocr), used through reflection so the bot
// compiles with the plain .NET Framework compiler. Available = false if it is missing.
public static class WinOcr
{
    static object engine; static bool tried; static MethodInfo asTaskOp, asStream;
    static Type tDecoder;
    public static bool Available { get { Init(); return engine != null; } }

    static void Init()
    {
        if (tried) return; tried = true;
        try
        {
            var tEngine = Type.GetType("Windows.Media.Ocr.OcrEngine, Windows.Foundation, ContentType=WindowsRuntime", true);
            tDecoder = Type.GetType("Windows.Graphics.Imaging.BitmapDecoder, Windows.Foundation, ContentType=WindowsRuntime", true);
            Type.GetType("Windows.Graphics.Imaging.SoftwareBitmap, Windows.Foundation, ContentType=WindowsRuntime", true);
            var rt = Assembly.Load("System.Runtime.WindowsRuntime, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
            asTaskOp = rt.GetType("System.WindowsRuntimeSystemExtensions").GetMethods()
                .First(m => m.Name == "AsTask" && m.IsGenericMethod && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.Name == "IAsyncOperation`1");
            asStream = rt.GetType("System.IO.WindowsRuntimeStreamExtensions").GetMethod("AsRandomAccessStream", new[] { typeof(Stream) });
            engine = tEngine.GetMethod("TryCreateFromUserProfileLanguages").Invoke(null, null);
        }
        catch { engine = null; }
    }

    static object Await(object op, Type result)
    {
        var task = (Task)asTaskOp.MakeGenericMethod(result).Invoke(null, new[] { op });
        task.Wait(5000);
        return task.GetType().GetProperty("Result").GetValue(task);
    }

    // returns the recognised lines (empty array on failure)
    public static string[] Read(Bitmap bmp)
    {
        Init(); if (engine == null) return new string[0];
        try
        {
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png); ms.Position = 0;
                var ras = asStream.Invoke(null, new object[] { ms });
                var decOp = tDecoder.GetMethods().First(m => m.Name == "CreateAsync" && m.GetParameters().Length == 1).Invoke(null, new[] { ras });
                var dec = Await(decOp, tDecoder);
                var sbOp = tDecoder.GetMethods().First(m => m.Name == "GetSoftwareBitmapAsync" && m.GetParameters().Length == 0).Invoke(dec, null);
                var tSb = Type.GetType("Windows.Graphics.Imaging.SoftwareBitmap, Windows.Foundation, ContentType=WindowsRuntime");
                var sb = Await(sbOp, tSb);
                var recOp = engine.GetType().GetMethod("RecognizeAsync").Invoke(engine, new[] { sb });
                var tRes = Type.GetType("Windows.Media.Ocr.OcrResult, Windows.Foundation, ContentType=WindowsRuntime");
                var res = Await(recOp, tRes);
                var lines = (System.Collections.IEnumerable)tRes.GetProperty("Lines").GetValue(res);
                var list = new System.Collections.Generic.List<string>();
                foreach (var l in lines) list.Add((string)l.GetType().GetProperty("Text").GetValue(l));
                ((IDisposable)sb).Dispose();
                return list.ToArray();
            }
        }
        catch { return new string[0]; }
    }
}
