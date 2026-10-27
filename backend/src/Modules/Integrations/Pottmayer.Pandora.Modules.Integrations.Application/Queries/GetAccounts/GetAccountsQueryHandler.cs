using Microsoft.Extensions.Logging;
using Pottmayer.Pandora.Modules.Integrations.Abstractions;
using Pottmayer.Pandora.Modules.Integrations.Abstractions.Models;
using Pottmayer.Pandora.Modules.Integrations.Abstractions.Ports;
using Pottmayer.Pandora.Modules.Integrations.Application.Dtos;
using Pottmayer.Pandora.Modules.Integrations.Application.Oauth;
using Pottmayer.Pandora.Modules.Integrations.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Integrations.Application.Queries.GetAccounts;

/// <summary>
/// The user's accounts for the connections screen, each with the places other modules use it. Usages
/// come from every registered <see cref="IExternalAccountUsageSource"/>; a source that fails is skipped
/// and logged, so one broken consumer never takes the screen down.
/// </summary>
public sealed class GetAccountsQueryHandler(
    IUnitOfWorkFactory factory,
    IEnumerable<IExternalAccountUsageSource> usageSources,
    ILogger<GetAccountsQueryHandler> logger)
    : QueryHandlerBase<GetAccountsQuery, IReadOnlyList<ExternalAccountDto>>
{
    protected override async Task<Result<IReadOnlyList<ExternalAccountDto>>> HandleAsync(
        GetAccountsQuery request, CancellationToken cancellationToken)
    {
        var userId = request.Input.UserId;

        var accounts = await factory.ExecuteAsync(IntegrationsModule.DatabaseKey, async (context, ct) =>
        {
            var repo = context.AcquireRepository<IExternalAccountRepository>();
            return await repo.GetByUserAsync(userId, ct);
        }, cancellationToken: cancellationToken);

        var usages = new List<ExternalAccountUsage>();
        foreach (var source in usageSources)
        {
            try
            {
                usages.AddRange(await source.ListAsync(userId, cancellationToken));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Account usage source {Source} failed; its usages are left out.", source.GetType().Name);
            }
        }

        var usagesByAccount = usages.ToLookup(u => u.ExternalAccountId);

        IReadOnlyList<ExternalAccountDto> dtos = [.. accounts.Select(a => new ExternalAccountDto(
            a.Id,
            a.Provider,
            a.AuthKind.Value,
            a.Label,
            a.DisplayName,
            ScopeString.Split(a.Scopes),
            a.Status.Value,
            a.LastError,
            a.ConnectedAt,
            a.LastRefreshedAt,
            [.. usagesByAccount[a.Id].Select(u => new AccountUsageDto(u.Module, u.Purpose, u.Detail))]))];

        return Ok(dtos);
    }
}
