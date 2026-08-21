using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace Tomoshibi.Services;

/// <summary>What the launch-time check actually learned.
///
/// <para>The distinction that matters is between the app having asked and got
/// an answer, and the app not having managed to ask. Silence is not the same
/// as good news, and telling someone their software is current when the
/// machine was offline is telling them something untrue about their own
/// install.</para></summary>
public enum UpdateStatus
{
    /// <summary>GitHub answered, and this build is the newest there is.</summary>
    UpToDate,

    /// <summary>GitHub answered, and something newer exists.</summary>
    UpdateAvailable,

    /// <summary>GitHub answered, and the project has published no releases at
    /// all. Nothing to offer, but for a different reason than being current —
    /// this is what every check returned before v2.1.3 was tagged.</summary>
    NoReleaseYet,

    /// <summary>The question never got answered: offline, timed out, GitHub
    /// unreachable or rate-limiting, or a body we couldn't read. Says nothing
    /// either way about whether an update exists.</summary>
    Unreachable,
}

/// <summary>The outcome, plus the tag when there was one to read.</summary>
public readonly record struct UpdateCheckResult(UpdateStatus Status, string? Tag = null);

/// <summary>
/// The launch-time update check — the whole of tomoshibi's network story.
/// One GET to the GitHub releases API (nothing about the user rides along
/// beyond the request itself), compare version tags, and if something newer
/// exists the app says so quietly and points at the releases page.
/// Downloading and swapping binaries is deliberately out of scope until
/// builds are signed — an unsigned self-update fights Gatekeeper and
/// SmartScreen and loses. There's an off-switch in settings.
/// </summary>
public static class UpdateCheck
{
    public const string ReleasesUrl = "https://github.com/socom1/Tomoshibi/releases/latest";
    private const string ApiUrl = "https://api.github.com/repos/socom1/Tomoshibi/releases/latest";

    /// <summary>Is <paramref name="tag"/> ("v2.1.0" or "2.1.0") newer than
    /// <paramref name="current"/>? Unparseable input reads as "no".</summary>
    public static bool IsNewer(string current, string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag) || !Version.TryParse(current, out var mine))
            return false;

        return Version.TryParse(tag.TrimStart('v', 'V'), out var theirs) && theirs > mine;
    }

    /// <summary>Pull "tag_name" out of a releases-API payload — null for
    /// anything that isn't the JSON shape GitHub sends.</summary>
    public static string? TagFrom(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("tag_name", out var tag)
                ? tag.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Turn a response into an outcome. Split out from the request so
    /// the decisions are testable without a network or a clock — the request
    /// itself has nothing left in it worth asserting.</summary>
    public static UpdateCheckResult Classify(string current, HttpStatusCode status, string? body)
    {
        // GitHub answers 404 for a repo that exists but has never published a
        // release. That's a real answer, not a failure.
        if (status == HttpStatusCode.NotFound)
            return new(UpdateStatus.NoReleaseYet);

        // Anything else non-2xx is the API declining to tell us — rate limits
        // land here, and a rate limit is emphatically not "you're up to date".
        if ((int)status is < 200 or > 299)
            return new(UpdateStatus.Unreachable);

        var tag = body is null ? null : TagFrom(body);
        if (tag is null)
            return new(UpdateStatus.Unreachable);

        return IsNewer(current, tag)
            ? new(UpdateStatus.UpdateAvailable, tag)
            : new(UpdateStatus.UpToDate, tag);
    }

    /// <summary>Ask GitHub what the latest release is.
    ///
    /// <para><paramref name="handler"/> exists for the tests; leave it null and
    /// this builds its own client.</para></summary>
    public static async Task<UpdateCheckResult> FetchAsync(
        string current, HttpMessageHandler? handler = null)
    {
        try
        {
            using var http = handler is null
                ? new HttpClient()
                : new HttpClient(handler, disposeHandler: false);

            http.Timeout = TimeSpan.FromSeconds(5);
            http.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("tomoshibi", ReleaseNotes.Version));

            using var response = await http.GetAsync(ApiUrl);
            var body = response.IsSuccessStatusCode
                ? await response.Content.ReadAsStringAsync()
                : null;

            return Classify(current, response.StatusCode, body);
        }
        catch (Exception e) when (e is HttpRequestException
                                       or TaskCanceledException
                                       or OperationCanceledException
                                       or UriFormatException)
        {
            // No network, DNS failure, or the five-second timeout ran out.
            return new(UpdateStatus.Unreachable);
        }
    }
}
