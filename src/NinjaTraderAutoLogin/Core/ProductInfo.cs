using System;

namespace NinjaTraderAutoLogin
{
    internal static class ProductInfo
    {
        public const string ContactEmail = "zehtabex@gmail.com";
        public const string GitHubProfileUrl = "https://github.com/MohZeh";
        public const string RepositoryOwner = "MohZeh";
        public const string RepositoryName = "Auto-Login-for-NinjaTrader";
        public const string SourceRepositoryUrl = "https://github.com/MohZeh/Auto-Login-for-NinjaTrader";
        public const string EnglishName = "Auto Login for NinjaTrader";
        public static string Name(string language)
        {
            return UiText.Choose(language, EnglishName, "ورود خودکار برای نینجاتریدر");
        }
        public static Uri ReleaseApi
        {
            get
            {
                return new Uri("https://api.github.com/repos/" + RepositoryOwner + "/" + RepositoryName + "/releases/latest");
            }
        }
        public static bool IsReleasePage(Uri uri)
        {
            return uri != null && uri.Scheme == Uri.UriSchemeHttps && uri.Host == "github.com" &&
                uri.IsDefaultPort && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0 &&
                uri.AbsolutePath.StartsWith("/" + RepositoryOwner + "/" + RepositoryName + "/releases/tag/", StringComparison.Ordinal);
        }
    }
}
