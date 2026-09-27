using Pottmayer.Pandora.Modules.Assistant.Abstractions;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Pandora.Modules.Assistant.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Assistant.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Assistant.Application.Interpret;

/// <summary>
/// Builds the <see cref="AssistantToolContext"/> for a user outside the interpret pipeline — confirming or
/// cancelling a held call, answering a button tap — from their profile's locale and their effective zone.
/// </summary>
public sealed class AssistantToolContextResolver(IUnitOfWorkFactory factory, IEffectiveTimeZoneResolver timeZones)
{
    public async Task<AssistantToolContext> ResolveAsync(Guid userId, CancellationToken ct = default)
    {
        var profile = await factory.ExecuteAsync(AssistantModule.DatabaseKey, async (context, token) =>
            await context.AcquireRepository<IAssistantProfileRepository>().FindByUserAsync(userId, token),
            cancellationToken: ct);

        var timeZone = await timeZones.ResolveAsync(userId, ct: ct);
        return For(userId, profile, timeZone);
    }

    /// <summary>The context for an already-loaded profile: its locale override, else the default.</summary>
    public static AssistantToolContext For(Guid userId, AssistantProfile? profile, TimeZoneInfo timeZone) =>
        new(userId,
            string.IsNullOrWhiteSpace(profile?.LocaleOverride) ? AssistantToolContext.DefaultLocale : profile.LocaleOverride!,
            timeZone);
}
