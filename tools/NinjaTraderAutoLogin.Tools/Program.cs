using System;

namespace NinjaTraderAutoLogin.Tools
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "icons")
            {
                return GenerateIcons.Run(new[] { args[1] });
            }

            if (args.Length == 2 && args[0] == "layout-audit")
            {
                return ResponsivenessAudit.Run(new[] { args[1] });
            }

            if (args.Length == 1 && args[0] == "font-probe")
            {
                return VerifyEmbeddedFont.Run();
            }

            Console.Error.WriteLine("Usage: tools icons <directory> | layout-audit <report.json> | font-probe");
            return 2;
        }
    }
}
