using Pottmayer.Pandora.Modules.Integrations.Abstractions;
using Pottmayer.Pandora.Modules.Integrations.Application.Dtos;
using Pottmayer.Pandora.Modules.Integrations.Application.Oauth;
using Pottmayer.Pandora.Modules.Integrations.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Integrations.Application.Queries.GetAccounts;

public sealed class GetAccountsQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetAccountsQuery, IReadOnlyList<ExternalAccountDto>>
{
    protected override async Task<Result<IReadOnlyList<ExternalAccountDto>>> HandleAsync(
        GetAccountsQuery request, CancellationToken cancellationToken)
    {
        var accounts = await factory.ExecuteAsync(IntegrationsModule.Name, async (context, ct) =>
        {
            var repo = context.AcquireRepository<IExternalAccountRepository>();
            return await repo.GetByUserAsync(request.Input.UserId, ct);
        }, cancellationToken: cancellationToken);

        IReadOnlyList<ExternalAccountDto> dtos = [.. accounts.Select(a => new ExternalAccountDto(
            a.Id,
            a.Provider,
            a.AuthKind.Value,
            a.DisplayName,
            ScopeString.Split(a.Scopes),
            a.Status.Value,
            a.LastError,
            a.ConnectedAt,
            a.LastRefreshedAt))];

        return Ok(dtos);
    }
}
