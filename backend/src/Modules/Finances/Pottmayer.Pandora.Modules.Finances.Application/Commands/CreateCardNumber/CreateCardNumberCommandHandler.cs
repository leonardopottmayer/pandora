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

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.CreateCardNumber;

public sealed class CreateCardNumberCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<CreateCardNumberCommand, CardNumberDto>
{
    protected override async Task<Result<CardNumberDto>> HandleAsync(CreateCardNumberCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if (!CardNumberKind.IsSupported(input.Kind))
            return Fail(CardNumberErrors.InvalidKind(input.Kind));
        if (!CardNumber.IsValidLastFour(input.LastFour))
            return Fail(CardNumberErrors.InvalidLastFour);

        var now = timeProvider.GetUtcNow();
        var result = await factory.ExecuteAsync(FinancesModule.DatabaseKey, async (ctx, token) =>
        {
            var card = await ctx.AcquireRepository<ICardRepository>().FindByIdForUserAsync(input.CardId, input.UserId, token);
            if (card is null)
                return Result<CardNumber>.Failure([CardErrors.NotFound]);
            if (card.IsArchived)
                return Result<CardNumber>.Failure([CardErrors.Archived]);

            var numbers = ctx.AcquireRepository<ICardNumberRepository>();
            if (await numbers.ExistsWithLastFourAsync(card.Id, input.LastFour, null, token))
                return Result<CardNumber>.Failure([CardNumberErrors.LastFourAlreadyExists]);

            var number = CardNumber.Create(
                input.UserId, card.Id, CardNumberKind.FromValue(input.Kind), input.LastFour, input.Nickname, timeProvider);
            await numbers.AddAsync(number, token);
            await ctx.RecordAsync(input.UserId, input.UserId, CardEvents.EntityType, card.Id, CardEvents.NumberAdded, now,
                new { numberId = number.Id, kind = number.Kind.Value, number.LastFour, number.Nickname }, ct: token);

            return Result<CardNumber>.Success(number);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(CardNumberDto.From(result.Value!));
    }
}
