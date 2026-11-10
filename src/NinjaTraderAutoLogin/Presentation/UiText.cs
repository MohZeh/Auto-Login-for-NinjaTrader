namespace NinjaTraderAutoLogin
{
    internal static class UiText
    {
        public static string PasswordHint(string language, bool saved, bool draft)
        {
            if (draft)
            {
                return Choose(language, "Save to use this new password.", "برای استفاده از رمز جدید، تنظیمات را ذخیره کنید.");
            }
            return saved ? Choose(language, "Saved password is hidden. Enter a new one to replace it.", "رمز ذخیره‌شده قابل نمایش نیست؛ برای تغییر، رمز جدید وارد کنید.")
                : Choose(language, "Enter and save your password to enable automatic login.", "برای ورود خودکار، رمز را وارد و ذخیره کنید.");
        }
        public static string ProgressTitle(string language)
        {
            return Choose(language, "NinjaTrader sign-in", "ورود به نینجاتریدر");
        }
        public static string ProgressStage(string language, string stage, TradingMode mode)
        {
            if (stage == Stage.WaitingForLogin.ToString())
            {
                return Choose(language, "Waiting for the sign-in window…", "در انتظار صفحهٔ ورود…");
            }

            if (stage == Stage.WaitingForTradingMode.ToString())
            {
                return Choose(language, "Signing in…", "در حال ورود…");
            }

            if (stage == Stage.WaitingForControlCenter.ToString())
            {
                return Choose(language, "Opening NinjaTrader…", "در حال بازشدن نینجاتریدر…");
            }

            return Choose(language, "Starting…", "در حال شروع…");
        }
        public static string TargetStatus(string language, TargetInspection target)
        {
            if (!target.Exists)
            {
                return Choose(language, "✕ This file was not found.", "✕ این فایل پیدا نشد.");
            }

            if (!target.Trusted)
            {
                return Choose(language, "✕ Not signed by NinjaTrader, so sign-in will stop.", "✕ امضای نینجاتریدر را ندارد؛ ورود انجام نمی‌شود.");
            }

            if (target.Status == NinjaTraderVersionStatus.Tested)
            {
                return Choose(language, "✓ Version " + target.Version + " · tested · signed by NinjaTrader", "✓ نسخهٔ " + target.Version + " · آزموده‌شده · با امضای نینجاتریدر");
            }

            return target.Status == NinjaTraderVersionStatus.Unknown
                ? Choose(language, "⚠ Version " + target.Version + " has not been tested with this app.", "⚠ نسخهٔ " + target.Version + " با این برنامه آزموده نشده است.")
                : Choose(language, "✕ This file's version could not be read.", "✕ نسخهٔ این فایل خوانده نشد.");
        }
        public static string RejectedHint(string language, bool uncertain)
        {
            return uncertain
                ? Choose(language, "Sign-in wasn't accepted. It may be the password or the connection.", "ورود پذیرفته نشد؛ ممکن است از رمز یا از اتصال باشد.")
                : Choose(language, "NinjaTrader rejected this username or password. It won't be resent.", "نینجاتریدر این نام کاربری یا رمز را نپذیرفت؛ ارسال نمی‌شود.");
        }
        public static string LegalNotice(string language)
        {
            return Choose(language,
                "Unofficial, independent software. Not affiliated with, endorsed by or supported by NinjaTrader, LLC or its affiliates. NinjaTrader is a trademark of its owner. You are responsible for complying with NinjaTrader's license and any broker or prop-firm rules. Provided as is, without warranty, under the MIT license.",
                "این نرم‌افزار غیررسمی و مستقل است و هیچ وابستگی، تأییدیه یا پشتیبانی از سوی NinjaTrader, LLC یا شرکت‌های وابسته به آن ندارد. NinjaTrader علامت تجاری مالک آن است. مسئولیت رعایت قوانین مجوز نینجاتریدر و مقررات کارگزار یا شرکت‌های پراپ‌فرم بر عهده شماست. این نرم‌افزار «همان‌گونه که هست»، بدون هیچ ضمانتی و تحت مجوز MIT ارائه شده است.");
        }
        public static string UnknownVersionNotice(string language, string version)
        {
            return Choose(language, "NinjaTrader " + version + " has not been tested with this app.", "نینجاتریدر " + version + " با این برنامه آزموده نشده است.");
        }
        public static string ShortcutAction(string language, ShortcutState state)
        {
            return state == ShortcutState.Ready ? Choose(language, "✓ Shortcut ready", "✓ شورتکات آماده است")
                : state == ShortcutState.Missing ? Choose(language, "Create shortcut", "ساخت شورتکات")
                : Choose(language, "Update shortcut", "به‌روزرسانی شورتکات");
        }
        public static string Notice(SettingsNotice notice, string language, TradingMode savedMode)
        {
            switch (notice)
            {
                case SettingsNotice.Unsaved:
                    return Choose(language, "Unsaved changes. Saved environment: ", "تغییرات ذخیره نشده است؛ محیط ذخیره‌شده: ") + ModeName(savedMode, language);
                case SettingsNotice.MissingCredentials:
                    return Choose(language, "Not ready. Enter and save your username and password.", "آماده نیست؛ نام کاربری و رمز را وارد و ذخیره کنید.");
                case SettingsNotice.MissingExecutable:
                    return Choose(language, "Not ready. The saved NinjaTrader file was not found.", "آماده نیست؛ فایل ذخیره‌شدهٔ نینجاتریدر پیدا نشد.");
                default:
                    return null;
            }
        }
        public static string Choose(string language, string english, string persian)
        {
            return language == "en" ? english : persian;
        }
        public static string ModeName(TradingMode mode, string language)
        {
            return mode == TradingMode.Live ? Choose(language, "Live trading", "لایو ترید") : Choose(language, "Simulation", "سیمولیشن");
        }
        public static string Status(string key, string language, TradingMode mode = TradingMode.Simulation)
        {
            switch (key)
            {
                case "invalid":
                    return Choose(language, "Settings need attention. Review and save them below.", "تنظیمات نیاز به بررسی دارد؛ آن را در همین پنجره ذخیره کنید.");
                case "startupError":
                    return Choose(language, "Windows startup needs attention. Review the launch method and save again.", "اجرای خودکار با ویندوز نیاز به بررسی دارد؛ روش شروع را بررسی و دوباره ذخیره کنید.");
                case "paused":
                    return Choose(language, "Watching paused. You can resume it here or from the tray.", "پایش متوقف است؛ از همین پنجره یا آیکون کنار ساعت ادامه دهید.");
                case "watching":
                    return Choose(language, "Watching is active. Start NinjaTrader normally.", "پایش فعال است؛ نینجاتریدر را از مسیر معمول اجرا کنید.");
                case "watchError":
                    return Choose(language, "Watching paused. Check NinjaTrader's path and access level.", "پایش متوقف شد؛ مسیر و سطح دسترسی نینجاتریدر را بررسی کنید.");
                case "watchMultiple":
                    return Choose(language, "Watching paused. More than one NinjaTrader is running; close the extra copy and resume.", "پایش متوقف شد؛ بیش از یک نینجاتریدر در حال اجراست. نسخهٔ اضافه را ببندید و پایش را ادامه دهید.");
                case "savedWatch":
                    return Choose(language, "Settings saved. Watching is active in the tray.", "تنظیمات ذخیره شد؛ پایش با آیکون کنار ساعت فعال است.");
                case "savedRefusalKept":
                    return Choose(language, "Settings saved, but the record of the earlier refused password could not be cleared, so automatic sign-in stays on hold. Save again to retry.",
                        "تنظیمات ذخیره شد، ولی ثبتِ ردشدن قبلی رمز پاک نشد، پس ورود خودکار متوقف می‌ماند. برای تلاش دوباره، دوباره ذخیره کنید.");
                case "savedShortcut":
                    return Choose(language, "Settings saved. Environment: ", "تنظیمات ذخیره شد؛ محیط معاملاتی: ") + ModeName(mode, language);
                case "loggingIn":
                    return Choose(language, "Checking NinjaTrader / signing in — ", "بررسی وضعیت نینجاتریدر / ورود — ") + ModeName(mode, language);
                case "cancelling":
                    return Choose(language, "Cancelling the current login…", "در حال لغو ورود جاری…");
                default:
                    return Choose(language, "Ready. Environment: ", "آماده؛ محیط معاملاتی: ") + ModeName(mode, language);
            }
        }
    }

    internal sealed class ConfirmationContent
    {
        public string Title, Lead, WatchNote, Untested, Confirm, Cancel;
        public bool Live = true;

        public static ConfirmationContent DiscardChanges(string language)
        {
            return new ConfirmationContent
            {
                Live = false,
                Title = UiText.Choose(language, "Discard your changes?", "تغییرات دور ریخته شوند؟"),
                Lead = UiText.Choose(language, "Your changes, including any new password, have not been saved.", "تغییرات شما، از جمله رمز تازه، ذخیره نشده‌اند."),
                Confirm = UiText.Choose(language, "Discard", "دور ریختن"),
                Cancel = UiText.Choose(language, "Keep editing", "ادامهٔ ویرایش")
            };
        }
        public static ConfirmationContent WatchWhileOpen(string language)
        {
            return new ConfirmationContent
            {
                Live = false,
                Title = UiText.Choose(language, "Start watching now?", "پایش همین حالا شروع شود؟"),
                Lead = UiText.Choose(language, "NinjaTrader is open. If its sign-in window is showing, the app will sign in as soon as you save.",
                    "نینجاتریدر باز است. اگر پنجرهٔ ورود آن باز باشد، برنامه با ذخیره‌کردن همان لحظه وارد می‌شود."),
                Confirm = UiText.Choose(language, "Save and watch", "ذخیره و پایش"),
                Cancel = UiText.Choose(language, "Cancel", "انصراف")
            };
        }
        public static ConfirmationContent RemoveLogin(string language)
        {
            return new ConfirmationContent
            {
                Live = false,
                Title = UiText.Choose(language, "Remove the saved login?", "اطلاعات ورود حذف شود؟"),
                Lead = UiText.Choose(language, "The saved username and password will be deleted. Automatic sign-in will need them again.",
                    "نام کاربری و رمز ذخیره‌شده حذف می‌شوند و برای ورود خودکار باید دوباره وارد شوند."),
                Confirm = UiText.Choose(language, "Remove", "حذف"),
                Cancel = UiText.Choose(language, "Cancel", "انصراف")
            };
        }
        public static ConfirmationContent RetryRejected(string language, bool uncertain = false)
        {
            return new ConfirmationContent
            {
                Live = false,
                Title = UiText.Choose(language, "Try the same password again?", "دوباره با همین رمز امتحان شود؟"),
                Lead = uncertain
                    ? UiText.Choose(language, "The last sign-in wasn't accepted. It may be the password or the connection. Repeated attempts may lock your account.",
                        "ورود قبلی پذیرفته نشد؛ ممکن است از رمز یا از اتصال باشد. تلاش‌های مکرر ممکن است حساب شما را قفل کند.")
                    : UiText.Choose(language, "NinjaTrader rejected this username or password last time. Repeated attempts may lock your account.",
                        "نینجاتریدر دفعهٔ قبل این نام کاربری یا رمز را نپذیرفت. تلاش‌های مکرر ممکن است حساب شما را قفل کند."),
                Confirm = UiText.Choose(language, "Try again", "تلاش دوباره"),
                Cancel = UiText.Choose(language, "Cancel", "انصراف")
            };
        }
        public static ConfirmationContent For(string language, LiveConfirmation kind, string version = null)
        {
            if (kind == LiveConfirmation.UnknownVersion)
            {
                return new ConfirmationContent
                {
                    Title = UiText.Choose(language, "Use Live with an untested version?", "ورود به لایو با نسخهٔ آزموده‌نشده؟"),
                    Lead = UiText.Choose(language, "NinjaTrader " + version + " has not been tested with this app, and this sign-in goes to your real account. You will not be asked again for this version.",
                        "نینجاتریدر " + version + " با این برنامه آزموده نشده است و این ورود به حساب واقعی شماست. برای این نسخه دوباره پرسیده نمی‌شود."),
                    Untested = UiText.Choose(language, "Live sign-in has not yet been tested with a real account.", "ورود به لایو هنوز با حساب واقعی آزموده نشده است."),
                    Confirm = UiText.Choose(language, "Continue with Live", "ادامه با لایو"),
                    Cancel = UiText.Choose(language, "Cancel", "انصراف")
                };
            }

            bool watchOnly = kind == LiveConfirmation.WatchWhileLive;
            return new ConfirmationContent
            {
                Title = watchOnly ? UiText.Choose(language, "Turn on watching with Live?", "پایش همراه با لایو فعال شود؟")
                    : UiText.Choose(language, "Sign in to your real account?", "ورود به حساب واقعی انجام شود؟"),
                Lead = watchOnly ? UiText.Choose(language, "Every time NinjaTrader opens, it will sign in to your Live account without asking.",
                        "هر بار نینجاتریدر باز شود، بدون پرسش وارد حساب لایو شما می‌شود.")
                    : UiText.Choose(language, "NinjaTrader will sign in to your Live account instead of Simulation.",
                        "نینجاتریدر به‌جای سیمولیشن وارد حساب لایو شما می‌شود."),
                WatchNote = kind != LiveConfirmation.EnterLiveWithWatch ? null : UiText.Choose(language, "Watch NinjaTrader is selected, so this happens without asking every time NinjaTrader opens.",
                    "پایش انتخاب شده است؛ پس هر بار نینجاتریدر باز شود، این ورود بدون پرسش انجام می‌شود."),
                // Remove this sentence after Native Live acceptance; the confirmation itself stays.
                Untested = UiText.Choose(language, "Live sign-in has not yet been tested with a real account.", "ورود به لایو هنوز با حساب واقعی آزموده نشده است."),
                Confirm = watchOnly ? UiText.Choose(language, "Turn on watching", "فعال‌کردن پایش") : UiText.Choose(language, "Save with Live", "ذخیره با لایو"),
                Cancel = UiText.Choose(language, "Cancel", "انصراف")
            };
        }
    }

    internal static class Messages
    {
        public static string For(Outcome outcome, string language, TradingMode mode = TradingMode.Simulation)
        {
            switch (outcome)
            {
                case Outcome.Success:
                    return UiText.ModeName(mode, language) + UiText.Choose(language, " selected. NinjaTrader is open.", " انتخاب شد؛ نینجاتریدر باز است.");
                case Outcome.AlreadyOpen:
                    return UiText.Choose(language, "NinjaTrader is already open. No new login was performed.", "نینجاتریدر از قبل باز است؛ ورود تازه‌ای انجام نشد.");
                case Outcome.AuthenticationFailed:
                    return UiText.Choose(language, "Sign-in was rejected. Review your credentials; there is no automatic retry.", "ورود پذیرفته نشد؛ اطلاعات ورود را بررسی کنید. تلاش مجدد خودکار انجام نمی‌شود.");
                case Outcome.NeedsManual:
                    return UiText.Choose(language, "NinjaTrader needs your attention. Continue manually in its window.", "نینجاتریدر به تأیید شما نیاز دارد؛ ادامه را در پنجرهٔ نینجاتریدر انجام دهید.");
                case Outcome.UnsupportedUi:
                    return UiText.Choose(language, "A NinjaTrader control was not recognized. A diagnostic report was saved.", "یکی از کنترل‌های نینجاتریدر شناسایی نشد؛ گزارش تشخیصی ثبت شده است.");
                case Outcome.TimedOut:
                    return UiText.Choose(language, "Waiting timed out. Check NinjaTrader's window.", "مهلت انتظار تمام شد؛ پنجرهٔ نینجاتریدر را بررسی کنید.");
                case Outcome.Cancelled:
                    return UiText.Choose(language, "Automatic login cancelled.", "ورود خودکار لغو شد.");
                case Outcome.ProcessExited:
                    return UiText.Choose(language, "NinjaTrader closed or could not be found at the selected path.", "نینجاتریدر بسته شد یا در مسیر انتخاب‌شده در دسترس نیست.");
                case Outcome.CredentialsMissing:
                    return UiText.Choose(language, "Save your username and password to enable automatic login.", "برای ورود خودکار، نام کاربری و رمز را ذخیره کنید.");
                case Outcome.ModeNotConfirmed:
                    return UiText.Choose(language, "NinjaTrader opened; the selected trading mode was not confirmed.", "نینجاتریدر باز شد؛ انتخاب محیط معاملاتی تأیید نشد.");
                case Outcome.EvidenceUnavailable:
                    return UiText.Choose(language, "The app could not save its diagnostic report.", "برنامه نتوانست گزارش تشخیصی را ذخیره کند.");
                case Outcome.MultipleInstances:
                    return UiText.Choose(language, "More than one NinjaTrader is running. Close the extra copy and try again.", "بیش از یک نینجاتریدر در حال اجراست؛ نسخهٔ اضافه را ببندید و دوباره تلاش کنید.");
                case Outcome.CredentialsRejected:
                    return UiText.Choose(language, "The last sign-in wasn't accepted, so the password was not sent again. Enter it again here.",
                        "ورود قبلی پذیرفته نشد، پس رمز دوباره ارسال نشد. رمز را اینجا دوباره وارد کنید.");
                case Outcome.RefusalUnavailable:
                    return UiText.Choose(language, "The record of an earlier refused password could not be read, so the password was not sent. Check that Windows can read your user's registry, then try again.",
                        "ثبتِ ردشدن قبلی رمز خوانده نشد، پس رمز ارسال نشد. مطمئن شوید ویندوز به رجیستری کاربر شما دسترسی دارد و دوباره امتحان کنید.");
                case Outcome.RefusalNotCleared:
                    return UiText.Choose(language, "Your choice to try again could not be saved, so the password was not sent. Try again in a moment.",
                        "انتخاب «تلاش دوباره» ذخیره نشد، پس رمز ارسال نشد. کمی بعد دوباره امتحان کنید.");
                case Outcome.UntrustedTarget:
                    return UiText.Choose(language, "This file is not signed by NinjaTrader, so your password was not used. Check the NinjaTrader path in Settings.",
                        "این فایل امضای نینجاتریدر را ندارد و رمز شما استفاده نشد. مسیر نینجاتریدر را در تنظیمات بررسی کنید.");
                case Outcome.LiveVersionNotConfirmed:
                    return UiText.Choose(language, "Live sign-in stopped because this NinjaTrader version was not confirmed.",
                        "ورود به لایو متوقف شد، چون این نسخهٔ نینجاتریدر تأیید نشد.");
                case Outcome.LoginNotAccepted:
                    return UiText.Choose(language, "NinjaTrader returned to its sign-in window, so it did not accept this sign-in. There is no automatic retry; check its window.",
                        "نینجاتریدر به صفحهٔ ورود برگشت و این ورود را نپذیرفت. تلاش مجدد خودکار انجام نمی‌شود؛ پنجرهٔ نینجاتریدر را بررسی کنید.");
                case Outcome.LiveAccountUnavailable:
                    return UiText.Choose(language, "This login has no active Live account (Live offers Open Account). Nothing was selected; continue in NinjaTrader's window.",
                        "این ورود حساب لایو فعال ندارد (گزینهٔ لایو «Open Account» است). چیزی انتخاب نشد؛ ادامه را در پنجرهٔ نینجاتریدر انجام دهید.");
                case Outcome.AccessDenied:
                    return UiText.Choose(language, "Run the app and NinjaTrader as the same Windows user and access level.", "برنامه و نینجاتریدر را با همان کاربر و سطح دسترسی ویندوز اجرا کنید.");
                default:
                    return UiText.Choose(language, "The operation could not finish. A diagnostic report was saved.", "عملیات کامل نشد؛ گزارش تشخیصی برای بررسی نگه داشته شد.");
            }
        }
    }
}
