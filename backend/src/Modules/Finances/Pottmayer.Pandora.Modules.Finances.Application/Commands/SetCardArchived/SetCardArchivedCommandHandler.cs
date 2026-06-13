using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.SetCardArchived;

public sealed class SetCardArchivedCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<SetCardArchivedCommand, CardDto>
{
    protected override async Task<Result<CardDto>> HandleAsync(SetCardArchivedCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var now = timeProvider.GetUtcNow();

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<ICardRepository>();
            var card = await repo.FindByIdForUserAsync(input.CardId, input.UserId, token);
            if (card is null)
                return Result<Card>.Failure([CardErrors.NotFound]);

            if (card.IsArchived == input.Archived)
                return Result<Card>.Success(card);

            if (input.Archived) card.Archive(timeProvider);
            else card.Unarchive();

            await repo.UpdateAsync(card, token);
            await ctx.RecordAsync(
                input.UserId, input.UserId, "card", card.Id,
                input.Archived ? "card.archived" : "card.unarchived", now, ct: token);

            return Result<Card>.Success(card);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(CardDto.From(result.Value!));
    }
}
