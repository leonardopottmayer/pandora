using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Finances.Tests.Fakes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Finances.Tests;

public sealed class ImportFileTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 13, 11, 0, 0, TimeSpan.Zero);

    private static ImportFile NewFile(TimeProvider time, byte[]? content = null) =>
        ImportFile.Create(
            userId: Guid.NewGuid(),
            layoutId: Guid.NewGuid(),
            accountId: Guid.NewGuid(),
            cardId: null,
            fileName: "extrato.csv",
            fileHash: "abc",
            fileContent: content ?? [1, 2, 3, 4],
            timeProvider: time);

    [Fact]
    public void Create_starts_received_and_captures_size()
    {
        var file = NewFile(new FixedTimeProvider(Now), content: [1, 2, 3]);

        Assert.True(file.IsReceived);
        Assert.False(file.IsParsing);
        Assert.False(file.IsTerminal);
        Assert.Equal(3, file.FileSize);
        Assert.NotEqual(Guid.Empty, file.CorrelationId);
        Assert.Equal(Now, file.CreatedAt);
    }

    [Fact]
    public void StartParsing_only_from_received()
    {
        var time = new FixedTimeProvider(Now);
        var file = NewFile(time);

        Assert.True(file.StartParsing(time));
        Assert.True(file.IsParsing);
        Assert.Equal(Now, file.StartedAt);

        // Not received anymore → no-op.
        Assert.False(file.StartParsing(time));
    }

    [Fact]
    public void Complete_records_counters_and_terminal_state()
    {
        var time = new FixedTimeProvider(Now);
        var file = NewFile(time);
        file.StartParsing(time);

        file.Complete(total: 10, parsed: 8, errors: 1, duplicates: 1, suggestions: 7, timeProvider: time);

        Assert.True(file.IsTerminal);
        Assert.Equal(ImportFileStatus.Completed, file.Status);
        Assert.Equal(10, file.TotalRows);
        Assert.Equal(8, file.ParsedRows);
        Assert.Equal(1, file.ErrorRows);
        Assert.Equal(1, file.DuplicateRows);
        Assert.Equal(7, file.SuggestionRows);
        Assert.Equal(Now, file.CompletedAt);
    }

    [Fact]
    public void Fail_increments_retry_count_and_is_retryable()
    {
        var time = new FixedTimeProvider(Now);
        var file = NewFile(time);
        file.StartParsing(time);

        file.Fail("boom", time);

        Assert.Equal(ImportFileStatus.Failed, file.Status);
        Assert.Equal("boom", file.ErrorMessage);
        Assert.Equal(1, file.RetryCount);
        Assert.False(file.IsTerminal); // failed is not terminal — retry is available
    }

    [Fact]
    public void Retry_only_from_failed_and_resets_run_state()
    {
        var time = new FixedTimeProvider(Now);
        var file = NewFile(time);

        Assert.False(file.Retry(time)); // received → not retryable

        file.StartParsing(time);
        file.Fail("boom", time);

        Assert.True(file.Retry(time));
        Assert.True(file.IsReceived);
        Assert.Null(file.ErrorMessage);
        Assert.Null(file.StartedAt);
        Assert.Null(file.CompletedAt);
    }

    [Fact]
    public void Abort_blocked_once_terminal()
    {
        var time = new FixedTimeProvider(Now);
        var file = NewFile(time);

        Assert.True(file.Abort(time));
        Assert.Equal(ImportFileStatus.Aborted, file.Status);
        Assert.True(file.IsTerminal);

        Assert.False(file.Abort(time)); // already terminal
    }

    [Fact]
    public void Abort_blocked_after_completed()
    {
        var time = new FixedTimeProvider(Now);
        var file = NewFile(time);
        file.StartParsing(time);
        file.Complete(1, 1, 0, 0, 1, time);

        Assert.False(file.Abort(time));
        Assert.Equal(ImportFileStatus.Completed, file.Status);
    }
}
