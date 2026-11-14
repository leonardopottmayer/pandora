using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetGoogleTaskLists;

public sealed record GetGoogleTaskListsInput(Guid UserId, Guid AccountId);

/// <summary>The task lists of a connected Google account, marking those already bound.</summary>
[RequiresPermission(AgendaPermissions.Lists.Read)]
public sealed class GetGoogleTaskListsQuery(GetGoogleTaskListsInput input)
    : QueryBase<GetGoogleTaskListsInput, IReadOnlyList<GoogleTaskListDto>>(input);
