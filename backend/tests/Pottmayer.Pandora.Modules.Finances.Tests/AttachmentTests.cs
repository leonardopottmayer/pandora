using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.Modules.Finances.Tests;

public sealed class AttachmentTests
{
    private static Attachment Create(Guid? transactionId, Guid? pendingTransactionId, Guid? cardStatementId = null) =>
        Attachment.Create(Guid.NewGuid(), transactionId, pendingTransactionId, cardStatementId, AttachmentKind.Bill,
            "boleto.pdf", "application/pdf", 10, "Database", "key", note: null, TimeProvider.System);

    [Fact]
    public void Belongs_to_at_most_one_owner()
    {
        Assert.Throws<ArgumentException>(() => Create(Guid.NewGuid(), Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => Create(null, Guid.NewGuid(), Guid.NewGuid()));
        Assert.False(Create(null, null, Guid.NewGuid()).IsQueued);
    }

    [Fact]
    public void With_no_owner_it_waits_in_the_queue_until_assigned_to_exactly_one()
    {
        var attachment = Create(null, null);
        Assert.True(attachment.IsQueued);
        Assert.Throws<ArgumentException>(() => attachment.AssignTo(null, null, null));
        Assert.Throws<ArgumentException>(() => attachment.AssignTo(Guid.NewGuid(), null, Guid.NewGuid()));

        var statement = Guid.NewGuid();
        attachment.AssignTo(null, null, statement);

        Assert.False(attachment.IsQueued);
        Assert.Equal(statement, attachment.CardStatementId);
        Assert.Throws<InvalidOperationException>(() => attachment.AssignTo(Guid.NewGuid(), null, null));
    }

    [Fact]
    public void Moving_to_the_transaction_leaves_the_suggestion()
    {
        var attachment = Create(null, Guid.NewGuid());
        var tx = Guid.NewGuid();

        attachment.MoveToTransaction(tx);

        Assert.Equal(tx, attachment.TransactionId);
        Assert.Null(attachment.PendingTransactionId);
    }

    [Theory]
    [InlineData("bill", true)]
    [InlineData("receipt", true)]
    [InlineData("invoice", true)]
    [InlineData("other", true)]
    [InlineData("Receipt", false)]
    [InlineData(null, false)]
    public void Kinds_are_a_closed_set(string? value, bool supported) =>
        Assert.Equal(supported, AttachmentKind.IsSupported(value));
}
