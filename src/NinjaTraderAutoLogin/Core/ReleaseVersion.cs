using System;
using System.Text.RegularExpressions;

namespace NinjaTraderAutoLogin
{
    // SemVer precedence: numeric identifiers compare numerically; build metadata is ignored.
    internal sealed class ReleaseVersion : IComparable<ReleaseVersion>
    {
        private readonly string[] numbers, prerelease;
        private ReleaseVersion(string[] numbers, string[] prerelease)
        {
            this.numbers = numbers;
            this.prerelease = prerelease;
        }
        public static bool TryParse(string text, out ReleaseVersion version)
        {
            version = null;
            if (String.IsNullOrEmpty(text) || text.Length > 128)
            {
                return false;
            }
            Match match = Regex.Match(text, @"\Av?(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?\z", RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                return false;
            }
            string[] pre = match.Groups[4].Success ? match.Groups[4].Value.Split('.') : new string[0];
            foreach (string identifier in pre)
            {
                if (IsNumeric(identifier) && identifier.Length > 1 && identifier[0] == '0')
                {
                    return false;
                }
            }
            version = new ReleaseVersion(new[] { match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value }, pre);
            return true;
        }
        private static bool IsNumeric(string value)
        {
            foreach (char character in value)
            {
                if (character < '0' || character > '9')
                {
                    return false;
                }
            }
            return value.Length > 0;
        }
        private static int CompareNumber(string left, string right)
        {
            return left.Length != right.Length ? left.Length.CompareTo(right.Length) : String.CompareOrdinal(left, right);
        }
        public int CompareTo(ReleaseVersion other)
        {
            if (other == null)
            {
                return 1;
            }
            for (int i = 0; i < numbers.Length; i++)
            {
                int comparison = CompareNumber(numbers[i], other.numbers[i]);
                if (comparison != 0)
                {
                    return comparison;
                }
            }
            if (prerelease.Length == 0 || other.prerelease.Length == 0)
            {
                return prerelease.Length == other.prerelease.Length ? 0 : prerelease.Length == 0 ? 1 : -1;
            }
            for (int i = 0; i < Math.Min(prerelease.Length, other.prerelease.Length); i++)
            {
                string left = prerelease[i], right = other.prerelease[i];
                bool leftNumber = IsNumeric(left), rightNumber = IsNumeric(right);
                int comparison = leftNumber && rightNumber ? CompareNumber(left, right) : leftNumber != rightNumber ? (leftNumber ? -1 : 1) : String.CompareOrdinal(left, right);
                if (comparison != 0)
                {
                    return comparison;
                }
            }
            return prerelease.Length.CompareTo(other.prerelease.Length);
        }
    }
}
