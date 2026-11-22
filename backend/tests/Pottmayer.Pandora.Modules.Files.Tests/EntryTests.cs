using Pottmayer.Pandora.Modules.Files.Domain.Entities;
using Pottmayer.Pandora.Modules.Files.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.Modules.Files.Tests;

public sealed class EntryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
    private const string Hash = "0000000000000000000000000000000000000000000000000000000000000000";

    private static Entry File(string path = "/Movies/a.MKV", long size = 100, DateTimeOffset? modified = null, string? fingerprint = Hash) =>
        Entry.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), EntryKind.File, path, size, modified ?? Now, fingerprint, null, Now);

    [Fact]
    public void A_file_takes_its_name_extension_and_category_from_the_path()
    {
        var entry = File();

        Assert.Equal("/Movies", entry.ParentPath);
        Assert.Equal("a.MKV", entry.Name);
        Assert.Equal("mkv", entry.Extension);
        Assert.Equal(FileCategory.Video, entry.Category);
    }

    [Fact]
    public void An_unchanged_file_keeps_its_fingerprint()
    {
        var entry = File();

        // A date with sub-microsecond ticks, as the agent may send it, compares equal to the stored one.
        var changed = entry.See(Guid.NewGuid(), 100, Now.AddTicks(3), null);

        Assert.False(changed);
        Assert.Equal(Hash, entry.Fingerprint);
        Assert.False(entry.NeedsFingerprint);
    }

    [Fact]
    public void A_changed_file_loses_its_fingerprint_until_the_agent_sends_one()
    {
        var entry = File();

        var changed = entry.See(Guid.NewGuid(), 200, Now, null);

        Assert.True(changed);
        Assert.Equal(200, entry.SizeBytes);
        Assert.True(entry.NeedsFingerprint);
    }

    [Fact]
    public void A_moved_entry_keeps_its_id_and_takes_the_new_place()
    {
        var old = File("/Movies/a.mkv");
        var newer = File("/Archive/2026/a.mkv");
        var id = old.Id;

        old.MoveTo(newer);

        Assert.Equal(id, old.Id);
        Assert.Equal(newer.RootId, old.RootId);
        Assert.Equal("/Archive/2026/a.mkv", old.RelativePath);
        Assert.Equal("/Archive/2026", old.ParentPath);
    }

    [Theory]
    [InlineData(0, 10, false)]
    [InlineData(2, 10, false)]
    [InlineData(3, 10, true)]
    [InlineData(1, 0, true)]
    public void The_safety_brake_fires_above_a_fifth_of_the_root(int missing, int presentBefore, bool trips) =>
        Assert.Equal(trips, Scan.TripsSafetyBrake(missing, presentBefore));

    [Fact]
    public void Built_in_filters_exclude_system_clutter()
    {
        var builtins = Filter.CreateBuiltins(Guid.NewGuid()).ToList();

        Assert.All(builtins, f => Assert.True(f.IsBuiltin && f.IsEnabled && f.Action == FilterAction.Exclude));
        Assert.Contains(builtins, f => f.Pattern == "$RECYCLE.BIN" && f.AppliesTo == FilterTarget.Folder);
        Assert.Contains(builtins, f => f.Pattern == "Thumbs.db" && f.AppliesTo == FilterTarget.File);
    }
}
