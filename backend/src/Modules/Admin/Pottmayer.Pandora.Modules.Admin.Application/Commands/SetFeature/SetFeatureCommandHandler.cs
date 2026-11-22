using System.Text.Json;
using Pottmayer.Pandora.Modules.Admin.Application.Dtos;
using Pottmayer.Pandora.Modules.Admin.Application.Services;
using Pottmayer.Pandora.Modules.Admin.Abstractions;
using Pottmayer.Pandora.Modules.Admin.Domain.Entities;
using Pottmayer.Pandora.Modules.Admin.Domain.Errors;
using Pottmayer.Pandora.Modules.Admin.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.FeatureFlags.Abstractions;
using Pottmayer.Tars.Security.Authorization;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Admin.Application.Commands.SetFeature;

/// <summary>Returns every flag, since a parent switched off turns its children off too.</summary>
public sealed class SetFeatureCommandHandler(
    IUnitOfWorkFactory factory, IFeatureFlagCatalog catalog, IFeatureFlags flags, TimeProvider timeProvider)
    : CommandHandlerBase<SetFeatureCommand, IReadOnlyList<FeatureDto>>
{
    protected override async Task<Result<IReadOnlyList<FeatureDto>>> HandleAsync(SetFeatureCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if (catalog.Find(input.Key) is not { } definition)
            return Fail(AdminErrors.UnknownFeature);

        await factory.ExecuteAsync(AdminModule.DatabaseKey, async (ctx, token) =>
        {
            var overrides = ctx.AcquireRepository<IFeatureFlagOverrideRepository>();
            var current = await overrides.GetByIdAsync(input.Key, token);
            var before = current?.Enabled ?? definition.DefaultEnabled;

            if (input.Enabled == definition.DefaultEnabled)
            {
                if (current is not null)
                    await overrides.RemoveAsync(current, token);
            }
            else if (current is null)
            {
                await overrides.AddAsync(FeatureFlagOverride.Create(input.Key, input.Enabled, input.UserId, timeProvider), token);
            }
            else
            {
                current.Set(input.Enabled, input.UserId, timeProvider);
                await overrides.UpdateAsync(current, token);
            }

            await ctx.AcquireRepository<IAuditEntryRepository>().AddAsync(AuditEntry.Record(
                input.UserId, AuditEntry.FeatureChanged, AuditEntry.FeatureTarget, input.Key,
                JsonSerializer.Serialize(new { enabled = before }),
                JsonSerializer.Serialize(new { enabled = input.Enabled }), timeProvider), token);
            return true;
        }, cancellationToken: ct);

        flags.Invalidate();
        return Ok(FeatureDtos.From(await flags.GetAllAsync(ct)));
    }
}
