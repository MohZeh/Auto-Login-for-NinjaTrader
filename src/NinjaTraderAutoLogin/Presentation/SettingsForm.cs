using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace NinjaTraderAutoLogin
{
    internal sealed class SettingsForm : Form
    {
        private readonly LoginContext owner;
        private readonly ICredentialSettings credentials;
        private readonly PasswordEntryState passwordState = new PasswordEntryState();
        private readonly Button save, launch, close, settingsTab, aboutTab;
        private readonly Panel languages;
        private readonly RadioButton persianLanguage, englishLanguage;
        private readonly OptionsButton options;
        private readonly Button cancel;
        private readonly InWindowOptions optionsMenu;
        private readonly Button pause, exit, remove;
        private readonly LinkAction createShortcut, removeRejected;
        private ShortcutState shortcutState = ShortcutState.Missing;
        private readonly Label heading;
        private readonly Panel navigation;
        private AboutView aboutView;
        private bool aboutSelected;
        private readonly GlyphButton themeButton;
        private readonly Panel header, footer, statusRow, tradingModes;
        private readonly SettingsViewport body;
        private readonly Surface account, behavior, application;
        private readonly Label accountHeading, pathLabel, targetStatus, userLabel, passwordLabel, credentialHint, behaviorHeading, environmentLabel, startMethodLabel, behaviorHint, status;
        private TargetInspection target = new TargetInspection();
        private int targetCheck;
        private bool rejectionShown;
        private readonly InputFrame pathInput, userInput, passwordInput;
        private readonly RadioButton shortcutOption, watchOption;
        private readonly RadioButton simulationOption, liveOption;


        private readonly PictureBox mark;
        private readonly ToolTip tips = new ToolTip();
        private readonly DisabledControlToolTip passwordHelp;
        private readonly DisposeOnce cleanup = new DisposeOnce();
        private string language, storedUsername, theme;
        private LaunchMode selectedMode;
        private TradingMode selectedTradingMode;
        private bool initializing, closingUi, arranging, captureQueued, updatingAppearance;
        private bool fitQueued, fitting;
        private Rectangle lastWorkArea;
        private SettingsLayoutPlan currentLayout;
        private FormWindowState lastWindowState = FormWindowState.Normal;
        private readonly Dictionary<string, Control> layoutControls = new Dictionary<string, Control>();

        public SettingsForm(LoginContext owner, Settings current, ICredentialSettings credentials)
        {
            this.owner = owner;
            this.credentials = credentials;
            language = current.UiLanguage;
            selectedMode = current.Mode;
            selectedTradingMode = current.TradingSelection;
            theme = current.UiTheme;
            UiAssets.Palette = UiPalette.For(theme);
            initializing = true;
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9.25F);
            BackColor = UiAssets.Canvas;
            ForeColor = UiAssets.Ink;
            DoubleBuffered = true;
            ResizeRedraw = true;
            ClientSize = new Size(SettingsLayoutPlan.DesignWidth, SettingsLayoutPlan.DesignHeight);
            MinimumSize = new Size(220, 120);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            RightToLeftLayout = false;
            RightToLeft = RightToLeft.No;
            Name = "settings";
            Text = ProductInfo.Name(language);
            Icon = UiAssets.LoadIcon("settings.ico");

            header = Panel("header", this);
            mark = new PictureBox { Name = "brandMark", SizeMode = PictureBoxSizeMode.Zoom, TabStop = false };
            try
            {
                mark.Image = UiAssets.LoadBitmap("launch.png");
            }
            catch { mark.Visible = false; }
            header.Controls.Add(mark);
            heading = Label("heading", 13.5F, true, header);
            navigation = Panel("navigation", header);
            settingsTab = new NavigationTab { Name = "settingsTab" };
            navigation.Controls.Add(settingsTab);
            aboutTab = new NavigationTab { Name = "aboutTab" };
            navigation.Controls.Add(aboutTab);
            options = new OptionsButton { Name = "options" };
            navigation.Controls.Add(options);
            optionsMenu = new InWindowOptions(this, options);
            pause = optionsMenu.Pause;
            remove = optionsMenu.Remove;
            exit = optionsMenu.Exit;
            optionsMenu.Opening += delegate
            {
                HidePassword();
                RefreshStatus();
            };
            pause.Click += delegate
            {
                owner.ToggleMonitoring();
                RefreshStatus();
            };
            remove.Click += delegate
            {
                DeleteCredentials();
            };
            exit.Click += delegate
            {
                owner.ExitApplication();
            };
            settingsTab.Click += delegate
            {
                SelectPage(false);
            };
            aboutTab.Click += delegate
            {
                SelectPage(true);
            };
            heading.ForeColor = UiAssets.Title;
            languages = Panel("language", header);
            languages.TabIndex = 0;
            persianLanguage = new LanguageOption { Name = "languageFa", Text = "FA", TabIndex = 0, RightToLeft = RightToLeft.No };
            englishLanguage = new LanguageOption { Name = "languageEn", Text = "EN", TabIndex = 1, RightToLeft = RightToLeft.No };
            languages.Controls.Add(persianLanguage);
            languages.Controls.Add(englishLanguage);
            persianLanguage.CheckedChanged += delegate
            {
                if (persianLanguage.Checked)
                {
                    SelectLanguage("fa");
                }
            };
            englishLanguage.CheckedChanged += delegate
            {
                if (englishLanguage.Checked)
                {
                    SelectLanguage("en");
                }
            };
            themeButton = new GlyphButton { Name = "theme", BackColor = UiAssets.Canvas, TabIndex = 1 };
            header.Controls.Add(themeButton);
            themeButton.Click += delegate
            {
                HidePassword();
                UpdateAppearance(delegate
                {
                    theme = theme == "Dark" ? "Light" : "Dark";
                    ApplyTheme();
                });
                owner.SavePreferences(language, theme);
                QueueLayoutCapture("ThemeChanged");
            };
            tradingModes = new Panel { Name = "tradingModes", Margin = Padding.Empty };
            tradingModes.TabIndex = 0;
            simulationOption = Radio("simulationOption", tradingModes);
            liveOption = Radio("liveOption", tradingModes);
            simulationOption.CheckedChanged += delegate
            {
                if (simulationOption.Checked)
                {
                    selectedTradingMode = TradingMode.Simulation;
                }

                StyleChoices();
                QueueLayoutCapture("TradingModeChanged");
            };
            liveOption.CheckedChanged += delegate
            {
                if (liveOption.Checked)
                {
                    selectedTradingMode = TradingMode.Live;
                }

                StyleChoices();
                QueueLayoutCapture("TradingModeChanged");
            };

            body = new SettingsViewport { Name = "bodyViewport", Margin = Padding.Empty, Padding = Padding.Empty, RightToLeft = RightToLeft.No };
            Controls.Add(body);
            body.TabIndex = 0;
            body.AutoScroll = true;
            body.Scroll += delegate
            {
                if (!arranging && !fitting)
                {
                    QueueLayoutCapture("Scroll");
                }
            };
            body.Controls.Add(header);
            header.TabIndex = 0;
            account = new Surface { Name = "account", TabIndex = 2 };
            body.Controls.Add(account);
            accountHeading = Label("accountHeading", 9.5F, true, account);
            behavior = new Surface { Name = "startMethod", TabIndex = 3 };
            body.Controls.Add(behavior);
            application = new Surface { Name = "application", TabIndex = 4 };
            body.Controls.Add(application);
            pathLabel = Label("pathLabel", 9.5F, true, application);
            pathInput = new InputFrame(true, true) { Name = "pathInput", TabIndex = 0 };
            pathInput.Field.Name = "pathValue";
            pathInput.ActionButton.Name = "browseButton";
            pathInput.Field.Text = current.NinjaTraderPath;
            pathInput.ActionButton.Click += Browse;
            pathInput.Field.TextChanged += delegate
            {
                InspectTarget();
                ShowStatus();
            };
            application.Controls.Add(pathInput);
            targetStatus = Label("targetStatus", 9, false, application);
            userLabel = Label("usernameLabel", 9.5F, false, account);
            userInput = new InputFrame(false, false) { Name = "usernameInput", TabIndex = 0 };
            userInput.Field.Name = "usernameValue";
            userInput.Field.TextChanged += delegate
            {
                HidePassword();
                ShowStatus();
            };
            account.Controls.Add(userInput);
            passwordLabel = Label("passwordLabel", 9.5F, false, account);
            passwordInput = new InputFrame(true, false) { Name = "passwordInput", TabIndex = 1 };
            passwordInput.Field.Name = "passwordValue";
            passwordInput.Field.UseSystemPasswordChar = true;
            passwordInput.ActionButton.Name = "passwordVisibilityButton";
            passwordInput.ActionButton.Click += delegate
            {
                TogglePassword();
            };
            passwordInput.Field.TextChanged += delegate
            {
                passwordState.DraftChanged(passwordInput.Field.TextLength > 0);
                UpdateEye();
                ShowStatus();
            };
            account.Controls.Add(passwordInput);
            passwordHelp = new DisabledControlToolTip(passwordInput.ActionButton);
            credentialHint = Label("credentialHint", 9, false, account);
            credentialHint.ForeColor = UiAssets.Muted;

            behaviorHeading = Label("startMethodHeading", 9.5F, true, behavior);
            environmentLabel = Label("environmentLabel", 9.5F, false, behavior);
            behavior.Controls.Add(tradingModes);
            startMethodLabel = Label("startMethodLabel", 9.5F, false, behavior);
            shortcutOption = Radio("shortcutOption", behavior);
            watchOption = Radio("watchOption", behavior);
            shortcutOption.TabIndex = 1;
            watchOption.TabIndex = 2;
            shortcutOption.CheckedChanged += delegate
            {
                if (shortcutOption.Checked)
                {
                    selectedMode = LaunchMode.Shortcut;
                }

                StyleChoices();
            };
            watchOption.CheckedChanged += delegate
            {
                if (watchOption.Checked)
                {
                    selectedMode = LaunchMode.Watch;
                }

                StyleChoices();
            };
            behaviorHint = Label("startMethodHint", 9, false, behavior);
            behaviorHint.ForeColor = UiAssets.Muted;
            removeRejected = new LinkAction { Name = "removeRejected", TabIndex = 2, Visible = false };
            account.Controls.Add(removeRejected);
            removeRejected.Click += delegate
            {
                DeleteCredentials();
            };
            createShortcut = new LinkAction { Name = "createShortcut", TabIndex = 3 };
            behavior.Controls.Add(createShortcut);
            createShortcut.Click += delegate
            {
                CreateDesktopShortcuts();
            };
            statusRow = Panel("statusRow", body);
            statusRow.TabIndex = 5;
            cancel = Button("cancelLogin", false, statusRow);
            cancel.Click += delegate
            {
                owner.CancelCurrent();
            };
            status = Label("status", 9.25F, false, statusRow);
            status.AutoEllipsis = true;
            footer = Panel("footer", this);
            footer.TabIndex = 6;
            save = Button("saveSettings", true, footer);
            launch = Button("saveAndLaunch", false, footer);
            close = Button("closeSettings", false, footer);
            save.TabIndex = 2;
            launch.TabIndex = 1;
            close.TabIndex = 0;
            save.Click += delegate
            {
                Save();
            };
            launch.Click += delegate
            {
                if (Save())
                {
                    owner.LaunchFromSettings();
                }
            };
            close.Click += delegate
            {
                Close();
            };
            AcceptButton = save;
            CancelButton = close;
            try
            {
                CredentialMetadata metadata = credentials.ReadMetadata();
                storedUsername = metadata == null ? null : metadata.Username;
                passwordState.LoadSaved(metadata != null && metadata.HasPassword);
                userInput.Field.Text = storedUsername ?? String.Empty;
            }
            catch (Exception error) { AppFaults.Record("ReadCredentialMetadata", error); }
            foreach (Control control in new Control[]{body,header,account,behavior,application,accountHeading,environmentLabel,startMethodLabel,targetStatus,statusRow,footer,mark,heading,navigation,settingsTab,aboutTab,options,languages,persianLanguage,englishLanguage,themeButton,tradingModes,cancel,simulationOption,liveOption,
                pathLabel,pathInput,userLabel,userInput,passwordLabel,passwordInput,credentialHint,removeRejected,behaviorHeading,shortcutOption,watchOption,behaviorHint,createShortcut,status,save,launch,close})
            {
                layoutControls.Add(control.Name, control);
            }

            InspectShortcuts();
            ApplyLanguage();
            ApplyFonts();
            ApplyTheme();
            initializing = false;
            ResumeLayout(true);
            Deactivate += delegate
            {
                if (!closingUi)
                {
                    HidePassword();
                }
            };
        }
        private static Panel Panel(string name, Control parent)
        {
            var panel = new Panel { Name = name, Margin = Padding.Empty, Padding = Padding.Empty, RightToLeft = RightToLeft.No };
            parent.Controls.Add(panel);
            return panel;
        }
        private static Label Label(string name, float size, bool bold, Control parent)
        {
            var label = new Label
            {
                Name = name,
                AutoSize = false,
                UseCompatibleTextRendering = false,
                UseMnemonic = false,
                Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = UiAssets.Ink,
                Margin = Padding.Empty,
                TextAlign = ContentAlignment.MiddleLeft
            };
            parent.Controls.Add(label);
            return label;
        }
        private static Button Button(string name, bool primary, Control parent)
        {
            var button = new ActionButton { Name = name, Kind = primary ? ActionKind.Primary : ActionKind.Secondary };
            parent.Controls.Add(button);
            return button;
        }
        private static RadioButton Radio(string name, Control parent)
        {
            var radio = new FilledRadioButton
            {
                Name = name,
                AutoSize = false,
                FlatStyle = FlatStyle.Standard,
                UseMnemonic = false,
                UseVisualStyleBackColor = false,
                BackColor = UiAssets.Surface,
                ForeColor = UiAssets.Ink,
                Margin = Padding.Empty,
                TextAlign = ContentAlignment.MiddleLeft,
                CheckAlign = ContentAlignment.MiddleLeft
            };
            parent.Controls.Add(radio);
            return radio;
        }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (initializing || closingUi || updatingAppearance || arranging || WindowState == FormWindowState.Minimized || layoutControls == null || layoutControls.Count == 0)
            {
                return;
            }

            arranging = true;
            try
            {
                if (aboutView != null && !aboutSelected)
                {
                    aboutView.Visible = false;
                }
                body.AutoScroll = !aboutSelected;
                int dpi = UiDpi.Get(this);
                int scrollbar = 0;
                var plan = new SettingsLayoutPlan(ClientSize.Width, ClientSize.Height, dpi, language == "fa", scrollbar);
                if (aboutSelected)
                {
                    body.SuspendLayout();
                    try
                    {
                        body.AutoScrollMinSize = Size.Empty;
                        body.ResetScrollbarsForLayout();
                        foreach (var entry in plan.Bounds)
                        {
                            string parent = plan.Parents[entry.Key];
                            if (parent == "header" || parent == "navigation" || parent == "language")
                            {
                                layoutControls[entry.Key].Bounds = entry.Value;
                            }
                        }
                        LayoutSelectedPage(plan);
                        if (optionsMenu.Visible)
                        {
                            optionsMenu.Reposition();
                        }
                    }
                    finally { body.ResumeLayout(true); }
                    return;
                }
                for (int pass = 0; pass < 3; pass++)
                {
                    int needed = !aboutSelected && plan.PageContentHeight > plan.Bounds["bodyViewport"].Height ? UiDpi.ScrollbarReserve(dpi) : 0;
                    if (needed == scrollbar)
                    {
                        break;
                    }

                    scrollbar = needed;
                    plan = new SettingsLayoutPlan(ClientSize.Width, ClientSize.Height, dpi, language == "fa", scrollbar);
                }
                Rectangle viewport = plan.Bounds["bodyViewport"];
                int scrollY = Math.Max(0, -body.AutoScrollPosition.Y);
                bool extentChanged = currentLayout == null || currentLayout.Bounds["bodyViewport"] != viewport ||
                    currentLayout.PageContentHeight != plan.PageContentHeight || currentLayout.Bounds["header"].Width != plan.Bounds["header"].Width;
                body.SuspendLayout();
                try
                {
                    if (extentChanged || body.HorizontalScroll.Visible || body.VerticalScroll.Visible != (plan.PageContentHeight > viewport.Height))
                    {
                        body.ResetScrollbarsForLayout();
                    }

                    body.Bounds = viewport;
                    foreach (var entry in plan.Parents)
                    {
                        Control control = layoutControls[entry.Key], parent = entry.Value == "settings" ? (Control)this : layoutControls[entry.Value];
                        if (control.Parent != parent)
                        {
                            parent.Controls.Add(control);
                        }
                    }
                    body.AutoScrollMinSize = new Size(0, plan.PageContentHeight);

                    Point offset = body.AutoScrollPosition;
                    foreach (var entry in plan.Bounds)
                    {
                        Control control = layoutControls[entry.Key];
                        Rectangle bounds = entry.Value;
                        if (control.Parent == body)
                        {
                            bounds.Offset(offset);
                        }

                        control.Bounds = bounds;
                    }
                    ((FilledRadioButton)shortcutOption).WrapText = plan.Compact;
                    ((FilledRadioButton)watchOption).WrapText = plan.Compact;
                    currentLayout = plan;
                }
                finally { body.ResumeLayout(true); }
                // Restore only after WinForms has recomputed the new page range.
                // Restoring earlier can retain a stale horizontal/vertical pair.
                scrollY = Math.Min(scrollY, Math.Max(0, plan.PageContentHeight - body.ClientSize.Height));
                if (body.AutoScrollPosition.X != 0 || body.AutoScrollPosition.Y != -scrollY)
                {
                    body.AutoScrollPosition = new Point(0, scrollY);
                }
                LayoutSelectedPage(plan);
                if (optionsMenu.Visible)
                {
                    optionsMenu.Reposition();
                }
            }
            finally { arranging = false; }
        }
        private string T(string en, string fa)
        {
            return UiText.Choose(language, en, fa);
        }
        private void StyleNavigation()
        {
            ((NavigationTab)settingsTab).Selected = !aboutSelected;
            ((NavigationTab)aboutTab).Selected = aboutSelected;
            foreach (Button tab in new[] { settingsTab, aboutTab })
            {
                tab.Font = UiFonts.Get(language, 9.25F, ((NavigationTab)tab).Selected ? FontStyle.Bold : FontStyle.Regular);
                tab.BackColor = UiAssets.Canvas;
                tab.ForeColor = UiAssets.Ink;
                tab.Invalidate();
            }
        }
        private void SelectPage(bool about)
        {
            HidePassword();
            aboutSelected = about;
            if (aboutSelected && aboutView == null)
            {
                RecreateAbout();
            }
            body.AutoScrollPosition = Point.Empty;
            StyleNavigation();

            PerformLayout();
            QueueLayoutCapture(aboutSelected ? "AboutPage" : "SettingsPage");
        }
        private void RecreateAbout()
        {
            AboutView previous = aboutView;
            aboutView = null;
            if (previous != null)
            {
                previous.Dispose();
            }
            if (aboutSelected)
            {
                aboutView = new AboutView(language) { Visible = false };
                body.Controls.Add(aboutView);
            }
        }
        private void LayoutSelectedPage(SettingsLayoutPlan plan)
        {
            account.Visible = behavior.Visible = application.Visible = statusRow.Visible = save.Visible = launch.Visible = !aboutSelected;
            footer.Visible = !aboutSelected || !plan.FooterInScroll;
            body.AutoScroll = !aboutSelected;
            if (!aboutSelected)
            {
                return;
            }
            int pad = plan.Bounds["bodyViewport"].Top;
            body.AutoScrollMinSize = Size.Empty;
            int bottom = ClientSize.Height - pad;
            if (footer.Visible)
            {
                // The About page has one command, so it sits alone in the corner.
                if (footer.Parent != this)
                {
                    Controls.Add(footer);
                }

                int closeHeight = plan.Bounds["closeSettings"].Height;
                Rectangle bar = plan.Bounds["footer"];
                footer.Bounds = new Rectangle(bar.Left, bar.Bottom - closeHeight, bar.Width, closeHeight);
                int closeWidth = plan.Compact ? bar.Width : plan.Bounds["closeSettings"].Width;
                close.Bounds = new Rectangle(bar.Width - closeWidth, 0, closeWidth, closeHeight);
                bottom = footer.Top - plan.Px(6);
            }

            body.Bounds = new Rectangle(pad, pad, Math.Max(1, ClientSize.Width - pad * 2), Math.Max(1, bottom - pad));
            int headerHeight = plan.Bounds["header"].Height;
            header.Bounds = new Rectangle(0, 0, body.ClientSize.Width, headerHeight);
            if (aboutView != null)
            {
                aboutView.Bounds = new Rectangle(0, headerHeight + plan.Px(6), body.ClientSize.Width, Math.Max(1, body.ClientSize.Height - headerHeight - plan.Px(6)));
                aboutView.Visible = true;
            }
        }
        private void SelectLanguage(string selected)
        {
            if (language == selected || initializing)
            {
                return;
            }
            HidePassword();
            UpdateAppearance(delegate
            {
                language = selected;
                ApplyLanguage();
                ApplyFonts();
                RecreateAbout();
                ApplyTheme();
            });
            owner.SavePreferences(language, theme);
            QueueLayoutCapture("LanguageChanged");
        }
        private void UpdateAppearance(Action update)
        {
            updatingAppearance = true;
            SuspendLayout();
            body.SuspendLayout();
            try
            {
                update();
            }
            finally
            {
                updatingAppearance = false;
                ResumeLayout(false);
                PerformLayout();
                body.ResumeLayout(true);
                Invalidate(true);
            }
        }
        private void ApplyLanguage()
        {
            bool rtl = language == "fa";
            SuspendLayout();
            foreach (Label label in new[] { heading, accountHeading, pathLabel, targetStatus, userLabel, passwordLabel, credentialHint, behaviorHeading, environmentLabel, startMethodLabel, behaviorHint, status })
            {
                label.RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No;
                label.TextAlign = ContentAlignment.MiddleLeft;
            }
            foreach (RadioButton radio in new[] { shortcutOption, watchOption, simulationOption, liveOption })
            {
                radio.RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No;
                radio.CheckAlign = ContentAlignment.MiddleLeft;
                radio.TextAlign = ContentAlignment.MiddleLeft;
            }
            foreach (Control button in new Control[] { save, launch, close, createShortcut, removeRejected, settingsTab, aboutTab, options })
            {
                button.RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No;
            }

            heading.Text = ProductInfo.Name(language);
            settingsTab.Text = T("Setup", "تنظیمات");
            aboutTab.Text = T("About", "دربارهٔ برنامه");
            options.SetCaption(T("Options", "گزینه‌ها"), T("Application options", "گزینه‌های برنامه"));
            optionsMenu.RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No;
            languages.RightToLeft = RightToLeft.No;
            persianLanguage.Checked = rtl;
            englishLanguage.Checked = !rtl;
            accountHeading.Text = T("NinjaTrader account", "حساب نینجاتریدر");
            pathLabel.Text = T("NinjaTrader application", "برنامهٔ نینجاتریدر");
            environmentLabel.Text = T("Environment", "محیط معاملاتی");
            startMethodLabel.Text = T("Start with", "روش شروع");
            userLabel.Text = T("Username", "نام کاربری");
            passwordLabel.Text = T("Password", "رمز عبور");

            removeRejected.Text = T("Remove saved login", "حذف اطلاعات ورود");
            remove.Text = T("Remove saved login", "حذف اطلاعات ورود");
            behaviorHeading.Text = T("Login behavior", "رفتار ورود");
            tradingModes.AccessibleName = environmentLabel.Text;
            simulationOption.Text = T("Simulation", "سیمولیشن");
            liveOption.Text = T("Live trading", "لایو ترید");
            shortcutOption.Text = T("Dedicated shortcut", "شورتکات اختصاصی");
            watchOption.Text = T("Watch NinjaTrader", "پایش نینجاتریدر");
            save.Text = T("Save settings", "ذخیرهٔ تنظیمات");
            launch.Text = T("Save and launch", "ذخیره و اجرا");
            close.Text = T("Close", "بستن");
            cancel.Text = T("Cancel login", "لغو ورود جاری");
            exit.Text = T("Exit", "خروج از برنامه");
            tips.SetToolTip(exit, T("Exit the app and stop watching. NinjaTrader stays open.", "خروج از برنامه و پایان پایش؛ نینجاتریدر باز می‌ماند."));
            tips.SetToolTip(close, T("Close this window. Watching stays active when enabled.", "بستن این پنجره؛ پایش فعال در کنار ساعت ادامه می‌یابد."));


            languages.AccessibleName = T("Language", "زبان");
            persianLanguage.AccessibleName = T("Persian", "فارسی");
            englishLanguage.AccessibleName = T("English", "انگلیسی");
            pathInput.Field.AccessibleName = pathLabel.Text;
            userInput.Field.AccessibleName = userLabel.Text;
            passwordInput.Field.AccessibleName = passwordLabel.Text;
            pathInput.ActionButton.AccessibleName = T("Choose NinjaTrader application", "انتخاب فایل نینجاتریدر");
            tips.SetToolTip(pathInput.ActionButton, pathInput.ActionButton.AccessibleName);
            tips.SetToolTip(persianLanguage, persianLanguage.AccessibleName);
            tips.SetToolTip(englishLanguage, englishLanguage.AccessibleName);

            tips.SetToolTip(launch, T("Save settings and open NinjaTrader in the selected mode.", "ذخیرهٔ تنظیمات و ورود به نینجاتریدر با محیط انتخاب‌شده."));
            shortcutOption.Checked = selectedMode == LaunchMode.Shortcut;
            watchOption.Checked = selectedMode == LaunchMode.Watch;
            simulationOption.Checked = selectedTradingMode == TradingMode.Simulation;
            liveOption.Checked = selectedTradingMode == TradingMode.Live;
            tips.SetToolTip(simulationOption, T("Select Simulation on the next sign-in.", "انتخاب سیمولیشن هنگام ورود تازه."));
            tips.SetToolTip(liveOption, T("Select Live trading on the next sign-in.", "انتخاب لایو ترید هنگام ورود تازه."));
            StyleChoices();
            UpdateEye();
            ShowTarget();
            RefreshStatus();
            ResumeLayout(true);
        }
        private void ApplyFonts()
        {
            Font = UiFonts.Get(language, Font.SizeInPoints, Font.Style);
            foreach (Control control in new Control[] { heading, accountHeading, pathLabel, targetStatus, userLabel, passwordLabel, credentialHint, behaviorHeading, environmentLabel, startMethodLabel, behaviorHint, status, save, launch, close, createShortcut, shortcutOption, watchOption, simulationOption, liveOption, cancel, settingsTab, aboutTab, options })
            {
                control.Font = UiFonts.Get(language, control.Font.SizeInPoints, control.Font.Style);
            }

            heading.Font = UiFonts.Get(language, 13.5F, FontStyle.Bold);
            optionsMenu.Font = UiFonts.Get(language, 9.25F, FontStyle.Regular);
            persianLanguage.Font = UiFonts.Get("en", 9.25F, persianLanguage.Checked ? FontStyle.Bold : FontStyle.Regular);
            englishLanguage.Font = UiFonts.Get("en", 9.25F, englishLanguage.Checked ? FontStyle.Bold : FontStyle.Regular);
            foreach (InputFrame frame in new[] { pathInput, userInput, passwordInput })
            {
                frame.Field.Font = UiFonts.Get("en", frame.Field.Font.SizeInPoints, FontStyle.Regular);
            }


            UpdateEye();
            PerformLayout();
        }
        private void StyleButton(Button button, bool primary)
        {
            button.BackColor = primary ? UiAssets.Primary : UiAssets.Surface;
            button.ForeColor = primary ? Color.White : UiAssets.Ink;
            button.FlatAppearance.BorderColor = primary ? UiAssets.Primary : UiAssets.Border;
            button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(190, 46, 0) : UiAssets.Palette.Hover;
        }
        private void ApplyTheme()
        {
            UiAssets.Palette = UiPalette.For(theme);
            BackColor = UiAssets.Canvas;
            ForeColor = UiAssets.Ink;
            foreach (Control panel in new Control[] { header, navigation, body, footer, statusRow, languages, persianLanguage, englishLanguage, options })
            {
                panel.BackColor = UiAssets.Canvas;
                panel.ForeColor = UiAssets.Ink;
            }
            tradingModes.BackColor = UiAssets.Surface;
            foreach (Label label in new[] { heading, accountHeading, pathLabel, userLabel, passwordLabel, credentialHint, behaviorHeading, environmentLabel, startMethodLabel, behaviorHint, status })
            {
                label.ForeColor = UiAssets.Ink;
            }

            heading.ForeColor = UiAssets.Title;
            credentialHint.ForeColor = behaviorHint.ForeColor = UiAssets.Muted;
            foreach (InputFrame frame in new[] { pathInput, userInput, passwordInput })
            {
                frame.BackColor = frame.Field.BackColor = UiAssets.Input;
                frame.Field.ForeColor = UiAssets.Ink;
                if (frame.ActionButton != null)
                {
                    frame.ActionButton.BackColor = UiAssets.Input;
                }
            }
            optionsMenu.ApplyPalette();
            StyleButton(themeButton, false);
            themeButton.BackColor = UiAssets.Canvas;
            themeButton.FlatAppearance.BorderSize = 0;
            foreach (RadioButton radio in new[] { shortcutOption, watchOption, simulationOption, liveOption })
            {
                radio.BackColor = UiAssets.Surface;
            }

            themeButton.Sun = theme == "Dark";
            themeButton.Moon = theme == "Light";
            themeButton.AccessibleName = theme == "Dark" ? T("Switch to light theme", "تغییر به تم روشن") : T("Switch to dark theme", "تغییر به تم تیره");
            tips.SetToolTip(themeButton, themeButton.AccessibleName);

            ApplyCaption();

            StyleChoices();
            StyleNavigation();

            if (aboutView != null)
            {
                aboutView.ApplyPalette();
            }
            UpdateEye();
            ShowTarget();
            RefreshStatus();
            Invalidate(true);
        }
        private void StyleChoices()
        {
            if (shortcutOption == null || watchOption == null)
            {
                return;
            }

            shortcutOption.ForeColor = shortcutOption.Checked ? UiAssets.Accent : UiAssets.Ink;
            watchOption.ForeColor = watchOption.Checked ? UiAssets.Accent : UiAssets.Ink;
            if (simulationOption != null)
            {
                simulationOption.ForeColor = simulationOption.Checked ? UiAssets.Simulation : UiAssets.Ink;
            }

            if (liveOption != null)
            {
                liveOption.ForeColor = liveOption.Checked ? UiAssets.Live : UiAssets.Ink;
                ((FilledRadioButton)liveOption).Badge = liveOption.Checked ? LiveBadge.Text : null;
            }

            if (behaviorHint != null)
            {
                behaviorHint.Text = selectedMode == LaunchMode.Watch
                ? T("Starts with Windows. Open NinjaTrader as usual.", "پایش با ویندوز فعال می‌شود؛ نینجاتریدر را معمولی اجرا کنید.")
                : shortcutState == ShortcutState.Ready ? T("Use the Auto Login shortcut to sign in.", "برای ورود، شورتکات ورود خودکار را اجرا کنید.")
                : T("Create the desktop shortcut, then use it to sign in.", "شورتکات را روی دسکتاپ بسازید و با آن وارد شوید.");
            }

            if (createShortcut != null)
            {
                createShortcut.Visible = selectedMode == LaunchMode.Shortcut;
                createShortcut.Text = UiText.ShortcutAction(language, shortcutState);
                createShortcut.Ready = shortcutState == ShortcutState.Ready;
                tips.SetToolTip(createShortcut, T("Adds the Auto Login and Settings shortcuts to your desktop.", "شورتکات‌های ورود خودکار و تنظیمات را به دسکتاپ اضافه می‌کند."));
            }

            ShowStatus();
        }
        public void RefreshStatus()
        {
            if (status == null)
            {
                return;
            }

            if (owner.CredentialsRejected != rejectionShown)
            {
                UpdateEye();
            }

            ShowStatus();
            Text = (owner.SavedTradingMode == TradingMode.Live ? "LIVE · " : String.Empty) + ProductInfo.Name(language);
            ApplyCaption();
            if (pause != null)
            {
                pause.Enabled = owner.WatchEnabled;
                pause.Text = owner.Paused ? T("Resume watching", "ادامهٔ پایش") : T("Pause watching", "توقف موقت پایش");
                tips.SetToolTip(pause, owner.WatchEnabled ? String.Empty : T("Save the Watch NinjaTrader start method to turn on watching.", "برای فعال‌شدن پایش، روش پایش اجرای نینجاتریدر را انتخاب و ذخیره کنید."));
            }
            if (cancel != null)
            {
                cancel.Visible = owner.Running;
            }

            if (exit != null)
            {
                exit.Enabled = true;
            }

            if (launch != null)
            {
                launch.Enabled = !owner.Running;
            }

            if (save != null)
            {
                save.Enabled = !owner.Running;
            }
            string busyHint = T("Checking NinjaTrader or signing in. Available when the operation finishes.", "در حال بررسی نینجاتریدر یا ورود؛ پس از پایان عملیات فعال می‌شود.");
            tips.SetToolTip(save, owner.Running ? busyHint : String.Empty);
            tips.SetToolTip(launch, owner.Running ? busyHint : T("Save settings and open NinjaTrader in the selected mode.", "ذخیرهٔ تنظیمات و ورود به نینجاتریدر با محیط انتخاب‌شده."));

            if (remove != null)
            {
                remove.Enabled = !owner.Running;
            }

            PerformLayout();
        }
        // The title bar shows the saved environment, using the same meaning as NinjaTrader's mode screen.
        private void ApplyCaption()
        {
            if (!IsHandleCreated)
            {
                return;
            }

            bool live = owner.SavedTradingMode == TradingMode.Live;
            NativeAppearance.ThemeCaption(Handle);
            NativeAppearance.TintCaption(Handle, live ? UiAssets.Palette.CaptionLive : UiAssets.Palette.CaptionSimulation, UiAssets.Ink, live ? UiAssets.Palette.FrameLive : UiAssets.Palette.FrameSimulation);
            Invalidate(false);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            UiAssets.DrawWindowFrame(e.Graphics, ClientSize, owner.SavedTradingMode == TradingMode.Live ? UiAssets.Palette.FrameLive : UiAssets.Palette.FrameSimulation, UiDpi.Get(this) / 96F);
        }
        private bool HasUnsavedChanges()
        {
            return SettingsNoticePolicy.Differs(owner.SavedSettings, pathInput.Field.Text, selectedMode, selectedTradingMode,
                userInput.Field.Text, storedUsername, passwordInput.Field.TextLength > 0);
        }
        private bool Agrees(ConfirmationContent content)
        {
            using (var dialog = new ConfirmationDialog(language, content))
            {
                return dialog.ShowDialog(this) == DialogResult.Yes;
            }
        }
        private void ShowStatus()
        {
            if (status == null)
            {
                return;
            }

            SettingsNotice notice = SettingsNoticePolicy.Select(owner.Running || owner.StatusIsError, HasUnsavedChanges(), owner.StatusIdle,
                passwordState.HasSavedPassword && !String.IsNullOrWhiteSpace(storedUsername), System.IO.File.Exists(owner.SavedExecutablePath));
            status.Text = UiText.Notice(notice, language, owner.SavedTradingMode) ?? owner.GetStatus(language);
            status.ForeColor = owner.StatusIsError || notice == SettingsNotice.MissingCredentials || notice == SettingsNotice.MissingExecutable
                ? UiAssets.Error : notice == SettingsNotice.Unsaved ? UiAssets.Ink : UiAssets.Muted;
            // "Ready" would contradict the warning in the account card.
            if (notice == SettingsNotice.None && owner.StatusIdle && owner.CredentialsRejected)
            {
                status.Text = T("Automatic sign-in is on hold until the login is fixed.", "ورود خودکار تا اصلاح اطلاعات ورود متوقف است.");
                status.ForeColor = UiAssets.Error;
            }

            tips.SetToolTip(status, status.Text);
        }
        private void Feedback(string english, string persian, bool error)
        {
            status.Text = T(english, persian);
            status.ForeColor = error ? UiAssets.Error : UiAssets.Ink;
            tips.SetToolTip(status, status.Text);
            if (statusRow.Parent == body)
            {
                body.ScrollControlIntoView(status);
            }
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyCaption();
            NativeAppearance.HideCaptionText(Handle);
        }
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            PerformLayout();
            QueueLayoutCapture("Shown");
        }
        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            ScheduleFit("DpiChanged");
        }
        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);
            if (initializing || fitting || closingUi || body == null || !IsHandleCreated || !Visible)
            {
                return;
            }

            if (Screen.FromControl(this).WorkingArea != lastWorkArea)
            {
                ScheduleFit("MonitorChanged");
            }
        }
        protected override void OnResizeEnd(EventArgs e)
        {
            base.OnResizeEnd(e);
            FitToWorkingArea(false);
            QueueLayoutCapture("ResizeEnd");
        }
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            FormWindowState state = WindowState;
            bool restored = state == FormWindowState.Normal && lastWindowState != FormWindowState.Normal;
            lastWindowState = state;
            if (restored)
            {
                ScheduleFit("WindowRestored");
            }
        }
        protected override void WndProc(ref Message message)
        {
            int id = message.Msg;
            base.WndProc(ref message);
            if (id == 0x007E || id == 0x001A)
            {
                ScheduleFit("DisplayOrWorkAreaChanged");
            }
        }
        private void ScheduleFit(string reason)
        {
            if (initializing || fitting || closingUi || IsDisposed || !IsHandleCreated || body == null || fitQueued)
            {
                return;
            }

            fitQueued = true;
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    fitQueued = false;
                    if (IsDisposed || closingUi)
                    {
                        return;
                    }

                    FitToWorkingArea(false);
                    QueueLayoutCapture(reason);
                });
            }
            catch (InvalidOperationException) { fitQueued = false; }
        }
        private void FitToWorkingArea(bool center)
        {
            if (fitting || closingUi || IsDisposed || !IsHandleCreated || WindowState == FormWindowState.Minimized)
            {
                return;
            }

            fitting = true;
            try
            {
                Rectangle work = Screen.FromControl(this).WorkingArea;
                Size frame = new Size(Math.Max(0, Width - ClientSize.Width), Math.Max(0, Height - ClientSize.Height));
                WindowFit fit = WindowFitPolicy.Calculate(work, Bounds, frame, UiDpi.Get(this), center);
                MinimumSize = fit.MinimumOuter;
                if (WindowState == FormWindowState.Normal)
                {
                    Bounds = fit.Bounds;
                }

                lastWorkArea = work;
                PerformLayout();
            }
            finally { fitting = false; }
        }
        private void QueueLayoutCapture(string reason)
        {
            if (aboutSelected)
            {
                return;
            }
            if (closingUi || initializing || !IsHandleCreated || !Visible || WindowState == FormWindowState.Minimized || captureQueued)
            {
                return;
            }

            captureQueued = true;
            BeginInvoke((MethodInvoker)delegate
            {
                captureQueued = false;
                if (aboutSelected || IsDisposed || closingUi || !Visible || WindowState == FormWindowState.Minimized)
                {
                    return;
                }

                PerformLayout();
                SettingsLayoutEvidence.Record(this, language, theme, selectedTradingMode.ToString(), reason, currentLayout, body, lastWorkArea);
            });
        }

        // Reading the signature touches the disk, so it never runs on the window's thread.
        private void InspectTarget()
        {
            if (initializing || closingUi || !IsHandleCreated)
            {
                return;
            }

            string path = pathInput.Field.Text.Trim();
            int check = ++targetCheck;
            System.Threading.Tasks.Task.Run(delegate
            {
                TargetInspection result = TargetInspection.Of(path);
                try
                {
                    BeginInvoke((Action)delegate
                    {
                        if (check == targetCheck && !closingUi)
                        {
                            target = result;
                            ShowTarget();
                        }
                    });
                }
                catch (InvalidOperationException) { }
            });
        }
        private void ShowTarget()
        {
            targetStatus.Text = UiText.TargetStatus(language, target);
            targetStatus.ForeColor = target.Exists && target.Trusted && target.Status == NinjaTraderVersionStatus.Tested ? UiAssets.Simulation : UiAssets.Live;
        }
        private void InspectShortcuts()
        {
            try
            {
                shortcutState = DesktopShortcuts.Inspect(DesktopShortcuts.DesktopFolder, Paths.Executable);
            }
            catch (Exception error)
            {
                AppFaults.Record("InspectShortcuts", error);
                shortcutState = ShortcutState.Missing;
            }
        }
        private void CreateDesktopShortcuts()
        {
            try
            {
                DesktopShortcuts.Write(DesktopShortcuts.DesktopFolder, Paths.Executable);
                InspectShortcuts();
                StyleChoices();
                Feedback("The shortcuts are on your desktop.", "شورتکات‌ها روی دسکتاپ ساخته شدند.", false);
            }
            catch (InvalidOperationException)
            {
                Feedback("A desktop shortcut with this name belongs to another application, so it was not changed.", "شورتکاتی با این نام روی دسکتاپ متعلق به برنامهٔ دیگری است و تغییر نکرد.", true);
            }
            catch (Exception error)
            {
                AppFaults.Record("CreateDesktopShortcuts", error);
                Feedback("The shortcuts could not be created.", "ساخت شورتکات انجام نشد.", true);
            }
        }
        private void Browse(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog { Filter = "NinjaTrader.exe|NinjaTrader.exe", CheckFileExists = true, FileName = "NinjaTrader.exe", Title = T("Choose NinjaTrader", "انتخاب نینجاتریدر") })
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    pathInput.Field.Text = dialog.FileName;
                }
            }
        }
        private void TogglePassword()
        {
            passwordState.ToggleReveal();
            UpdateEye();
        }
        private void HidePassword()
        {
            if (passwordHelp != null)
            {
                passwordHelp.Hide();
            }
            if (closingUi || passwordInput == null || passwordInput.Field.IsDisposed)
            {
                return;
            }

            passwordState.Conceal();
            UpdateEye();
        }
        private void UpdateEye()
        {
            if (closingUi || passwordInput == null || passwordInput.ActionButton.IsDisposed)
            {
                return;
            }

            passwordInput.Field.UseSystemPasswordChar = !passwordState.IsRevealed;
            passwordInput.ActionButton.Enabled = passwordState.CanReveal;
            passwordInput.ActionButton.Slash = passwordState.IsRevealed;
            passwordInput.ActionButton.AccessibleName = passwordState.IsRevealed ? T("Hide new password", "پنهان‌کردن رمز جدید") : T("Show new password", "نمایش رمز جدید");
            bool usernameMatches = String.Equals(userInput.Field.Text.Trim(), storedUsername, StringComparison.Ordinal);
            string description = T("A password is saved. Type to replace it.", "رمز ذخیره شده است؛ برای تغییر آن، رمز جدید وارد کنید.");
            passwordInput.ShowSavedPassword(passwordState.ShowSavedMask(usernameMatches), description);
            credentialHint.Text = UiText.PasswordHint(language, passwordState.HasSavedPassword && usernameMatches, passwordState.CanReveal);
            passwordInput.SetInputToolTip(tips, T("You can enter a new password.", "می‌توانید رمز جدید وارد کنید."));
            passwordInput.Field.AccessibleDescription = passwordState.ShowSavedMask(usernameMatches) ? description : String.Empty;
            string help = passwordState.CanReveal ? passwordInput.ActionButton.AccessibleName : passwordState.ShowSavedMask(usernameMatches)
                ? T("A saved password exists and cannot be displayed.", "رمز ذخیره‌شده موجود است و قابل نمایش نیست.")
                : T("Enter a new password to enable preview.", "برای فعال‌شدن نمایش، رمز جدید وارد کنید.");
            passwordInput.ActionButton.AccessibleDescription = help;
            tips.SetToolTip(passwordInput.ActionButton, help);
            passwordHelp.Text = passwordState.CanReveal ? String.Empty : help;
            passwordInput.ActionButton.Invalidate();
            ShowRejection();
        }
        // A refused login stays visible until it is resolved. The password is kept on purpose:
        // the app cannot tell a wrong password from a locked account or a failed connection.
        private void ShowRejection()
        {
            rejectionShown = owner.CredentialsRejected;
            removeRejected.Visible = rejectionShown;
            credentialHint.ForeColor = rejectionShown ? UiAssets.Error : UiAssets.Muted;
            credentialHint.Font = UiFonts.Get(language, 9, rejectionShown ? FontStyle.Bold : FontStyle.Regular);
            if (rejectionShown)
            {
                credentialHint.Text = UiText.RejectedHint(language, owner.RejectionUncertain);
            }
        }
        public void FocusRejectedPassword()
        {
            if (owner.CredentialsRejected)
            {
                SelectPage(false);
                passwordInput.Field.Focus();
            }
        }
        private bool Save()
        {
            HidePassword();
            if (owner.Running)
            {
                return false;
            }

            string user = userInput.Field.Text.Trim();
            if (String.IsNullOrWhiteSpace(user) || (String.IsNullOrEmpty(passwordInput.Field.Text) &&
                (!passwordState.HasSavedPassword || !String.Equals(user, storedUsername, StringComparison.Ordinal))))
            {
                Feedback("Enter a username and password. Changing the username also requires its password.", "نام کاربری و رمز را وارد کنید؛ تغییر نام کاربری به ثبت دوبارهٔ رمز نیاز دارد.", true);
                userInput.Field.Focus();
                return false;
            }
            if (!ConfirmLive())
            {
                return false;
            }
            if (selectedMode == LaunchMode.Watch && owner.SavedLaunchMode != LaunchMode.Watch &&
                owner.IsNinjaTraderOpen(pathInput.Field.Text.Trim()) && !Agrees(ConfirmationContent.WatchWhileOpen(language)))
            {
                return false;
            }
            try
            {
                owner.SaveConfiguration(new Settings { NinjaTraderPath = pathInput.Field.Text.Trim(), LaunchMethod = selectedMode == LaunchMode.Watch ? "Watch" : "Shortcut", UiLanguage = language, UiTheme = theme, TradingMode = selectedTradingMode.ToString() }, user, passwordInput.Field.Text);
                storedUsername = user;
                passwordState.Saved();
                passwordInput.Field.Clear();
                HidePassword();
                RefreshStatus();
                if (statusRow.Parent == body)
                {
                    body.ScrollControlIntoView(status);
                }

                QueueLayoutCapture("Saved");
                return true;
            }
            catch (Exception error)
            {
                AppFaults.Record("SaveSettings", error);
                var configurationError = error as ConfigurationSaveException;
                if (configurationError != null && configurationError.RecoveryIncomplete)
                {
                    Feedback("Settings could not be fully restored. Review and save them again before resuming watching.",
                        "بازیابی تنظیمات کامل نشد؛ پیش از ادامهٔ پایش، تنظیمات را دوباره بررسی و ذخیره کنید.", true);
                }
                else
                {
                    Feedback("Settings could not be saved. Check the NinjaTrader path and your Windows access.",
                        "ذخیره انجام نشد؛ مسیر نینجاتریدر و دسترسی کاربر ویندوز را بررسی کنید.", true);
                }

                return false;
            }
        }
        private bool ConfirmLive()
        {
            LiveConfirmation required = LiveConfirmationPolicy.Required(owner.SavedTradingMode, owner.SavedLaunchMode, selectedTradingMode, selectedMode);
            if (required == LiveConfirmation.None)
            {
                return true;
            }

            using (var dialog = new ConfirmationDialog(language, required))
            {
                return dialog.ShowDialog(this) == DialogResult.Yes;
            }
        }
        private void DeleteCredentials()
        {
            if (owner.Running)
            {
                return;
            }

            HidePassword();
            if (!Agrees(ConfirmationContent.RemoveLogin(language)))
            {
                return;
            }

            try
            {
                credentials.Delete();
                bool recordsCleared = owner.CredentialsRemoved();
                storedUsername = null;
                userInput.Field.Clear();
                passwordInput.Field.Clear();
                passwordState.LoadSaved(false);
                HidePassword();
                UpdateEye();
                SelectPage(false);
                if (recordsCleared)
                {
                    Feedback("Saved credentials removed.", "اطلاعات ورود ذخیره‌شده حذف شد.", false);
                }
                else
                {
                    // The password itself is gone; only the app's own record could not be confirmed.
                    Feedback("Saved credentials removed, but the app could not confirm that its record of an earlier refused password was cleared. It is checked again when you save new details.",
                        "اطلاعات ورود ذخیره‌شده حذف شد، ولی پاک شدنِ ثبتِ ردشدن قبلی رمز تأیید نشد. با ذخیرهٔ اطلاعات جدید دوباره بررسی می‌شود.", true);
                }
            }
            catch (Exception error) { AppFaults.Record("DeleteCredentials", error); Feedback("Saved credentials could not be removed.", "حذف اطلاعات ذخیره‌شده انجام نشد.", true); }
        }
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            InspectTarget();
            FitToWorkingArea(true);
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && !owner.Exiting && !closingUi && HasUnsavedChanges() && !Agrees(ConfirmationContent.DiscardChanges(language)))
            {
                e.Cancel = true;
                base.OnFormClosing(e);
                return;
            }

            HidePassword();
            passwordInput.Field.Clear();
            closingUi = true;
            base.OnFormClosing(e);
            if (e.Cancel)
            {
                closingUi = false;
            }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && cleanup.TryBegin())
            {
                closingUi = true;
                if (passwordInput != null && !passwordInput.Field.IsDisposed)
                {
                    passwordInput.Field.Clear();
                }

                if (passwordHelp != null)
                {
                    passwordHelp.Dispose();
                }
                if (aboutView != null)
                {
                    aboutView.Dispose();
                }
                tips.Dispose();
                optionsMenu.Dispose();
                if (mark != null && mark.Image != null)
                {
                    Image image = mark.Image;
                    mark.Image = null;
                    image.Dispose();
                }
                if (Icon != null)
                {
                    Icon icon = Icon;
                    Icon = null;
                    icon.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
