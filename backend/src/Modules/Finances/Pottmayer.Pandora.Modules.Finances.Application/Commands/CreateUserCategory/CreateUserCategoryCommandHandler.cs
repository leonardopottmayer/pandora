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

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.CreateUserCategory;

public sealed class CreateUserCategoryCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<CreateUserCategoryCommand, UserCategoryDto>
{
    protected override async Task<Result<UserCategoryDto>> HandleAsync(
        CreateUserCategoryCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var now = timeProvider.GetUtcNow();

        if (string.IsNullOrWhiteSpace(input.Name))
            return Fail(CategoryErrors.InvalidName);

        if (!TransactionNature.IsSupported(input.Nature))
            return Fail(CategoryErrors.InvalidNature(input.Nature));

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IUserCategoryRepository>();

            var nature = TransactionNature.FromValue(input.Nature);

            if (input.ParentCategoryId is { } parentId)
            {
                var parent = await repo.FindByIdForUserAsync(parentId, input.UserId, token);
                if (parent is null)
                    return Result<UserCategory>.Failure([CategoryErrors.ParentNotFound]);
                if (!parent.IsRoot)
                    return Result<UserCategory>.Failure([CategoryErrors.ParentIsNotRoot]);
                if (parent.Nature.Value != input.Nature)
                    return Result<UserCategory>.Failure([CategoryErrors.NatureMismatch]);

                nature = parent.Nature;
            }

            if (await repo.ExistsWithNameAsync(input.UserId, input.Name, input.ParentCategoryId, null, token))
                return Result<UserCategory>.Failure([CategoryErrors.NameAlreadyExists]);

            var category = UserCategory.Create(
                input.UserId, input.Name, nature, input.ParentCategoryId,
                input.Color, input.Icon, input.DisplayOrder, timeProvider);
            await repo.AddAsync(category, token);

            await ctx.RecordAsync(
                input.UserId, input.UserId, "user-category", category.Id, "category.created", now,
                new
                {
                    name = category.Name,
                    nature = category.Nature.Value,
                    parentCategoryId = category.ParentCategoryId,
                    displayOrder = category.DisplayOrder
                },
                ct: token);

            return Result<UserCategory>.Success(category);
        }, cancellationToken: ct);

        return result.IsFailure
            ? Fail([.. result.Errors])
            : Ok(UserCategoryDto.From(result.Value!));
    }
}
