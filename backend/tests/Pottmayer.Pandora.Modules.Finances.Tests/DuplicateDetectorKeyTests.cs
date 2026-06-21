using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Services;
using Pottmayer.Pandora.Modules.Finances.Infrastructure.Import;
using Xunit;

namespace Pottmayer.Pandora.Modules.Finances.Tests;

/// <summary>
/// Unit coverage for the pure dedup-key computation (<see cref="DuplicateDetector"/> internals). The
/// repository-driven <c>DetectAsync</c> path is exercised by the integration suite; here we pin the
/// key derivation that decides whether two rows are considered the same movement.
/// </summary>
public sealed class DuplicateDetectorKeyTests
{
    private static readonly Guid Dest = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static ParsedImportRow Row(
        string description = "Coffee", decimal amount = 12.50m, string? externalId = null,
        int year = 2026, int month = 6, int day = 10) =>
        new(
            RowIndex: 0,
            RawData: "raw",
            Description: description,
            Payee: null,
            OccurredOn: new DateOnly(year, month, day),
            Amount: amount,
            IsCredit: false,
            Currency: "BRL",
            ExternalId: externalId,
            InstallmentNumber: null,
            InstallmentCount: null);

    [Fact]
    public void ComputeDedupKey_prefers_external_id_when_present()
    {
        var withId = DuplicateDetector.ComputeDedupKey(Row(externalId: "FITID-1"), Dest);
        var contentKey = DuplicateDetector.BuildFallbackKey(Row(externalId: "FITID-1"), Dest);

        // The FITID-based key is distinct from the content hash for the same row.
        Assert.NotNull(withId);
        Assert.NotEqual(contentKey, withId);
    }

    [Fact]
    public void ComputeDedupKey_falls_back_to_content_hash_without_external_id()
    {
        var key = DuplicateDetector.ComputeDedupKey(Row(externalId: null), Dest);
        var fallback = DuplicateDetector.BuildFallbackKey(Row(externalId: null), Dest);

        Assert.Equal(fallback, key);
    }

    [Fact]
    public void Same_external_id_and_destination_yields_the_same_key()
    {
        var a = DuplicateDetector.ComputeDedupKey(Row(description: "A", externalId: "X"), Dest);
        var b = DuplicateDetector.ComputeDedupKey(Row(description: "B", amount: 999m, externalId: "X"), Dest);

        // With a FITID, only id + destination matter — description/amount are ignored.
        Assert.Equal(a, b);
    }

    [Fact]
    public void Different_destination_changes_the_key()
    {
        var other = Guid.Parse("22222222-2222-2222-2222-222222222222");

        Assert.NotEqual(
            DuplicateDetector.ComputeDedupKey(Row(externalId: "X"), Dest),
            DuplicateDetector.ComputeDedupKey(Row(externalId: "X"), other));
    }

    [Fact]
    public void Content_key_is_stable_across_description_casing_and_whitespace()
    {
        var a = DuplicateDetector.BuildFallbackKey(Row(description: "Coffee  Shop"), Dest);
        var b = DuplicateDetector.BuildFallbackKey(Row(description: "  coffee shop  "), Dest);

        // NormalizeDescription lowercases and collapses whitespace.
        Assert.Equal(a, b);
    }

    [Fact]
    public void Content_key_changes_with_amount_or_date()
    {
        var baseline = DuplicateDetector.BuildFallbackKey(Row(), Dest);
        var otherAmount = DuplicateDetector.BuildFallbackKey(Row(amount: 12.51m), Dest);
        var otherDate = DuplicateDetector.BuildFallbackKey(Row(day: 11), Dest);

        Assert.NotEqual(baseline, otherAmount);
        Assert.NotEqual(baseline, otherDate);
    }
}
