using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Synthea.Cli;

internal sealed record GitHubReachabilityResult(
    bool Reachable,
    int? StatusCode,
    long? ElapsedMilliseconds,
    string? ErrorMessage);

internal interface IGitHubReachabilityProbe
{
    Task<GitHubReachabilityResult> PingAsync(TimeSpan timeout, CancellationToken cancelToken);
}

// Real probe: HEAD against the synthea releases endpoint we rely on. A
// failed ping is reported as Warn (the user can still use --jar to bypass
// GitHub entirely), so network blips don't turn `synthea doctor` red.
internal sealed class HttpGitHubReachabilityProbe : IGitHubReachabilityProbe
{
    private const string ProbeUrl = "https://api.github.com/repos/synthetichealth/synthea";

    private readonly HttpClient _http;

    public HttpGitHubReachabilityProbe(HttpClient http) => _http = http;

    public async Task<GitHubReachabilityResult> PingAsync(TimeSpan timeout, CancellationToken cancelToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);
        cts.CancelAfter(timeout);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Head, ProbeUrl);
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            sw.Stop();
            return new GitHubReachabilityResult(
                Reachable: resp.IsSuccessStatusCode,
                StatusCode: (int)resp.StatusCode,
                ElapsedMilliseconds: sw.ElapsedMilliseconds,
                ErrorMessage: null);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancelToken.IsCancellationRequested)
        {
            sw.Stop();
            return new GitHubReachabilityResult(false, null, sw.ElapsedMilliseconds, $"timed out after {timeout.TotalSeconds:0.0}s");
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            return new GitHubReachabilityResult(false, null, sw.ElapsedMilliseconds, ex.Message);
        }
    }
}
