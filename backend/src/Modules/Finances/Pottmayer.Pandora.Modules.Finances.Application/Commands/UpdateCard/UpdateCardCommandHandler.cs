using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.CreateCard;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.UpdateCard;

public sealed class UpdateCardCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<UpdateCardCommand, CardDto>
{
    protected override async Task<Result<CardDto>> HandleAsync(UpdateCardCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if (string.IsNullOrWhiteSpace(input.Name))
            return Fail(CardErrors.InvalidName);
        if (!string.IsNullOrWhiteSpace(input.LastFour) && (input.LastFour.Trim().Length != 4 || !input.LastFour.Trim().All(char.IsDigit)))
            return Fail(CardErrors.InvalidLastFour);
        if (input.CreditLimit is < 0m)
            return Fail(CardErrors.InvalidCreditLimit);
        if (input.ClosingDay is < 1 or > 28)
            return Fail(CardErrors.InvalidClosingDay);
        if (input.DueDay is < 1 or > 28)
            return Fail(CardErrors.InvalidDueDay);

        var now = timeProvider.GetUtcNow();
        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<ICardRepository>();
            var card = await repo.FindByIdForUserAsync(input.CardId, input.UserId, token);
            if (card is null)
                return Result<Card>.Failure([CardErrors.NotFound]);

            // Excludes the card itself, so renaming to the same name is allowed.
            if (await repo.ExistsWithNameAsync(input.UserId, input.Name, input.CardId, token))
                return Result<Card>.Failure([CardErrors.NameAlreadyExists]);

            if (input.DefaultPaymentAccountId is not null)
            {
                var account = await ctx.AcquireRepository<IAccountRepository>()
                    .FindByIdForUserAsync(input.DefaultPaymentAccountId.Value, input.UserId, token);
                if (account is null)
                    return Result<Card>.Failure([CardErrors.DefaultPaymentAccountNotFound]);
            }

            // The aggregate itself refuses the update once archived.
            if (!card.Update(input.Name, input.Brand, input.LastFour, input.CreditLimit, input.ClosingDay, input.DueDay, input.DefaultPaymentAccountId))
                return Result<Card>.Failure([CardErrors.Archived]);

            await repo.UpdateAsync(card, token);
            await ctx.RecordAsync(input.UserId, input.UserId, CardEvents.EntityType, card.Id, CardEvents.Updated, now, ct: token);
            return Result<Card>.Success(card);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(CardDto.From(result.Value!));
    }
}
