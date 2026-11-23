using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.DeleteCardNumber;

public sealed class DeleteCardNumberCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<DeleteCardNumberCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(DeleteCardNumberCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var now = timeProvider.GetUtcNow();

        var result = await factory.ExecuteAsync(FinancesModule.DatabaseKey, async (ctx, token) =>
        {
            var numbers = ctx.AcquireRepository<ICardNumberRepository>();
            var number = await numbers.FindByIdForUserAsync(input.NumberId, input.UserId, token);
            if (number is null || number.CardId != input.CardId)
                return Result<bool>.Failure([CardNumberErrors.NotFound]);

            var details = new { numberId = number.Id, kind = number.Kind.Value, number.LastFour, number.Nickname };
            if (await ctx.AcquireRepository<ITransactionRepository>().ExistsForCardNumberAsync(number.Id, token))
            {
                if (!number.IsArchived)
                {
                    number.Archive(timeProvider);
                    await numbers.UpdateAsync(number, token);
                    await ctx.RecordAsync(input.UserId, input.UserId, CardEvents.EntityType, number.CardId, CardEvents.NumberArchived, now, details, ct: token);
                }
                return Result<bool>.Success(false);
            }

            await numbers.RemoveAsync(number, token);
            await ctx.RecordAsync(input.UserId, input.UserId, CardEvents.EntityType, number.CardId, CardEvents.NumberRemoved, now, details, ct: token);
            return Result<bool>.Success(true);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value);
    }
}
