using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.DeleteCard;

public sealed class DeleteCardCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<DeleteCardCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(DeleteCardCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var now = timeProvider.GetUtcNow();

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<ICardRepository>();
            var card = await repo.FindByIdForUserAsync(input.CardId, input.UserId, token);
            if (card is null)
                return Result<bool>.Failure([CardErrors.NotFound]);

            var statements = ctx.AcquireRepository<ICardStatementRepository>();
            var existingStatements = await statements.GetByCardAsync(card.Id, input.UserId, token);
            if (existingStatements.Count > 0)
                return Result<bool>.Failure([CardErrors.HasHistory]);

            await repo.RemoveAsync(card, token);
            await ctx.RecordAsync(input.UserId, input.UserId, "card", card.Id, "card.deleted", now, new { card.Name }, ct: token);
            return Result<bool>.Success(true);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(true);
    }
}
