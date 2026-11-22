using Pottmayer.Pandora.Modules.Files.Abstractions;
using Pottmayer.Pandora.Modules.Files.Agent;
using Pottmayer.Pandora.Modules.Files.Application.Catalog;
using Pottmayer.Pandora.Modules.Files.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Files.Application.Queries.GetAgentConfig;

/// <summary>
/// What the calling device scans: its active roots, each with its marks and the enabled filters that reach
/// it. While the account switch is off, no roots — the agent has nothing to do.
/// </summary>
public sealed class GetAgentConfigQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetAgentConfigQuery, AgentConfig>
{
    protected override async Task<Result<AgentConfig>> HandleAsync(GetAgentConfigQuery request, CancellationToken cancellationToken)
    {
        var input = request.Input;

        var config = await factory.ExecuteAsync(FilesModule.DatabaseKey, async (ctx, ct) =>
        {
            var preferences = await ctx.AcquireRepository<IPreferencesRepository>().GetByIdAsync(input.UserId, ct);
            if (preferences is not { IsEnabled: true }) return new AgentConfig(false, []);

            var roots = await ctx.AcquireRepository<IRootRepository>().ListActiveByDeviceAsync(input.DeviceId, ct);
            var configs = new List<RootConfig>();
            foreach (var root in roots.Where(r => r.UserId == input.UserId))
                configs.Add(await RootRules.LoadConfigAsync(ctx, root, ct));

            return new AgentConfig(true, configs);
        }, cancellationToken: cancellationToken);

        return Ok(config);
    }
}
