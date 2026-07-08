using System;
using System.IO;
using System.Text;
using Tomoshibi.Services;
using Xunit;

namespace Tomoshibi.Tests;

/// <summary>Media files are content-addressed so imports dedupe and names stay
/// filesystem-safe.</summary>
public class MediaStoreTests : IDisposable
{
    private readonly string _dir;

    public MediaStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "tomoshibi-media-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Import_copies_the_file_into_the_media_folder_and_returns_a_token()
    {
        var src = Path.Combine(_dir, "diagram.png");
        File.WriteAllBytes(src, Encoding.UTF8.GetBytes("fake-image-bytes"));
        var store = new MediaStore(_dir);

        var token = store.Import(src);

        Assert.EndsWith("-diagram.png", token);
        Assert.NotNull(store.Resolve(token));
    }

    [Fact]
    public void Importing_the_same_file_twice_dedupes_to_one_stored_file()
    {
        var src = Path.Combine(_dir, "same.png");
        File.WriteAllBytes(src, Encoding.UTF8.GetBytes("identical"));
        var store = new MediaStore(_dir);

        var a = store.Import(src);
        var b = store.Import(src);

        Assert.Equal(a, b);
        Assert.Single(Directory.GetFiles(store.Directory));
    }

    [Fact]
    public void Different_content_with_the_same_name_gets_distinct_tokens()
    {
        var store = new MediaStore(_dir);
        var one = store.Import(Encoding.UTF8.GetBytes("first"), "pic.png");
        var two = store.Import(Encoding.UTF8.GetBytes("second"), "pic.png");

        Assert.NotEqual(one, two);
        Assert.Equal(2, Directory.GetFiles(store.Directory).Length);
    }

    [Fact]
    public void Odd_file_names_are_sanitised()
    {
        var store = new MediaStore(_dir);
        var token = store.Import(Encoding.UTF8.GetBytes("x"), "My Photo (2)!.JPG");

        // Lowercased, spaces/punctuation collapsed to hyphens, extension kept.
        Assert.Matches("^[a-f0-9]{12}-[a-z0-9._-]+$", token);
        Assert.EndsWith(".jpg", token);
    }

    [Fact]
    public void Resolve_returns_null_for_an_unknown_token()
    {
        var store = new MediaStore(_dir);
        Assert.Null(store.Resolve("nope.png"));
    }
}
