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
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Admin.Application.Queries.GetAudit;

public sealed class GetAuditQueryHandler(IUnitOfWorkFactory factory, IUserDirectory directory)
    : QueryHandlerBase<GetAuditQuery, IReadOnlyList<AuditEntryDto>>
{
    private const int DefaultLimit = 100;
    private const int MaxLimit = 200;

    protected override async Task<Result<IReadOnlyList<AuditEntryDto>>> HandleAsync(GetAuditQuery request, CancellationToken ct)
    {
        var input = request.Input;
        var limit = input.Limit is > 0 and <= MaxLimit ? input.Limit : DefaultLimit;

        var (entries, roles) = await factory.ExecuteAsync(AdminModule.DatabaseKey, async (ctx, token) =>
        {
            var found = await ctx.AcquireRepository<IAuditEntryRepository>()
                .ListAsync(input.ActorId, input.TargetType, input.TargetId, input.Before, limit, token);
            var all = await ctx.AcquireRepository<IRoleRepository>().GetAllAsync(token);
            return (found, all.ToDictionary(r => r.Id.ToString(), r => r.Name));
        }, cancellationToken: ct);

        var users = (await directory.ListAsync(ct)).ToDictionary(u => u.Id, u => u.Name);
        string? TargetName(AuditEntry e) => e.TargetType switch
        {
            AuditEntry.UserTarget when Guid.TryParse(e.TargetId, out var id) => users.GetValueOrDefault(id),
            AuditEntry.RoleTarget => roles.GetValueOrDefault(e.TargetId),
            _ => null,
        };

        return Ok(entries.Select(e => new AuditEntryDto(
            e.Id, e.ActorId, e.ActorId is { } actor ? users.GetValueOrDefault(actor) : null,
            e.Action, e.TargetType, e.TargetId, TargetName(e), e.Before, e.After, e.OccurredAt)).ToList());
    }
}
