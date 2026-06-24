using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.UpdateUserCategory;

public sealed class UpdateUserCategoryCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<UpdateUserCategoryCommand, UserCategoryDto>
{
    protected override async Task<Result<UserCategoryDto>> HandleAsync(
        UpdateUserCategoryCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var now = timeProvider.GetUtcNow();

        if (string.IsNullOrWhiteSpace(input.Name))
            return Fail(CategoryErrors.InvalidName);

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IUserCategoryRepository>();

            var category = await repo.FindByIdForUserAsync(input.CategoryId, input.UserId, token);
            if (category is null)
                return Result<UserCategory>.Failure([CategoryErrors.NotFound]);

            if (await repo.ExistsWithNameAsync(
                    input.UserId, input.Name, category.ParentCategoryId, category.Id, token))
                return Result<UserCategory>.Failure([CategoryErrors.NameAlreadyExists]);

            // Captured before the mutation so the audit event records both sides of the change.
            var diff = new
            {
                name = new { old = category.Name, @new = input.Name.Trim() },
                color = new { old = category.Color, @new = input.Color },
                icon = new { old = category.Icon, @new = input.Icon },
                displayOrder = new { old = category.DisplayOrder, @new = input.DisplayOrder }
            };

            category.Update(input.Name, input.Color, input.Icon, input.DisplayOrder);
            await repo.UpdateAsync(category, token);

            await ctx.RecordAsync(
                input.UserId, input.UserId, UserCategoryEvents.EntityType, category.Id, UserCategoryEvents.Updated, now, diff, ct: token);

            return Result<UserCategory>.Success(category);
        }, cancellationToken: ct);

        return result.IsFailure
            ? Fail([.. result.Errors])
            : Ok(UserCategoryDto.From(result.Value!));
    }
}
