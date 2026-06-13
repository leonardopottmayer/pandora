using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetAccounts;

public sealed class GetAccountsQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetAccountsQuery, IReadOnlyList<AccountDto>>
{
    protected override async Task<Result<IReadOnlyList<AccountDto>>> HandleAsync(
        GetAccountsQuery request, CancellationToken ct)
    {
        var input = request.Input;

        var tagIds = input.TagIds?.Distinct().ToList();

        var accounts = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var all = await ctx.AcquireRepository<IAccountRepository>()
                .GetAllForUserAsync(input.UserId, input.IncludeArchived, token);

            if (tagIds is not { Count: > 0 })
                return all;

            // OR semantics: keep accounts carrying any of the requested tags.
            var matching = (await ctx.AcquireRepository<ITagLinkRepository>()
                .GetEntityIdsByTagsAsync(TaggableEntityType.Account, tagIds, token)).ToHashSet();
            return [.. all.Where(a => matching.Contains(a.Id))];
        }, cancellationToken: ct);

        IReadOnlyList<AccountDto> dtos = [.. accounts.Select(AccountDto.From)];
        return Ok(dtos);
    }
}
