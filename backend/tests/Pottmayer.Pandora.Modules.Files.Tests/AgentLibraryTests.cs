using System.Security.Cryptography;
using System.Text;
using Pottmayer.Pandora.Modules.Files.Agent;
using Xunit;

namespace Pottmayer.Pandora.Modules.Files.Tests;

public sealed class AgentLibraryTests
{
    [Theory]
    [InlineData(@"Movies\Breaking Bad\s02e01.mkv", "/Movies/Breaking Bad/s02e01.mkv")]
    [InlineData("/Movies/", "/Movies")]
    [InlineData("", "/")]
    public void Paths_normalize_to_the_catalog_form(string input, string expected) =>
        Assert.Equal(expected, CatalogPath.Normalize(input));

    [Fact]
    public void A_mac_name_and_a_windows_name_normalize_to_the_same_path()
    {
        var nfd = "/Ação";   // as macOS hands it out
        var nfc = "/Ação";

        Assert.Equal(nfc, CatalogPath.Normalize(nfd));
    }

    [Theory]
    [InlineData("/a/b", true)]
    [InlineData("/", true)]
    [InlineData("a/b", false)]
    [InlineData("/a//b", false)]
    [InlineData("/a/../b", false)]
    [InlineData("/a/", false)]
    public void Only_catalog_paths_are_valid(string path, bool valid) =>
        Assert.Equal(valid, CatalogPath.IsValid(path));

    [Fact]
    public void Path_parts()
    {
        Assert.Equal("/A", CatalogPath.Parent("/A/b.txt"));
        Assert.Equal("/", CatalogPath.Parent("/b.txt"));
        Assert.Equal("b.txt", CatalogPath.Name("/A/b.txt"));
        Assert.Equal("mkv", CatalogPath.Extension("Movie.MKV"));
        Assert.Null(CatalogPath.Extension(".gitignore"));
        Assert.True(CatalogPath.IsUnder("/A/B", "/A"));
        Assert.False(CatalogPath.IsUnder("/AB", "/A"));
        Assert.False(CatalogPath.IsUnder("/A", "/A"));
    }

    [Fact]
    public void Matchers_validate_their_pattern()
    {
        Assert.Null(NameMatcher.Validate(NameMatcher.Regex, "^s\\d{2}e\\d{2}"));
        Assert.NotNull(NameMatcher.Validate(NameMatcher.Regex, "(unclosed"));
        Assert.NotNull(NameMatcher.Validate(NameMatcher.Extension, ", ,"));
        Assert.NotNull(NameMatcher.Validate("soundex", "x"));
    }

    [Fact]
    public void Matchers_honour_case_sensitivity()
    {
        var insensitive = NameMatcher.Create(NameMatcher.StartsWith, "thumbs", caseSensitive: false);
        var sensitive = NameMatcher.Create(NameMatcher.StartsWith, "thumbs", caseSensitive: true);

        Assert.True(insensitive("Thumbs.db", "/Thumbs.db"));
        Assert.False(sensitive("Thumbs.db", "/Thumbs.db"));
    }

    [Fact]
    public async Task A_small_file_is_hashed_whole()
    {
        var bytes = Encoding.UTF8.GetBytes("hello");

        var fingerprint = await Fingerprint.ComputeAsync(new MemoryStream(bytes));

        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), fingerprint);
        Assert.True(Fingerprint.IsValid(fingerprint));
    }

    [Fact]
    public async Task A_large_file_is_hashed_by_its_ends()
    {
        var bytes = new byte[Fingerprint.ChunkSize * 3];
        Random.Shared.NextBytes(bytes);
        var ends = bytes.AsSpan(0, Fingerprint.ChunkSize).ToArray().Concat(bytes[^Fingerprint.ChunkSize..]).ToArray();

        var fingerprint = await Fingerprint.ComputeAsync(new MemoryStream(bytes));

        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(ends)), fingerprint);

        // The middle does not count: that is the trade-off for reading 128 KiB per file.
        bytes[Fingerprint.ChunkSize + 1] ^= 0xFF;
        Assert.Equal(fingerprint, await Fingerprint.ComputeAsync(new MemoryStream(bytes)));
    }
}
