using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetReminders;

public sealed record GetRemindersInput(Guid UserId);

[RequiresPermission(AgendaPermissions.Reminders.Read)]
public sealed class GetRemindersQuery(GetRemindersInput input)
    : QueryBase<GetRemindersInput, IReadOnlyList<ReminderDto>>(input);
