using System;
using System.Collections.Generic;

namespace NinjaTraderAutoLogin
{
    // One production contract also consumes captured metadata in headless regressions.
    internal static class ControlContract
    {
        private static readonly string[] Known = { "tbUserName", "passwordBox", "btnLogin", "btnSimulation", "btnLiveTrading", "ControlCenter" };
        public static bool IsMainSurface(ControlEvidence control)
        {
            return control.AutomationId == "ControlCenter" && control.ClassName == "ControlCenter" &&
                (control.ControlType == "ControlType.Window" || control.ControlType == "ControlType.Custom");
        }
        public static UiSnapshot Evaluate(List<ControlEvidence> controls)
        {
            var snapshot = new UiSnapshot { Controls = controls };
            var ready = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (string id in Known)
            {
                int count = 0;
                bool supported = false;
                foreach (ControlEvidence item in controls)
                {
                    if (item.AutomationId != id || item.Offscreen)
                    {
                        continue;
                    }

                    count++;
                    bool valid = id == "ControlCenter" ? IsMainSurface(item) :
                        (id == "tbUserName" || id == "passwordBox") ?
                        item.ControlType == "ControlType.Edit" && item.ValuePattern && item.IsPassword == (id == "passwordBox") :
                        item.ControlType == "ControlType.Button" && item.InvokePattern && !item.IsPassword;
                    if (item.Enabled && !valid)
                    {
                        snapshot.Ambiguous = true;
                    }

                    supported = valid && (id == "ControlCenter" || item.Enabled);
                }
                if (count > 1)
                {
                    snapshot.Ambiguous = true;
                }

                ready[id] = count == 1 && supported;
            }
            snapshot.LoginReady = ready["tbUserName"] && ready["passwordBox"] && ready["btnLogin"];
            snapshot.SimulationReady = ready["btnSimulation"];
            snapshot.LiveReady = ready["btnLiveTrading"];
            snapshot.ControlCenter = ready["ControlCenter"];
            if (snapshot.LoginReady && (snapshot.SimulationReady || snapshot.LiveReady))
            {
                snapshot.Ambiguous = true;
            }

            return snapshot;
        }
    }
}
