using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Windows.Forms;
using NinjaTraderAutoLogin;

[DataContract]
internal sealed class AuditCase
{
    [DataMember] public int ScreenWidth, ScreenHeight, Dpi, ScalePercent, WorkWidth, WorkHeight, FrameWidth, FrameHeight, ClientWidth, ClientHeight, BodyViewportHeight, BodyContentHeight;
    [DataMember] public string Language, Result;
    [DataMember] public bool ScrollNeeded, Compact, FooterInScroll, ManualResize;
    [DataMember] public List<string> GeometryIssues = new List<string>(), TextWidthIssues = new List<string>();
}
[DataContract]
internal sealed class AuditReport
{
    [DataMember] public string Candidate = BuildInfo.Version, Scope = "Pure production-plan geometry and font measurements; no Form, Control, UI Automation or Native UI test was created/executed/observed";
    [DataMember] public string WorkAreaModel = "Bottom taskbar48 logical pixels; production WindowFitPolicy margin24 physical pixels; resizable caption/system/minimize/maximize/thick-frame style calculated by AdjustWindowRectExForDpi. Frame/taskbar are model inputs, not observed UI.";
    [DataMember] public string TextModel = "GDI memory-graphics measurement at96 DPI with embedded fonts; wrapping height must fit allocated rows. No actual control is rendered.";
    [DataMember] public int Cases, FitsWithoutScroll, ScrollOnly, GeometryIssues, TextOnlyRiskCases, CasesWithAnyTextWidthRisk;
    [DataMember] public List<AuditCase> Results = new List<AuditCase>();
    [DataMember] public List<AuditCase> ResizeResults = new List<AuditCase>();
    [DataMember] public int ResizeCases, ResizeGeometryIssues, ResizeTextRisks;
}
internal static class ResponsivenessAudit
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool AdjustWindowRectExForDpi(ref Rect rect, uint style, bool menu, uint extended, uint dpi);
    [DllImport("user32.dll")] private static extern int GetSystemMetricsForDpi(int index, uint dpi);
    private static int Px(int value, int dpi)
    {
        return (int)Math.Round(value * dpi / 96.0);
    }
    private static string T(string language, string en, string fa)
    {
        return language == "fa" ? fa : en;
    }
    private static AuditCase Evaluate(int width, int height, int dpi, string language, Graphics graphics, int resizeWidth = 0, int resizeHeight = 0)
    {
        var frame = new Rect { Right = 1, Bottom = 1 };
        // Resizable caption/system/minimize/maximize/thick frame with window edge.
        if (!AdjustWindowRectExForDpi(ref frame, 0x00CF0000, false, 0x00000100, (uint)dpi))
        {
            throw new Exception("FrameCalculationFailed");
        }

        var item = new AuditCase
        {
            ScreenWidth = width,
            ScreenHeight = height,
            Dpi = dpi,
            ScalePercent = (int)Math.Round(dpi * 100.0 / 96),
            Language = language,
            WorkWidth = width,
            WorkHeight = height - Px(48, dpi),
            FrameWidth = frame.Right - frame.Left - 1,
            FrameHeight = frame.Bottom - frame.Top - 1
        };
        var requested = new Rectangle(0, 0, Px(SettingsLayoutPlan.DesignWidth, dpi) + item.FrameWidth, Px(SettingsLayoutPlan.DesignHeight, dpi) + item.FrameHeight);
        WindowFit fit = WindowFitPolicy.Calculate(new Rectangle(0, 0, item.WorkWidth, item.WorkHeight), requested, new Size(item.FrameWidth, item.FrameHeight), dpi, true);
        item.ClientWidth = fit.Bounds.Width - item.FrameWidth;
        item.ClientHeight = fit.Bounds.Height - item.FrameHeight;
        if (resizeWidth > 0)
        {
            item.ClientWidth = Px(resizeWidth, dpi);
            item.ClientHeight = Px(resizeHeight, dpi);
            item.ManualResize = true;
        }
        int scrollbar = 0;
        var plan = new SettingsLayoutPlan(item.ClientWidth, item.ClientHeight, dpi, language == "fa", scrollbar);
        for (int pass = 0; pass < 3; pass++)
        {
            int needed = plan.PageContentHeight > plan.Bounds["bodyViewport"].Height ? UiDpi.ScrollbarReserve(dpi) : 0;
            if (needed == scrollbar)
            {
                break;
            }

            scrollbar = needed;
            plan = new SettingsLayoutPlan(item.ClientWidth, item.ClientHeight, dpi, language == "fa", scrollbar);
        }
        item.ScrollNeeded = plan.Bounds["bodyViewport"].Height < plan.PageContentHeight;
        item.Compact = plan.Compact;
        item.FooterInScroll = plan.FooterInScroll;
        item.BodyViewportHeight = plan.Bounds["bodyViewport"].Height;
        item.BodyContentHeight = plan.BodyContentHeight;
        foreach (var role in plan.Parents)
        {
            Rectangle bounds = plan.Bounds[role.Key];
            Size parent = role.Value == "settings" ? new Size(item.ClientWidth, item.ClientHeight) : plan.Bounds[role.Value].Size;
            bool scrollChild = role.Value == "bodyViewport";
            if (bounds.X < 0 || bounds.Y < 0 || bounds.Width <= 0 || bounds.Height <= 0 || bounds.Right > parent.Width || (!scrollChild && bounds.Bottom > parent.Height))
            {
                item.GeometryIssues.Add("Outside parent: " + role.Key);
            }

            foreach (var other in plan.Parents)
            {
                if (String.CompareOrdinal(role.Key, other.Key) < 0 && role.Value == other.Value && bounds.IntersectsWith(plan.Bounds[other.Key]))
                {
                    item.GeometryIssues.Add("Overlap: " + role.Key + " / " + other.Key);
                }
            }
        }
        foreach (string role in new[] { "languageFa", "languageEn", "theme", "settingsTab", "aboutTab", "options", "cancelLogin", "pathInput", "usernameInput", "passwordInput", "simulationOption", "liveOption", "shortcutOption", "watchOption", "saveSettings", "saveAndLaunch", "closeSettings" })
        {
            string parent = plan.Parents[role];
            Rectangle absolute = plan.Bounds[role];
            while (parent != "bodyViewport" && parent != "settings")
            {
                absolute.Offset(plan.Bounds[parent].Location);
                parent = plan.Parents[parent];
            }
            if (parent == "bodyViewport" && (absolute.Height > plan.Bounds["bodyViewport"].Height || absolute.Bottom > plan.PageContentHeight))
            {
                item.GeometryIssues.Add("Interactive control cannot fully enter scroll viewport: " + role);
            }
        }
        CheckText(item, plan, graphics, "heading", ProductInfo.Name(language), 13.5F, FontStyle.Bold, 0);
        CheckText(item, plan, graphics, "settingsTab", T(language, "Setup", "تنظیمات"), 9.25F, FontStyle.Bold, 0);
        CheckText(item, plan, graphics, "aboutTab", T(language, "About", "دربارهٔ برنامه"), 9.25F, FontStyle.Bold, 0);
        CheckText(item, plan, graphics, "options", T(language, "Options", "گزینه‌ها"), 9.25F, FontStyle.Regular, 16);
        CheckText(item, plan, graphics, "languageFa", "FA", 9.25F, FontStyle.Bold, 0);
        CheckText(item, plan, graphics, "languageEn", "EN", 9.25F, FontStyle.Bold, 0);
        CheckText(item, plan, graphics, "accountHeading", T(language, "NinjaTrader account", "حساب نینجاتریدر"), 9.5F, FontStyle.Bold, 0);
        CheckText(item, plan, graphics, "pathLabel", T(language, "NinjaTrader application", "برنامهٔ نینجاتریدر"), 9.5F, FontStyle.Bold, 0);
        CheckText(item, plan, graphics, "usernameLabel", T(language, "Username", "نام کاربری"), 9.5F, FontStyle.Regular, 0);
        CheckText(item, plan, graphics, "passwordLabel", T(language, "Password", "رمز عبور"), 9.5F, FontStyle.Regular, 0);
        CheckText(item, plan, graphics, "environmentLabel", T(language, "Environment", "محیط معاملاتی"), 9.5F, FontStyle.Regular, 0);
        CheckText(item, plan, graphics, "startMethodLabel", T(language, "Start with", "روش شروع"), 9.5F, FontStyle.Regular, 0);
        foreach (TargetInspection target in new[]
        {
            new TargetInspection(),
            new TargetInspection { Exists = true },
            new TargetInspection { Exists = true, Trusted = true, Version = "8.1.8.4", Status = NinjaTraderVersionStatus.Tested },
            new TargetInspection { Exists = true, Trusted = true, Version = "8.1.10.12", Status = NinjaTraderVersionStatus.Unknown },
            new TargetInspection { Exists = true, Trusted = true, Status = NinjaTraderVersionStatus.Invalid }
        })
        {
            CheckText(item, plan, graphics, "targetStatus", UiText.TargetStatus(language, target), 9, FontStyle.Regular, 0);
        }
        foreach (bool saved in new[] { false, true })
        {
            foreach (bool draft in new[] { false, true })
            {
                CheckText(item, plan, graphics, "credentialHint", UiText.PasswordHint(language, saved, draft), 9, FontStyle.Regular, 0);
            }
        }
        foreach (bool uncertain in new[] { false, true })
        {
            CheckText(item, plan, graphics, "credentialHint", UiText.RejectedHint(language, uncertain), 9, FontStyle.Bold, 0);
        }
        CheckText(item, plan, graphics, "removeRejected", T(language, "Remove saved login", "حذف اطلاعات ورود"), 9.25F, FontStyle.Regular, 12);
        CheckText(item, plan, graphics, "startMethodHeading", T(language, "Login behavior", "رفتار ورود"), 9.5F, FontStyle.Bold, 0);
        CheckText(item, plan, graphics, "startMethodHint", T(language, "Starts with Windows. Open NinjaTrader as usual.", "پایش با ویندوز فعال می‌شود؛ نینجاتریدر را معمولی اجرا کنید."), 9, FontStyle.Regular, 0);
        foreach (ShortcutState state in new[] { ShortcutState.Missing, ShortcutState.Ready, ShortcutState.Stale })
        {
            CheckText(item, plan, graphics, "createShortcut", UiText.ShortcutAction(language, state), 9.25F, FontStyle.Regular, 12);
        }
        CheckText(item, plan, graphics, "shortcutOption", T(language, "Dedicated shortcut", "شورتکات اختصاصی"), 9.25F, FontStyle.Regular, 24);
        CheckText(item, plan, graphics, "watchOption", T(language, "Watch NinjaTrader", "پایش نینجاتریدر"), 9.25F, FontStyle.Regular, 24);
        CheckText(item, plan, graphics, "simulationOption", T(language, "Simulation", "سیمولیشن"), 9.25F, FontStyle.Regular, 24);
        CheckText(item, plan, graphics, "liveOption", T(language, "Live trading", "لایو ترید"), 9.25F, FontStyle.Regular, 24);
        item.Result = item.GeometryIssues.Count > 0 ? "GEOMETRY_ISSUE" : item.TextWidthIssues.Count > 0 ? "TEXT_WIDTH_RISK" : item.ScrollNeeded ? "SCROLL_REQUIRED" : "FITS_WITHOUT_SCROLL";
        return item;
    }
    private static void CheckText(AuditCase item, SettingsLayoutPlan plan, Graphics graphics, string role, string text, float points, FontStyle style, int reserved)
    {
        Font font = UiFonts.Get(item.Language, points * item.Dpi / 96F, style);
        TextFormatFlags flags = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix;
        if (item.Language == "fa")
        {
            flags |= TextFormatFlags.RightToLeft;
        }

        int available = plan.Bounds[role].Width - Px(reserved, item.Dpi);
        Size required = TextRenderer.MeasureText(graphics, text, font, new Size(Math.Max(1, available), 10000), flags);
        if (required.Width > available || required.Height > plan.Bounds[role].Height)
        {
            item.TextWidthIssues.Add(role + ": needs" + required.Width + "x" + required.Height + ", available" + available + "x" + plan.Bounds[role].Height);
        }
    }
    internal static int Run(string[] args)
    {
        try
        {
            var report = new AuditReport();
            int[,] screens = { { 800, 600 }, { 1024, 768 }, { 1280, 720 }, { 1366, 768 }, { 1600, 900 }, { 1920, 1080 }, { 2560, 1440 }, { 3840, 2160 }, { 768, 1024 }, { 1080, 1920 } };
            using (var bitmap = new Bitmap(1, 1))
            {
                bitmap.SetResolution(96, 96);
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    for (int s = 0; s < screens.GetLength(0); s++)
                    {
                        foreach (int dpi in new[] { 96, 120, 144, 168, 192, 216, 240, 288 })
                        {
                            foreach (string language in new[] { "fa", "en" })
                            {
                                AuditCase item = Evaluate(screens[s, 0], screens[s, 1], dpi, language, graphics);
                                report.Results.Add(item);
                                if (item.TextWidthIssues.Count > 0)
                                {
                                    report.CasesWithAnyTextWidthRisk++;
                                }

                                if (item.Result == "GEOMETRY_ISSUE")
                                {
                                    report.GeometryIssues++;
                                }
                                else if (item.Result == "TEXT_WIDTH_RISK")
                                {
                                    report.TextOnlyRiskCases++;
                                }
                                else if (item.Result == "SCROLL_REQUIRED")
                                {
                                    report.ScrollOnly++;
                                }
                                else
                                {
                                    report.FitsWithoutScroll++;
                                }
                            }
                        }
                    }
                }
            }
            report.Cases = report.Results.Count;
            using (var bitmap = new Bitmap(1, 1))
            {
                bitmap.SetResolution(96, 96);
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    foreach (Size size in new[] { new Size(220, 120), new Size(220, 484), new Size(300, 240), new Size(480, 160) })
                    {
                        foreach (int dpi in new[] { 96, 120, 144, 168, 192, 216, 240, 288 })
                        {
                            foreach (string language in new[] { "fa", "en" })
                            {
                                AuditCase item = Evaluate(2560, 1440, dpi, language, graphics, size.Width, size.Height);
                                report.ResizeResults.Add(item);
                                if (item.GeometryIssues.Count > 0)
                                {
                                    report.ResizeGeometryIssues++;
                                }

                                if (item.TextWidthIssues.Count > 0)
                                {
                                    report.ResizeTextRisks++;
                                }
                            }
                        }
                    }
                }
            }
            report.ResizeCases = report.ResizeResults.Count;
            using (var stream = File.Create(args[0]))
            {
                new DataContractJsonSerializer(typeof(AuditReport)).WriteObject(stream, report);
            }

            Console.WriteLine("Cases=" + report.Cases + "; Fits=" + report.FitsWithoutScroll + "; Scroll=" + report.ScrollOnly + "; GeometryIssues=" + report.GeometryIssues + "; AnyTextRisk=" + report.CasesWithAnyTextWidthRisk + "; ResizeCases=" + report.ResizeCases + "; ResizeIssues=" + (report.ResizeGeometryIssues + report.ResizeTextRisks) + "; no UI executed.");
            return 0;
        }
        finally { UiFonts.Release(); }
    }
}
