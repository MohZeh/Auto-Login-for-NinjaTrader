using System;
using System.IO;

namespace NinjaTraderAutoLogin
{
    // Ownership is limited to this helper's dedicated Run value and exact command shape.
    // It is not a security boundary against other processes of the same Windows user.
    internal static class StartupCommand
    {
        public const string ExecutableName = "NinjaTraderAutoLogin.exe";

        public static string Create(string executable)
        {
            if (String.IsNullOrWhiteSpace(executable) || executable.IndexOfAny(new[] { '"', '\r', '\n' }) >= 0 ||
                !Path.IsPathRooted(executable) || Path.GetPathRoot(executable).Length < 3 ||
                !String.Equals(Path.GetFileName(executable), ExecutableName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("InvalidHelperExecutable");
            }
            return "\"" + Path.GetFullPath(executable) + "\" --watch";
        }

        public static bool IsOwned(string command)
        {
            if (String.IsNullOrEmpty(command) || command[0] != '"')
            {
                return false;
            }
            int closingQuote = command.IndexOf('"', 1);
            if (closingQuote < 2 || command.Substring(closingQuote) != "\" --watch")
            {
                return false;
            }
            try
            {
                return String.Equals(command, Create(command.Substring(1, closingQuote - 1)), StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException) { return false; }
            catch (NotSupportedException) { return false; }
            catch (InvalidDataException) { return false; }
            catch (IOException) { return false; }
        }

        public static void ValidateExisting(string command)
        {
            if (command != null && !IsOwned(command))
            {
                throw new InvalidDataException("StartupRegistrationConflict");
            }
        }

        public static string Next(string previous, string executable, LaunchMode mode)
        {
            ValidateExisting(previous);
            return mode == LaunchMode.Watch ? Create(executable) : null;
        }

        public static string Reconciled(string previous, string executable, LaunchMode mode, Func<string, bool> fileExists)
        {
            // A manually removed startup entry stays removed until the user saves Watch mode.
            if (previous == null)
            {
                return null;
            }

            // Another working copy keeps its entry; running a trial copy must not take it over.
            ValidateExisting(previous);
            string target = previous.Substring(1, previous.IndexOf('"', 1) - 1);
            if (!String.Equals(target, executable, StringComparison.OrdinalIgnoreCase) && fileExists(target))
            {
                return previous;
            }

            return Next(previous, executable, mode);
        }

        public static string Restored(string current, string previous)
        {
            ValidateExisting(current);
            ValidateExisting(previous);
            return previous;
        }
    }
}
