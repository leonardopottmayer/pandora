using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.Modules.Finances.Tests;

public sealed class AttachmentTests
{
    private static Attachment Create(Guid? transactionId, Guid? pendingTransactionId) =>
        Attachment.Create(Guid.NewGuid(), transactionId, pendingTransactionId, AttachmentKind.Bill,
            "boleto.pdf", "application/pdf", 10, "Database", "key", TimeProvider.System);

    [Fact]
    public void Belongs_to_exactly_one_owner()
    {
        Assert.Throws<ArgumentException>(() => Create(null, null));
        Assert.Throws<ArgumentException>(() => Create(Guid.NewGuid(), Guid.NewGuid()));
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
