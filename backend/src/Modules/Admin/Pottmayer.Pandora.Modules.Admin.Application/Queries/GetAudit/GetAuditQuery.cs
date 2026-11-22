using Pottmayer.Pandora.Modules.Admin.Application.Dtos;
using Pottmayer.Pandora.Modules.Admin.Abstractions;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Admin.Application.Queries.GetAudit;

public sealed record GetAuditInput(Guid UserId, Guid? ActorId, string? TargetType, string? TargetId, DateTimeOffset? Before, int Limit);

/// <summary>The changes, newest first, filtered by actor and target; <c>Before</c> pages back in time.</summary>
[RequiresPermission(AdminPermissions.Audit.Read)]
public sealed class GetAuditQuery(GetAuditInput input)
    : QueryBase<GetAuditInput, IReadOnlyList<AuditEntryDto>>(input);
