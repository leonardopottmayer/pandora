using Pottmayer.Pandora.Modules.Finances.Application.Services;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Finances.Tests.Fakes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Finances.Tests;

public sealed class InstallmentPlanAssemblerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 6, 13);
    private static readonly CurrencyCode Brl = CurrencyCode.Create("BRL");
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CardId = Guid.NewGuid();

    private static CardStatement Statement(string referenceMonth, StatementStatus status)
    {
        var time = new FixedTimeProvider(Now);
        var s = CardStatement.Create(UserId, CardId, referenceMonth, Today, Today.AddDays(7), time);
        if (status == StatementStatus.Paid)
            s.SyncAmounts(100m, 100m, Today, time);           // remaining 0 → paid
        else if (status == StatementStatus.Closed)
        {
            s.SyncAmounts(100m, 0m, Today, time);
            s.Close(time);
            s.SyncAmounts(100m, 0m, Today, time);             // closed, unpaid
        }
        return s;
    }

    private static Transaction Installment(Guid planId, short number, Guid statementId, decimal amount)
        => Transaction.CreateInstallmentTransaction(
            userId: UserId,
            cardId: CardId,
            cardStatementId: statementId,
            installmentPlanId: planId,
            installmentNumber: number,
            currency: Brl,
            amount: amount,
            occurredOn: Today,
            description: "TV 3x",
            payee: null,
            notes: null,
            systemCategoryId: null,
            userCategoryId: null,
            timeProvider: new FixedTimeProvider(Now));

    [Fact]
    public void Assemble_counts_paid_and_sums_remaining_on_unpaid_statements()
    {
        var plan = InstallmentPlan.CreateManual(UserId, CardId, 300m, 3, "2026-06", "TV 3x", new FixedTimeProvider(Now));

        var paidStatement = Statement("2026-06", StatementStatus.Paid);
        var openStatement2 = Statement("2026-07", StatementStatus.Closed);
        var openStatement3 = Statement("2026-08", StatementStatus.Open);

        var installments = new[]
        {
            Installment(plan.Id, 1, paidStatement.Id, 100m),
            Installment(plan.Id, 2, openStatement2.Id, 100m),
            Installment(plan.Id, 3, openStatement3.Id, 100m),
        };

        var byId = new Dictionary<Guid, CardStatement>
        {
            [paidStatement.Id] = paidStatement,
            [openStatement2.Id] = openStatement2,
            [openStatement3.Id] = openStatement3,
        };

        var dto = InstallmentPlanAssembler.Assemble(plan, installments, byId);

        Assert.Equal(1, dto.PaidInstallments);
        Assert.Equal(200m, dto.RemainingAmount); // the two unpaid installments
        Assert.Equal(3, dto.Installments.Count);
        Assert.Equal(plan.Id, dto.Id);
        Assert.Equal(300m, dto.TotalAmount);
    }

    [Fact]
    public void Assemble_orders_items_by_installment_number()
    {
        var plan = InstallmentPlan.CreateManual(UserId, CardId, 300m, 3, "2026-06", "TV 3x", new FixedTimeProvider(Now));
        var s = Statement("2026-06", StatementStatus.Open);
        var byId = new Dictionary<Guid, CardStatement> { [s.Id] = s };

        // Provided out of order.
        var installments = new[]
        {
            Installment(plan.Id, 3, s.Id, 100m),
            Installment(plan.Id, 1, s.Id, 100m),
            Installment(plan.Id, 2, s.Id, 100m),
        };

        var dto = InstallmentPlanAssembler.Assemble(plan, installments, byId);

        Assert.Equal([1, 2, 3], dto.Installments.Select(i => (int)i.Number));
    }

    [Fact]
    public void Assemble_excludes_void_installments_from_remaining()
    {
        var plan = InstallmentPlan.CreateManual(UserId, CardId, 200m, 2, "2026-06", "TV 2x", new FixedTimeProvider(Now));
        var s1 = Statement("2026-06", StatementStatus.Open);
        var s2 = Statement("2026-07", StatementStatus.Open);

        var voided = Installment(plan.Id, 1, s1.Id, 100m);
        voided.Void("cancel", new FixedTimeProvider(Now));
        var active = Installment(plan.Id, 2, s2.Id, 100m);

        var byId = new Dictionary<Guid, CardStatement> { [s1.Id] = s1, [s2.Id] = s2 };

        var dto = InstallmentPlanAssembler.Assemble(plan, [voided, active], byId);

        Assert.Equal(0, dto.PaidInstallments);
        Assert.Equal(100m, dto.RemainingAmount); // void one drops out
    }
}
