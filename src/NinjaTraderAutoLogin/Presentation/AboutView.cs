using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NinjaTraderAutoLogin
{
    internal sealed class AboutView : UserControl
    {
        private readonly string language;
        private readonly bool rtl;
        private readonly Panel viewport;
        private readonly Panel content;
        private readonly Dictionary<string, Control> parts = new Dictionary<string, Control>();
        private readonly HashSet<string> shown = new HashSet<string>();
        private readonly Button checkUpdates, openDiagnostics, copySummary;
        private readonly Label updateStatus, supportStatus;
        private readonly AddressLink releaseLink;
        private readonly GitHubReleaseClient releases = new GitHubReleaseClient();
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly CancellationToken lifetimeToken;
        private readonly DisposeOnce cleanup = new DisposeOnce();
        private bool arranging;
        private string recordedLayout;

        public AboutView(string language)
        {
            this.language = language;
            lifetimeToken = lifetime.Token;
            rtl = language == "fa";
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = UiFonts.Get(language, 9.25F, FontStyle.Regular);
            BackColor = UiAssets.Canvas;
            ForeColor = UiAssets.Ink;
            Name = "aboutPage";
            viewport = new SettingsViewport { Name = "aboutViewport", Dock = DockStyle.Fill, AutoScroll = true, RightToLeft = RightToLeft.No };
            Controls.Add(viewport);
            viewport.BringToFront();
            content = new Panel { Name = "aboutColumn", Margin = Padding.Empty, Padding = Padding.Empty, RightToLeft = RightToLeft.No };
            viewport.Controls.Add(content);

            Surface version = Card("versionCard"), contact = Card("contactCard"), support = Card("supportCard");
            AddText("versionHeading", version, T("Version and updates", "نسخه و به‌روزرسانی"), 9.5F, FontStyle.Bold);
            AddText("versionLabel", version, T("Version", "نسخه"));
            var versionTag = new VersionTag(language, BuildInfo.Version) { Name = "versionTag", AccessibleName = T("Version", "نسخه") };
            parts.Add(versionTag.Name, versionTag);
            version.Controls.Add(versionTag);
            AddText("compatibilityLabel", version, T("NinjaTrader", "نینجاتریدر"));
            AddText("compatibility", version, T("✓ " + NinjaTraderCompatibility.TestedVersion + " tested", "✓ " + NinjaTraderCompatibility.TestedVersion + " آزموده‌شده"));
            checkUpdates = AddButton("checkUpdates", version, T("Check for updates", "بررسی نسخهٔ جدید"));
            checkUpdates.Click += async delegate { await CheckUpdatesAsync(); };
            updateStatus = AddText("updateStatus", version, String.Empty);
            releaseLink = new AddressLink(null)
            {
                Name = "releaseLink",
                Alignment = rtl ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft,
                Font = UiFonts.Get(language, 9.25F, FontStyle.Underline),
                RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No
            };
            releaseLink.Click += delegate
            {
                OpenLink(releaseLink.Target);
            };
            parts.Add(releaseLink.Name, releaseLink);
            version.Controls.Add(releaseLink);

            AddText("contactHeading", contact, T("Contact and source", "ارتباط و سورس"), 9.5F, FontStyle.Bold);
            AddAddress("email", contact, T("Email", "ایمیل"), new Uri("mailto:" + ProductInfo.ContactEmail));
            AddAddress("github", contact, T("GitHub", "گیت‌هاب"), new Uri(ProductInfo.GitHubProfileUrl));
            AddAddress("source", contact, T("Source code", "سورس برنامه"), new Uri(ProductInfo.SourceRepositoryUrl));

            AddText("supportHeading", support, T("License and support", "مجوز و پشتیبانی"), 9.5F, FontStyle.Bold);
            Label legal = AddText("legal", support, UiText.LegalNotice(language), 9F, FontStyle.Regular);
            legal.ForeColor = UiAssets.Muted;
            legal.TextAlign = ContentAlignment.TopLeft;
            openDiagnostics = AddButton("openDiagnostics", support, T("Open diagnostics folder", "بازکردن پوشهٔ گزارش‌ها"));
            openDiagnostics.Click += delegate
            {
                OpenDiagnostics();
            };
            copySummary = AddButton("copySummary", support, T("Copy support summary", "کپی خلاصهٔ پشتیبانی"));
            copySummary.Click += delegate
            {
                CopySummary();
            };
            supportStatus = AddText("supportStatus", support, String.Empty);
            supportStatus.TextAlign = ContentAlignment.MiddleCenter;
            viewport.ClientSizeChanged += delegate
            {
                Arrange();
            };
            ApplyPalette();
        }
        private string T(string english, string persian)
        {
            return UiText.Choose(language, english, persian);
        }
        public void ApplyPalette()
        {
            BackColor = viewport.BackColor = content.BackColor = UiAssets.Canvas;
            ForeColor = UiAssets.Ink;
            foreach (KeyValuePair<string, Control> part in parts)
            {
                if (part.Value is Surface)
                {
                    continue;
                }

                part.Value.BackColor = UiAssets.Surface;
                bool quiet = part.Key == "legal" || part.Key.EndsWith("Label", StringComparison.Ordinal);
                part.Value.ForeColor = part.Key == "compatibility" ? UiAssets.Simulation : quiet ? UiAssets.Muted : UiAssets.Ink;
            }
            Invalidate(true);
        }
        private Surface Card(string name)
        {
            var card = new Surface { Name = name };
            parts.Add(name, card);
            content.Controls.Add(card);
            return card;
        }
        private Button AddButton(string name, Control parent, string text)
        {
            var button = new ActionButton
            {
                Name = name,
                Kind = ActionKind.Secondary,
                Text = text,
                Font = Font,
                OnCard = true,
                RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No
            };
            parts.Add(name, button);
            parent.Controls.Add(button);
            return button;
        }
        private Label AddText(string name, Control parent, string text, float size = 9.25F, FontStyle style = FontStyle.Regular)
        {
            var label = new Label
            {
                Name = name,
                Text = text,
                AutoSize = false,
                UseMnemonic = false,
                UseCompatibleTextRendering = false,
                Margin = Padding.Empty,
                Font = UiFonts.Get(language, size, style),
                RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No,
                // WinForms mirrors Left to Right when RightToLeft is Yes.
                TextAlign = ContentAlignment.MiddleLeft
            };
            parts.Add(name, label);
            parent.Controls.Add(label);
            return label;
        }
        private void AddAddress(string name, Control parent, string caption, Uri target)
        {
            AddText(name + "Label", parent, caption);
            var link = new AddressLink(target) { Name = name + "Link", AccessibleName = caption };
            link.Click += delegate
            {
                OpenLink(link.Target);
            };
            parts.Add(link.Name, link);
            parent.Controls.Add(link);
        }
        private int ButtonWidth(string role)
        {
            int dpi = UiDpi.Get(this);
            using (Graphics graphics = CreateGraphics())
            {
                return TextRenderer.MeasureText(graphics, parts[role].Text, parts[role].Font).Width + (int)Math.Round(36 * dpi / 96.0);
            }
        }
        private int TextHeight(Graphics graphics, string role, int width)
        {
            Control part = parts[role];
            var address = part as AddressLink;
            if (address != null)
            {
                return address.HeightFor(graphics, width);
            }

            TextFormatFlags flags = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix;
            if (part.RightToLeft == RightToLeft.Yes)
            {
                flags |= TextFormatFlags.RightToLeft;
            }

            // Labels wrap inside their own client area, so reserve the same padding when measuring.
            int reserve = part is Label ? 6 : 0;
            return TextRenderer.MeasureText(graphics, part.Text, part.Font, new Size(Math.Max(1, width - reserve), Int32.MaxValue), flags).Height + (part is Label ? 4 : 0);
        }
        internal AboutLayoutPlan PlanFor(int width, int dpi)
        {
            using (Graphics graphics = CreateGraphics())
            {
                return new AboutLayoutPlan(width, dpi, rtl, (role, available) => TextHeight(graphics, role, available), ButtonWidth, shown);
            }
        }
        private void Arrange()
        {
            if (arranging || !IsHandleCreated)
            {
                return;
            }

            arranging = true;
            viewport.SuspendLayout();
            content.SuspendLayout();
            try
            {
                int dpi = UiDpi.Get(this);
                AboutLayoutPlan plan = PlanFor(viewport.Width, dpi);
                if (plan.Height > viewport.Height)
                {
                    plan = PlanFor(Math.Max(1, viewport.Width - UiDpi.ScrollbarReserve(dpi)), dpi);
                }

                int scrollY = Math.Max(0, -viewport.AutoScrollPosition.Y);
                content.Bounds = new Rectangle(0, -scrollY, plan.Bounds["versionCard"].Width, plan.Height);
                foreach (KeyValuePair<string, Control> part in parts)
                {
                    Rectangle bounds;
                    part.Value.Visible = plan.Bounds.TryGetValue(part.Key, out bounds);
                    if (part.Value.Visible)
                    {
                        part.Value.Bounds = bounds;
                    }
                }
                viewport.AutoScrollMinSize = new Size(0, plan.Height);
                string key = ClientSize + ":" + dpi + ":" + content.Bounds + ":" + plan.Bounds.Count;
                if (recordedLayout != key && Visible)
                {
                    recordedLayout = key;
                    SettingsLayoutEvidence.RecordSurface(this, language, "About");
                }
            }
            finally
            {
                content.ResumeLayout(false);
                viewport.ResumeLayout(true);
                arranging = false;
            }
        }
        internal void ShowNewVersion(string version, Uri page)
        {
            releaseLink.ShowSentence(page, T("Version " + version + " is available: open its page", "نسخهٔ " + version + " منتشر شده: رفتن به صفحهٔ دانلود"));
            releaseLink.AccessibleName = releaseLink.Text;
            shown.Remove(updateStatus.Name);
            shown.Add(releaseLink.Name);
            Arrange();
            if (viewport.VerticalScroll.Visible)
            {
                viewport.ScrollControlIntoView(releaseLink);
            }
        }
        private void Show(Label label, string text)
        {
            label.Text = text;
            shown.Add(label.Name);
            Arrange();
            if (viewport.VerticalScroll.Visible)
            {
                viewport.ScrollControlIntoView(label);
            }
        }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            Arrange();
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Arrange();
        }
        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible)
            {
                Arrange();
            }
        }
        private void OpenLink(Uri target)
        {
            if (target == null)
            {
                return;
            }
            bool allowed = target.AbsoluteUri == ProductInfo.GitHubProfileUrl || target.AbsoluteUri == "mailto:" + ProductInfo.ContactEmail ||
                target.AbsoluteUri == ProductInfo.SourceRepositoryUrl || ProductInfo.IsReleasePage(target);
            if (!allowed)
            {
                return;
            }
            try
            {
                Process.Start(new ProcessStartInfo(target.AbsoluteUri) { UseShellExecute = true });
            }
            catch { Show(supportStatus, T("The link could not be opened. Check your browser or email app.", "بازکردن لینک انجام نشد؛ مرورگر یا برنامهٔ ایمیل را بررسی کنید.")); }
        }
        private void OpenDiagnostics()
        {
            try
            {
                Directory.CreateDirectory(Paths.Evidence);
                Process.Start(new ProcessStartInfo("explorer.exe", "\"" + Paths.Evidence + "\"") { UseShellExecute = true });
            }
            catch (Exception error)
            {
                AppFaults.Record("OpenDiagnostics", error);
                Show(supportStatus, T("The folder could not be opened.", "پوشه باز نشد."));
            }
        }
        private void CopySummary()
        {
            try
            {
                Clipboard.SetText(SupportSummary.Build(Paths.Evidence, BuildInfo.Version, Environment.OSVersion.VersionString));
                Show(supportStatus, T("Copied. It has no password, username or file paths.", "کپی شد؛ رمز، نام کاربری و مسیر فایل در آن نیست."));
            }
            catch (Exception error) when (error is ExternalException || error is IOException || error is UnauthorizedAccessException)
            {
                AppFaults.Record("CopySupportSummary", error);
                Show(supportStatus, T("The summary could not be copied. Try again.", "خلاصه کپی نشد؛ دوباره تلاش کنید."));
            }
        }
        // The app only points to the release page. It never downloads or replaces itself, because it
        // handles a password and is not code-signed; the owner decides what to install and verifies it.
        private async Task CheckUpdatesAsync()
        {
            checkUpdates.Enabled = false;
            shown.Remove(releaseLink.Name);
            Show(updateStatus, T("Checking…", "در حال بررسی…"));
            try
            {
                UpdateResult result = await releases.CheckAsync(BuildInfo.Version, lifetimeToken);
                if (IsDisposed || lifetimeToken.IsCancellationRequested)
                {
                    return;
                }
                switch (result.Status)
                {
                    case UpdateStatus.NewerAvailable:
                        ShowNewVersion(result.Version, result.ReleasePage);
                        break;
                    case UpdateStatus.Current:
                        Show(updateStatus, T("You have the latest version", "نسخهٔ شما به‌روز است"));
                        break;
                    case UpdateStatus.NoPublicRelease:
                        Show(updateStatus, T("No public release yet", "هنوز نسخهٔ عمومی منتشر نشده است"));
                        break;
                    case UpdateStatus.RateLimited:
                        Show(updateStatus, T("GitHub limit reached. Try again later.", "محدودیت گیت‌هاب؛ کمی بعد دوباره بررسی کنید"));
                        break;
                    case UpdateStatus.InvalidResponse:
                        Show(updateStatus, T("Release details could not be verified", "اطلاعات نسخهٔ منتشرشده قابل تأیید نیست"));
                        break;
                    default:
                        Show(updateStatus, T("Could not check. Check your connection.", "بررسی نشد؛ اتصال اینترنت را بررسی کنید"));
                        break;
                }
            }
            catch (OperationCanceledException) { }
            catch { if (!IsDisposed) { Show(updateStatus, T("Could not check. Try again later.", "بررسی نشد؛ کمی بعد دوباره تلاش کنید")); } }
            finally { if (!IsDisposed) { checkUpdates.Enabled = true; } }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && cleanup.TryBegin())
            {
                lifetime.Cancel();
                releases.Dispose();
                lifetime.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
