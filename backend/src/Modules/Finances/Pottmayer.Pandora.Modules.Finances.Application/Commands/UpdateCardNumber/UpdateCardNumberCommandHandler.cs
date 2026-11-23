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

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.UpdateCardNumber;

public sealed class UpdateCardNumberCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<UpdateCardNumberCommand, CardNumberDto>
{
    protected override async Task<Result<CardNumberDto>> HandleAsync(UpdateCardNumberCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if (!CardNumberKind.IsSupported(input.Kind))
            return Fail(CardNumberErrors.InvalidKind(input.Kind));
        if (!CardNumber.IsValidLastFour(input.LastFour))
            return Fail(CardNumberErrors.InvalidLastFour);

        var now = timeProvider.GetUtcNow();
        var result = await factory.ExecuteAsync(FinancesModule.DatabaseKey, async (ctx, token) =>
        {
            var numbers = ctx.AcquireRepository<ICardNumberRepository>();
            var number = await numbers.FindByIdForUserAsync(input.NumberId, input.UserId, token);
            if (number is null || number.CardId != input.CardId)
                return Result<CardNumber>.Failure([CardNumberErrors.NotFound]);

            if (await numbers.ExistsWithLastFourAsync(number.CardId, input.LastFour, number.Id, token))
                return Result<CardNumber>.Failure([CardNumberErrors.LastFourAlreadyExists]);

            var diff = new
            {
                numberId = number.Id,
                kind = new { old = number.Kind.Value, @new = input.Kind },
                lastFour = new { old = number.LastFour, @new = input.LastFour.Trim() },
                nickname = new { old = number.Nickname, @new = input.Nickname }
            };

            if (!number.Update(CardNumberKind.FromValue(input.Kind), input.LastFour, input.Nickname))
                return Result<CardNumber>.Failure([CardNumberErrors.Archived]);

            await numbers.UpdateAsync(number, token);
            await ctx.RecordAsync(input.UserId, input.UserId, CardEvents.EntityType, number.CardId, CardEvents.NumberUpdated, now, diff, ct: token);
            return Result<CardNumber>.Success(number);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(CardNumberDto.From(result.Value!));
    }
}
