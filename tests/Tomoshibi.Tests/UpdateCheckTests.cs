using Tomoshibi.Services;
using Xunit;

namespace Tomoshibi.Tests;

/// <summary>The update check's pure halves: version comparison (GitHub tags
/// arrive with and without the v) and pulling the tag out of the API
/// payload. The network half deliberately reads any failure as "no update",
/// so everything here errs the same way.</summary>
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
}
