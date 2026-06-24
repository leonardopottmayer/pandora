using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.SetUserCategoryActive;

public sealed class SetUserCategoryActiveCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<SetUserCategoryActiveCommand, UserCategoryDto>
{
    protected override async Task<Result<UserCategoryDto>> HandleAsync(
        SetUserCategoryActiveCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var now = timeProvider.GetUtcNow();

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IUserCategoryRepository>();

            var category = await repo.FindByIdForUserAsync(input.CategoryId, input.UserId, token);
            if (category is null)
                return Result<UserCategory>.Failure([CategoryErrors.NotFound]);

            if (category.IsActive == input.Active)
                return Result<UserCategory>.Success(category); // idempotent: no change, no event

            if (input.Active)
                category.Activate();
            else
                category.Deactivate();

            await repo.UpdateAsync(category, token);

            await ctx.RecordAsync(
                input.UserId, input.UserId, UserCategoryEvents.EntityType, category.Id,
                input.Active ? UserCategoryEvents.Activated : UserCategoryEvents.Deactivated, now, ct: token);

            return Result<UserCategory>.Success(category);
        }, cancellationToken: ct);

        return result.IsFailure
            ? Fail([.. result.Errors])
            : Ok(UserCategoryDto.From(result.Value!));
    }
}
