using Pottmayer.Pandora.Modules.Assistant.Abstractions;
using Pottmayer.Pandora.Modules.Assistant.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Integrations.Abstractions.Models;
using Pottmayer.Pandora.Modules.Integrations.Abstractions.Ports;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Assistant.Application.Integrations;

/// <summary>
/// The assistant's answer to "which of my accounts do you use?": the API key its chat runs on. Shown on
/// the connections screen as "used by", and before that key is disconnected.
/// </summary>
public sealed class AssistantAccountUsageSource(IUnitOfWorkFactory factory) : IExternalAccountUsageSource
{
    public async Task<IReadOnlyList<ExternalAccountUsage>> ListAsync(Guid userId, CancellationToken ct = default)
    {
        var profile = await factory.ExecuteAsync(AssistantModule.DatabaseKey, async (context, token) =>
            await context.AcquireRepository<IAssistantProfileRepository>().FindByUserAsync(userId, token),
            cancellationToken: ct);

        return profile?.ChatAccountId is { } accountId
            ? [new ExternalAccountUsage(accountId, AssistantModule.Name, "chat")]
            : [];
    }
}
