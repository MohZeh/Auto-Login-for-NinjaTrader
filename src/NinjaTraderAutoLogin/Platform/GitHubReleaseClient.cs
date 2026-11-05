using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NinjaTraderAutoLogin
{
    internal enum UpdateStatus
    {
        NewerAvailable, Current, NoPublicRelease, Unavailable, RateLimited, InvalidResponse
    }
    internal sealed class UpdateResult
    {
        public UpdateStatus Status;
        public string Version;
        public Uri ReleasePage;
    }
    [DataContract]
    internal sealed class GitHubRelease
    {
        [DataMember(Name = "tag_name")] public string Tag;
        [DataMember(Name = "html_url")] public string Page;
        [DataMember(Name = "draft")] public bool Draft;
        [DataMember(Name = "prerelease")] public bool Prerelease;
    }
    internal sealed class GitHubReleaseClient : IDisposable
    {
        private const int MaximumResponseBytes = 65536;
        private readonly HttpClient client;
        public GitHubReleaseClient() : this(new HttpClientHandler { AllowAutoRedirect = false, UseDefaultCredentials = false }) { }
        internal GitHubReleaseClient(HttpMessageHandler handler)
        {
            client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("NinjaTraderAutoLogin", BuildInfo.Version));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        }
        public async Task<UpdateResult> CheckAsync(string currentVersion, CancellationToken cancellation)
        {
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                deadline.CancelAfter(TimeSpan.FromSeconds(10));
                try
                {
                    using (HttpResponseMessage response = await client.GetAsync(ProductInfo.ReleaseApi, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false))
                    {
                        if (response.StatusCode == HttpStatusCode.NotFound)
                        {
                            return Result(UpdateStatus.NoPublicRelease);
                        }
                        if (response.StatusCode == HttpStatusCode.Forbidden || (int)response.StatusCode == 429)
                        {
                            return Result(UpdateStatus.RateLimited);
                        }
                        if (response.StatusCode != HttpStatusCode.OK)
                        {
                            return Result(UpdateStatus.Unavailable);
                        }
                        if (response.Content == null || response.Content.Headers.ContentLength > MaximumResponseBytes)
                        {
                            return Result(UpdateStatus.InvalidResponse);
                        }
                        using (Stream input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        using (var buffer = new MemoryStream())
                        {
                            var bytes = new byte[4096];
                            int count;
                            while ((count = await input.ReadAsync(bytes, 0, bytes.Length, deadline.Token).ConfigureAwait(false)) > 0)
                            {
                                if (buffer.Length + count > MaximumResponseBytes)
                                {
                                    return Result(UpdateStatus.InvalidResponse);
                                }
                                buffer.Write(bytes, 0, count);
                            }
                            buffer.Position = 0;
                            var release = (GitHubRelease)new DataContractJsonSerializer(typeof(GitHubRelease)).ReadObject(buffer);
                            return Evaluate(release, currentVersion);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    cancellation.ThrowIfCancellationRequested();
                    return Result(UpdateStatus.Unavailable);
                }
                catch (HttpRequestException) { return Result(UpdateStatus.Unavailable); }
                catch (IOException) { return Result(UpdateStatus.Unavailable); }
                catch (SerializationException) { return Result(UpdateStatus.InvalidResponse); }
                catch (System.Xml.XmlException) { return Result(UpdateStatus.InvalidResponse); }
            }
        }
        internal static UpdateResult Evaluate(GitHubRelease release, string currentVersion)
        {
            ReleaseVersion current, remote;
            Uri page;
            if (release == null || release.Draft || release.Prerelease || !ReleaseVersion.TryParse(currentVersion, out current) ||
                !ReleaseVersion.TryParse(release.Tag, out remote) || !Uri.TryCreate(release.Page, UriKind.Absolute, out page) || !ProductInfo.IsReleasePage(page))
            {
                return Result(UpdateStatus.InvalidResponse);
            }
            return new UpdateResult { Status = remote.CompareTo(current) > 0 ? UpdateStatus.NewerAvailable : UpdateStatus.Current, Version = release.Tag, ReleasePage = page };
        }
        private static UpdateResult Result(UpdateStatus status)
        {
            return new UpdateResult { Status = status };
        }
        public void Dispose()
        {
            client.Dispose();
        }
    }
}
