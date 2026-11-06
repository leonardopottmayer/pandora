using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetAuditTimeline;

public sealed record GetAuditTimelineInput(
    Guid UserId,
    string? EntityType,
    Guid? EntityId,
    Guid? CorrelationId,
    int Skip,
    int Take);

[RequiresPermission(FinancesPermissions.Audit.Read)]
public sealed class GetAuditTimelineQuery(GetAuditTimelineInput input)
    : QueryBase<GetAuditTimelineInput, IReadOnlyList<AuditEventDto>>(input);
