using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.Serialization;
using System.Windows.Forms;

namespace NinjaTraderAutoLogin
{
    [DataContract]
    internal sealed class SettingsLayoutItem
    {
        [DataMember] public string Role, ParentRole, Kind, FontFamily;
        [DataMember] public int X, Y, Width, Height, ParentWidth, ParentHeight, FontHeight;
        [DataMember] public bool Visible;
    }

    [DataContract]
    internal sealed class SettingsLayoutRecord
    {
        [DataMember] public int SchemaVersion = 6;
        [DataMember] public string Candidate, HelperSha256, Utc, Language, Theme, TradingMode, Reason;
        [DataMember] public int Dpi, ManagedDpi, NativeDpi, ClientWidth, ClientHeight;
        [DataMember] public int PageContentHeight, ScrollX, ScrollY, PageClientWidth, PageClientHeight, WorkWidth, WorkHeight;
        [DataMember] public int NativeScrollBarWidth, FrameworkScrollBarWidth, ReservedScrollBarWidth;
        [DataMember] public bool Compact, FooterInScroll, HorizontalScrollbar, VerticalScrollbar;
        [DataMember] public string WindowState;
        [DataMember] public List<SettingsLayoutItem> Controls = new List<SettingsLayoutItem>();
    }

    internal static class SettingsLayoutEvidence
    {
        // Layout records are a development aid; normal use writes no files for them.
        public static readonly bool Enabled = Environment.GetEnvironmentVariable("NINJATRADER_AUTOLOGIN_LAYOUT_EVIDENCE") == "1";

        public static void RecordSurface(Control surface, string language, string reason)
        {
            if (!Enabled)
            {
                return;
            }

            try
            {
                var record = new SettingsLayoutRecord
                {
                    Candidate = BuildInfo.Version,
                    HelperSha256 = RunEvidence.HashFile(Paths.Executable),
                    Utc = DateTime.UtcNow.ToString("o"),
                    Language = language,
                    Reason = reason,
                    Dpi = UiDpi.Get(surface),
                    ClientWidth = surface.ClientSize.Width,
                    ClientHeight = surface.ClientSize.Height
                };
                AddChildren(surface, record.Controls);
                JsonFiles.Write(Path.Combine(Paths.Evidence, "surface-layout-" + BuildInfo.Version + "-" + language + "-" + reason + "-" + record.ClientWidth + "x" + record.ClientHeight + "-" + record.Dpi + ".json"), record);
            }
            catch (Exception error) { AppFaults.Record("SurfaceLayoutEvidence", error); }
        }
        public static void Record(Form form, string language, string theme, string tradingMode, string reason, SettingsLayoutPlan plan, ScrollableControl viewport, Rectangle work)
        {
            if (!Enabled)
            {
                return;
            }

            try
            {
                var record = new SettingsLayoutRecord
                {
                    Candidate = BuildInfo.Version,
                    HelperSha256 = RunEvidence.HashFile(Paths.Executable),
                    Utc = DateTime.UtcNow.ToString("o"),
                    Language = language,
                    Theme = theme,
                    TradingMode = tradingMode,
                    Reason = reason,
                    Dpi = UiDpi.Get(form),
                    ManagedDpi = form.DeviceDpi,
                    NativeDpi = UiDpi.Native(form),
                    ClientWidth = form.ClientSize.Width,
                    ClientHeight = form.ClientSize.Height,
                    Compact = plan != null && plan.Compact,
                    FooterInScroll = plan != null && plan.FooterInScroll,
                    PageContentHeight = plan == null ? 0 : plan.PageContentHeight,
                    ScrollX = -viewport.AutoScrollPosition.X,
                    ScrollY = -viewport.AutoScrollPosition.Y,
                    PageClientWidth = viewport.ClientSize.Width,
                    PageClientHeight = viewport.ClientSize.Height,
                    NativeScrollBarWidth = UiDpi.Scrollbar(UiDpi.Get(form)),
                    FrameworkScrollBarWidth = SystemInformation.VerticalScrollBarWidth,
                    ReservedScrollBarWidth = plan == null ? 0 : plan.Bounds["bodyViewport"].Width - plan.PageContentWidth,
                    HorizontalScrollbar = viewport.HorizontalScroll.Visible,
                    VerticalScrollbar = viewport.VerticalScroll.Visible,
                    WorkWidth = work.Width,
                    WorkHeight = work.Height,
                    WindowState = form.WindowState.ToString()
                };
                AddChildren(form, record.Controls);
                // Fixed role metadata only: never Text, Value, SelectedItem, AccessibleName,
                // window titles, external controls, screenshots, or clipboard content.
                string dimensions = record.ClientWidth + "x" + record.ClientHeight + "-" + record.Dpi + (record.ScrollY > 0 ? "-scrolled" : "-top");
                JsonFiles.Write(Path.Combine(Paths.Evidence, "settings-layout-" + BuildInfo.Version + "-" + language + "-" + theme + "-" + dimensions + ".json"), record);
            }
            catch (Exception error) { AppFaults.Record("SettingsLayoutEvidence", error); }
        }
        private static void AddChildren(Control parent, List<SettingsLayoutItem> items)
        {
            foreach (Control child in parent.Controls)
            {
                // The saved-password decoration occupies the empty editor's bounds.
                // It is not an independently laid-out field.
                if (parent is InputFrame && child.Name == "savedPasswordIndicator")
                {
                    continue;
                }
                Rectangle bounds = child.Bounds;
                items.Add(new SettingsLayoutItem
                {
                    Role = child.Name,
                    ParentRole = parent.Name,
                    Kind = child.GetType().Name,
                    X = bounds.X,
                    Y = bounds.Y,
                    Width = bounds.Width,
                    Height = bounds.Height,
                    ParentWidth = parent.ClientSize.Width,
                    ParentHeight = parent.ClientSize.Height,
                    FontHeight = child.Font.Height,
                    FontFamily = child.Font.FontFamily.Name,
                    Visible = child.Visible
                });
                AddChildren(child, items);
            }
        }
    }
}
