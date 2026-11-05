using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NinjaTraderAutoLogin;

internal sealed class FakeReleaseHandler : HttpMessageHandler
{
    public HttpStatusCode Status = HttpStatusCode.OK;
    public string Content;
    public bool Unreachable, Timeout;
    public HttpRequestMessage Request;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Request = request;
        cancellationToken.ThrowIfCancellationRequested();
        if (Unreachable)
        {
            throw new HttpRequestException("SimulatedNetworkFailure");
        }
        if (Timeout)
        {
            throw new TaskCanceledException("SimulatedTimeout");
        }
        return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(Content ?? String.Empty) });
    }
}

internal static partial class TestRunner
{
    private static ReleaseVersion ParseVersion(string text)
    {
        ReleaseVersion value;
        Require(ReleaseVersion.TryParse(text, out value));
        return value;
    }
    private static string ReleaseJson(string tag)
    {
        return "{\"tag_name\":\"" + tag + "\",\"html_url\":\"https://github.com/" + ProductInfo.RepositoryOwner + "/" + ProductInfo.RepositoryName + "/releases/tag/" + tag + "\",\"draft\":false,\"prerelease\":false}";
    }
    private static UpdateResult CheckRelease(FakeReleaseHandler handler, string current = "0.1.0-candidate.14")
    {
        using (var client = new GitHubReleaseClient(handler))
        {
            return client.CheckAsync(current, CancellationToken.None).GetAwaiter().GetResult();
        }
    }
    private static void RunReleaseChecks()
    {
        Test("Release versions use numeric precedence rather than lexical order", delegate
        {
            Require(ParseVersion("1.10.0").CompareTo(ParseVersion("1.9.99")) > 0);
            Require(ParseVersion("0.1.0-candidate.14").CompareTo(ParseVersion("0.1.0-candidate.9")) > 0);
            Require(ParseVersion("v2.0.0").CompareTo(ParseVersion("1.999.999")) > 0);
        });
        Test("SemVer prerelease precedence follows the published ordering", delegate
        {
            string[] versions = { "1.0.0-alpha", "1.0.0-alpha.1", "1.0.0-alpha.beta", "1.0.0-beta", "1.0.0-beta.2", "1.0.0-beta.11", "1.0.0-rc.1", "1.0.0" };
            for (int i = 1; i < versions.Length; i++)
            {
                Require(ParseVersion(versions[i]).CompareTo(ParseVersion(versions[i - 1])) > 0);
            }
            Require(ParseVersion("1.0.0+build.1").CompareTo(ParseVersion("1.0.0+build.9")) == 0);
        });
        Test("Version parsing rejects malformed or ambiguous release tags", delegate
        {
            foreach (string text in new[] { "", "1.0", "01.0.0", "1.0.0-01", "1.0.0-beta..1", "1.0.0 ", "1.0.0\n", "<script>", new String('1', 129) })
            {
                ReleaseVersion value;
                Require(!ReleaseVersion.TryParse(text, out value));
            }
            Require(ParseVersion("99999999999999999999.0.0").CompareTo(ParseVersion("9999999999999999999.0.0")) > 0);
        });
        Test("A newer public release exposes its version and approved release page", delegate
        {
            UpdateResult result = CheckRelease(new FakeReleaseHandler { Content = ReleaseJson("v0.1.0") });
            Require(result.Status == UpdateStatus.NewerAvailable && result.Version == "v0.1.0" && ProductInfo.IsReleasePage(result.ReleasePage));
        });
        Test("Equal and older releases do not report a false update", delegate
        {
            Require(CheckRelease(new FakeReleaseHandler { Content = ReleaseJson("1.0.0") }, "1.0.0").Status == UpdateStatus.Current);
            Require(CheckRelease(new FakeReleaseHandler { Content = ReleaseJson("1.0.0") }, "1.1.0").Status == UpdateStatus.Current);
        });
        Test("Missing public release is distinct from a current-version result", delegate
        {
            Require(CheckRelease(new FakeReleaseHandler { Status = HttpStatusCode.NotFound }).Status == UpdateStatus.NoPublicRelease);
        });
        Test("Denied and rate-limited requests have a recoverable result", delegate
        {
            Require(CheckRelease(new FakeReleaseHandler { Status = HttpStatusCode.Forbidden }).Status == UpdateStatus.RateLimited);
            Require(CheckRelease(new FakeReleaseHandler { Status = (HttpStatusCode)429 }).Status == UpdateStatus.RateLimited);
        });
        Test("Network failures and timeouts never become successful update checks", delegate
        {
            Require(CheckRelease(new FakeReleaseHandler { Unreachable = true }).Status == UpdateStatus.Unavailable);
            Require(CheckRelease(new FakeReleaseHandler { Timeout = true }).Status == UpdateStatus.Unavailable);
            Require(CheckRelease(new FakeReleaseHandler { Status = HttpStatusCode.Redirect }).Status == UpdateStatus.Unavailable);
        });
        Test("Malformed and oversized release responses are rejected", delegate
        {
            foreach (string body in new[] { "not json", "null", "{}", new String('x', 65537) })
            {
                Require(CheckRelease(new FakeReleaseHandler { Content = body }).Status == UpdateStatus.InvalidResponse);
            }
        });
        Test("Update results cannot open a different host or repository", delegate
        {
            foreach (string url in new[] { "http://github.com/MohZeh/Auto-Login-for-NinjaTrader/releases/tag/1.0.0", "https://github.com.evil.example/a", "https://github.com/another/repo/releases/tag/1.0.0", "file:///example", "https://user@github.com/MohZeh/Auto-Login-for-NinjaTrader/releases/tag/1.0.0", "https://github.com/MohZeh/Auto-Login-for-NinjaTrader/releases/tag/1.0.0?redirect=other" })
            {
                var release = new GitHubRelease { Tag = "1.0.0", Page = url };
                Require(GitHubReleaseClient.Evaluate(release, "0.1.0").Status == UpdateStatus.InvalidResponse);
            }
        });
        Test("Drafts and prereleases do not escape the public-release policy", delegate
        {
            string url = "https://github.com/" + ProductInfo.RepositoryOwner + "/" + ProductInfo.RepositoryName + "/releases/tag/1.0.0";
            Require(GitHubReleaseClient.Evaluate(new GitHubRelease { Tag = "1.0.0", Page = url, Draft = true }, "0.1.0").Status == UpdateStatus.InvalidResponse);
            Require(GitHubReleaseClient.Evaluate(new GitHubRelease { Tag = "1.0.0", Page = url, Prerelease = true }, "0.1.0").Status == UpdateStatus.InvalidResponse);
        });
        Test("The update request carries product metadata without account authorization", delegate
        {
            var handler = new FakeReleaseHandler { Content = ReleaseJson("1.0.0") };
            CheckRelease(handler);
            Require(handler.Request.Method == HttpMethod.Get && handler.Request.RequestUri == ProductInfo.ReleaseApi);
            Require(handler.Request.RequestUri.Query.Length == 0 && handler.Request.Headers.Authorization == null);
            Require(handler.Request.Headers.UserAgent.ToString() == "NinjaTraderAutoLogin/" + BuildInfo.Version);
        });
        Test("Closing the update request preserves cancellation rather than a false status", delegate
        {
            using (var cancellation = new CancellationTokenSource())
            using (var client = new GitHubReleaseClient(new FakeReleaseHandler()))
            {
                cancellation.Cancel();
                bool cancelled = false;
                try
                {
                    client.CheckAsync("0.1.0", cancellation.Token).GetAwaiter().GetResult();
                }
                catch (OperationCanceledException) { cancelled = true; }
                Require(cancelled);
            }
        });
        Test("Application identity and contact metadata remain independent and consistent", delegate
        {
            Require(typeof(BuildInfo).Assembly.GetName().Name == "NinjaTraderAutoLogin");
            Require(typeof(BuildInfo).Namespace == "NinjaTraderAutoLogin" && BuildInfo.CredentialTarget == "NinjaTraderAutoLogin/default");
            Require(new Uri(ProductInfo.GitHubProfileUrl).Host == "github.com" && new Uri("mailto:" + ProductInfo.ContactEmail).Scheme == "mailto");
            if (!String.IsNullOrEmpty(ProductInfo.SourceRepositoryUrl))
            {
                Uri source = new Uri(ProductInfo.SourceRepositoryUrl);
                Require(source.Scheme == Uri.UriSchemeHttps && source.Host == "github.com");
            }
        });
    }
}
