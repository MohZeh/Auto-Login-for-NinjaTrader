using System;
using System.Drawing;
using System.IO;
using NinjaTraderAutoLogin;
internal static class VerifyEmbeddedFont
{
    internal static int Run()
    {
        try
        {
            string root = AppDomain.CurrentDomain.BaseDirectory;
            if (Directory.GetFiles(root, "*.ttf", SearchOption.AllDirectories).Length != 0)
            {
                throw new Exception("Probe must contain no font files");
            }

            Font regular = UiFonts.Get("fa", 9.25F, FontStyle.Regular), bold = UiFonts.Get("fa", 12.5F, FontStyle.Bold);
            if (regular.FontFamily.Name != "Vazir" || bold.FontFamily.Name != "Vazir" || !bold.Bold)
            {
                return 1;
            }

            Console.WriteLine("{\"candidate\":\"" + BuildInfo.Version + "\",\"font\":\"Vazir\",\"source\":\"EXE embedded resources\",\"localFontFiles\":0,\"regular\":true,\"bold\":true,\"createdUi\":false,\"result\":\"PASS\"}");
            return 0;
        }
        finally { UiFonts.Release(); }
    }
}
