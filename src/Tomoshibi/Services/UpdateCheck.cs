using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace Tomoshibi.Services;

/// <summary>
/// The launch-time update check — the whole of tomoshibi's network story.
/// One GET to the GitHub releases API (nothing about the user rides along
/// beyond the request itself), compare version tags, and if something newer
/// exists the app says so quietly and points at the releases page.
/// Downloading and swapping binaries is deliberately out of scope until
/// builds are signed — an unsigned self-update fights Gatekeeper and
/// SmartScreen and loses. There's an off-switch in settings, and a failure
/// of any kind simply reads as "no update".
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

    /// <summary>The latest release tag, or null for any failure at all — an
    /// update check is never worth surfacing an error over.</summary>
    public static async Task<string?> FetchLatestTagAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            http.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("tomoshibi", ReleaseNotes.Version));

            return TagFrom(await http.GetStringAsync(ApiUrl));
        }
        catch
        {
            return null;
        }
    }
}
