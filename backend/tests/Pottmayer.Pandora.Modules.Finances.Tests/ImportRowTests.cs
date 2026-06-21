using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Xunit;

namespace Pottmayer.Pandora.Modules.Finances.Tests;

public sealed class ImportRowTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 13, 13, 0, 0, TimeSpan.Zero);

    private static ImportRow NewRow() =>
        ImportRow.CreatePending(Guid.NewGuid(), rowIndex: 5, rawData: "2026-06-10,Coffee,12.50", now: Now);

    [Fact]
    public void CreatePending_starts_pending_and_new_with_raw_preserved()
    {
        var row = NewRow();

        Assert.Equal("pending", row.Status);
        Assert.Equal("new", row.DedupStatus);
        Assert.Equal(5, row.RowIndex);
        Assert.Equal("2026-06-10,Coffee,12.50", row.RawData);
        Assert.Equal(Now, row.CreatedAt);
    }

    [Fact]
    public void SetParsed_records_payload_and_dedup_inputs()
    {
        var row = NewRow();

        row.SetParsed("{\"amount\":12.50}", externalId: "fitid-1", dedupKey: "key-1",
            installmentNumber: 2, installmentCount: 6);

        Assert.Equal("{\"amount\":12.50}", row.ParsedPayload);
        Assert.Equal("fitid-1", row.ExternalId);
        Assert.Equal("key-1", row.DedupKey);
        Assert.Equal((short)2, row.InstallmentNumber);
        Assert.Equal((short)6, row.InstallmentCount);
    }

    [Fact]
    public void SetDedup_stores_status_and_matches()
    {
        var row = NewRow();
        var txId = Guid.NewGuid();
        var pendingId = Guid.NewGuid();

        row.SetDedup("certain", txId, pendingId);

        Assert.Equal("certain", row.DedupStatus);
        Assert.Equal(txId, row.MatchedTransactionId);
        Assert.Equal(pendingId, row.MatchedPendingTransactionId);
    }

    [Fact]
    public void MarkMatched_links_transaction_and_flags_matched()
    {
        var row = NewRow();
        var txId = Guid.NewGuid();

        row.MarkMatched(txId);

        Assert.Equal(txId, row.MatchedTransactionId);
        Assert.Equal("matched", row.DedupStatus);
    }

    [Fact]
    public void MarkSuggestionCreated_links_pending_and_sets_status()
    {
        var row = NewRow();
        var pendingId = Guid.NewGuid();

        row.MarkSuggestionCreated(pendingId);

        Assert.Equal(pendingId, row.PendingTransactionId);
        Assert.Equal("suggestion-created", row.Status);
    }

    [Fact]
    public void MarkSkipped_and_MarkError_set_terminal_status()
    {
        var skipped = NewRow();
        skipped.MarkSkipped();
        Assert.Equal("skipped", skipped.Status);

        var errored = NewRow();
        errored.MarkError("bad row");
        Assert.Equal("error", errored.Status);
        Assert.Equal("bad row", errored.ErrorMessage);
    }

    [Fact]
    public void SetMatchedInstallmentPlan_records_plan()
    {
        var row = NewRow();
        var planId = Guid.NewGuid();

        row.SetMatchedInstallmentPlan(planId);

        Assert.Equal(planId, row.MatchedInstallmentPlanId);
    }
}
