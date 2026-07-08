using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Tomoshibi.Services;

/// <summary>
/// Owns the <c>media/</c> folder beside the state file, where card images,
/// audio and video live. Imported files are renamed to a content hash prefix +
/// a sanitised original name, so the same file imported twice dedupes to one
/// token and the names are always filesystem-safe. Fields reference media by
/// this token (e.g. <c>[img:a1b2c3d4e5f6-diagram.png]</c>).
/// </summary>
public class MediaStore
{
    private readonly string _dir;

    public MediaStore(string appDataDir)
    {
        _dir = Path.Combine(appDataDir, "media");
    }

    public string Directory => _dir;

    /// <summary>Import a file from disk; returns the token name to embed.</summary>
    public string Import(string sourcePath)
        => Import(File.ReadAllBytes(sourcePath), Path.GetFileName(sourcePath));

    /// <summary>Import raw bytes under an original file name (used by .apkg
    /// import); returns the token name.</summary>
    public string Import(byte[] bytes, string fileName)
    {
        System.IO.Directory.CreateDirectory(_dir);

        var token = TokenFor(bytes, fileName);
        var dest = Path.Combine(_dir, token);
        // Same hash + name means identical content already stored — skip.
        if (!File.Exists(dest))
            File.WriteAllBytes(dest, bytes);
        return token;
    }

    /// <summary>Absolute path for a token, or null if it isn't stored.</summary>
    public string? Resolve(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var path = Path.Combine(_dir, name);
        return File.Exists(path) ? path : null;
    }

    /// <summary>The token a file's bytes + name would be stored under.</summary>
    public static string TokenFor(byte[] bytes, string fileName)
    {
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()[..12];
        return $"{hash}-{Sanitize(fileName)}";
    }

    private static string Sanitize(string fileName)
    {
        var name = Path.GetFileName(fileName).ToLowerInvariant();
        var sb = new StringBuilder(name.Length);
        foreach (var ch in name)
            sb.Append(ch is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '.' or '_' or '-' ? ch : '-');
        var cleaned = sb.ToString().Trim('-');
        return cleaned.Length == 0 ? "file" : cleaned;
    }
}
