using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.CreateCard;

public sealed class CreateCardCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<CreateCardCommand, CardDto>
{
    protected override async Task<Result<CardDto>> HandleAsync(CreateCardCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var validation = Validate(input.Name, input.LastFour, input.CreditLimit, input.ClosingDay, input.DueDay, input.Currency);
        if (validation.IsFailure)
            return Fail([.. validation.Errors]);

        var now = timeProvider.GetUtcNow();
        var currency = CurrencyCode.Create(input.Currency);

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<ICardRepository>();
            if (await repo.ExistsWithNameAsync(input.UserId, input.Name, null, token))
                return Result<Card>.Failure([CardErrors.NameAlreadyExists]);

            // The default payment account, if given, must belong to the same user.
            if (input.DefaultPaymentAccountId is not null)
            {
                var account = await ctx.AcquireRepository<IAccountRepository>()
                    .FindByIdForUserAsync(input.DefaultPaymentAccountId.Value, input.UserId, token);
                if (account is null)
                    return Result<Card>.Failure([CardErrors.DefaultPaymentAccountNotFound]);
            }

            var card = Card.Create(
                input.UserId,
                input.Name,
                input.Brand,
                input.LastFour,
                input.CreditLimit,
                input.ClosingDay,
                input.DueDay,
                currency,
                input.DefaultPaymentAccountId,
                timeProvider);

            await repo.AddAsync(card, token);
            await ctx.RecordAsync(input.UserId, input.UserId, CardEvents.EntityType, card.Id, CardEvents.Created, now, new
            {
                card.Name,
                card.ClosingDay,
                card.DueDay,
                currency = card.Currency.Value
            }, ct: token);

            return Result<Card>.Success(card);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(CardDto.From(result.Value!));
    }

    /// <summary>Validates the card's input shape. Shared with <see cref="UpdateCard"/> validation rules.</summary>
    internal static Result<bool> Validate(string name, string? lastFour, decimal? creditLimit, int closingDay, int dueDay, string currency)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result<bool>.Failure([CardErrors.InvalidName]);
        if (!string.IsNullOrWhiteSpace(lastFour) && (lastFour.Trim().Length != 4 || !lastFour.Trim().All(char.IsDigit)))
            return Result<bool>.Failure([CardErrors.InvalidLastFour]);
        if (creditLimit is < 0m)
            return Result<bool>.Failure([CardErrors.InvalidCreditLimit]);
        if (closingDay is < 1 or > 28)
            return Result<bool>.Failure([CardErrors.InvalidClosingDay]);
        if (dueDay is < 1 or > 28)
            return Result<bool>.Failure([CardErrors.InvalidDueDay]);
        if (!CurrencyCode.TryCreate(currency, out _))
            return Result<bool>.Failure([CardErrors.InvalidCurrency(currency)]);

        return Result<bool>.Success(true);
    }
}
