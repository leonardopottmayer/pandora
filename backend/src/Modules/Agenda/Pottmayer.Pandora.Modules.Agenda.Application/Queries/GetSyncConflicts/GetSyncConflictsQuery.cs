using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetSyncConflicts;

public sealed record GetSyncConflictsInput(Guid UserId);

/// <summary>The user's latest Google sync conflicts, newest first.</summary>
[RequiresPermission(AgendaPermissions.Calendars.Read)]
public sealed class GetSyncConflictsQuery(GetSyncConflictsInput input)
    : QueryBase<GetSyncConflictsInput, IReadOnlyList<SyncConflictDto>>(input);
