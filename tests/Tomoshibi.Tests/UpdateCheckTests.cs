using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Tomoshibi.Services;
using Xunit;

namespace Tomoshibi.Tests;

/// <summary>The update check. Version comparison (GitHub tags arrive with and
/// without the v), reading the tag out of the payload, and — the part that
/// matters — keeping "we asked and you're current" apart from "we never got to
/// ask". Those used to be the same answer.</summary>
public class UpdateCheckTests
{
    [Theory]
    [InlineData("1.9.0", "v2.0.0", true)]
    [InlineData("1.9.0", "2.0.0", true)]
    [InlineData("1.9.0", "v1.9.1", true)]
    [InlineData("1.9.0", "v1.9.0", false)]
    [InlineData("1.9.0", "v1.8.0", false)]
    [InlineData("2.0.0", "v2.0.0", false)]
    [InlineData("1.9.0", "not-a-version", false)]
    [InlineData("1.9.0", "", false)]
    [InlineData("1.9.0", null, false)]
    [InlineData("garbage", "v2.0.0", false)]
    public void Newer_means_strictly_newer_and_garbage_means_no(
        string current, string? tag, bool expected)
    {
        Assert.Equal(expected, UpdateCheck.IsNewer(current, tag));
    }

    [Fact]
    public void The_tag_comes_out_of_a_releases_payload()
    {
        const string json = """{"url": "…", "tag_name": "v2.0.0", "name": "v2.0.0"}""";

        Assert.Equal("v2.0.0", UpdateCheck.TagFrom(json));
    }

    [Theory]
    [InlineData("""{"message": "Not Found"}""")]
    [InlineData("not json")]
    [InlineData("")]
    public void Anything_unexpected_reads_as_no_tag(string payload)
    {
        Assert.Null(UpdateCheck.TagFrom(payload));
    }

    // ---- telling the four outcomes apart ----

    private const string Payload = """{"tag_name": "v2.0.0"}""";

    [Fact]
    public void A_newer_tag_is_an_update()
    {
        var r = UpdateCheck.Classify("1.9.0", HttpStatusCode.OK, Payload);

        Assert.Equal(UpdateStatus.UpdateAvailable, r.Status);
        Assert.Equal("v2.0.0", r.Tag);
    }

    [Fact]
    public void The_same_tag_is_up_to_date()
    {
        var r = UpdateCheck.Classify("2.0.0", HttpStatusCode.OK, Payload);

        Assert.Equal(UpdateStatus.UpToDate, r.Status);
    }

    [Fact]
    public void A_repo_with_no_releases_is_not_the_same_as_being_current()
    {
        // GitHub answers 404 until something is published. That's an answer.
        var r = UpdateCheck.Classify("2.0.0", HttpStatusCode.NotFound, null);

        Assert.Equal(UpdateStatus.NoReleaseYet, r.Status);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]          // rate limited
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public void An_api_that_declines_to_answer_is_not_good_news(HttpStatusCode status)
    {
        var r = UpdateCheck.Classify("2.0.0", status, null);

        Assert.Equal(UpdateStatus.Unreachable, r.Status);
    }

    [Fact]
    public void A_two_hundred_full_of_nonsense_is_still_no_answer()
    {
        var r = UpdateCheck.Classify("2.0.0", HttpStatusCode.OK, "not json");

        Assert.Equal(UpdateStatus.Unreachable, r.Status);
    }

    // ---- the request itself ----

    /// <summary>Stands in for the network so the failure paths are real rather
    /// than argued about.</summary>
    private sealed class Stub : HttpMessageHandler
    {
        private readonly Func<HttpResponseMessage> _reply;
        public Stub(Func<HttpResponseMessage> reply) => _reply = reply;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_reply());
    }

    private static Task<UpdateCheckResult> Fetch(Func<HttpResponseMessage> reply)
        => UpdateCheck.FetchAsync("1.9.0", new Stub(reply));

    [Fact]
    public async Task A_reachable_api_reports_the_release()
    {
        var r = await Fetch(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(Payload)
        });

        Assert.Equal(UpdateStatus.UpdateAvailable, r.Status);
        Assert.Equal("v2.0.0", r.Tag);
    }

    [Fact]
    public async Task A_machine_with_no_network_says_so_instead_of_saying_youre_current()
    {
        // The regression this whole type exists to prevent: this used to come
        // back indistinguishable from a successful "nothing newer".
        var r = await Fetch(() => throw new HttpRequestException("no such host"));

        Assert.Equal(UpdateStatus.Unreachable, r.Status);
        Assert.Null(r.Tag);
        Assert.NotEqual(UpdateStatus.UpToDate, r.Status);
    }

    [Fact]
    public async Task A_timeout_is_unreachable_too()
    {
        var r = await Fetch(() => throw new TaskCanceledException("timed out"));

        Assert.Equal(UpdateStatus.Unreachable, r.Status);
    }
}
