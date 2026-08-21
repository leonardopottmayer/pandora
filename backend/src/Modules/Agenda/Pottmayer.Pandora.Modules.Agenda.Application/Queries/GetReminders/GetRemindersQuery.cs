using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetReminders;

public sealed record GetRemindersInput(Guid UserId);

public sealed class GetRemindersQuery(GetRemindersInput input)
    : QueryBase<GetRemindersInput, IReadOnlyList<ReminderDto>>(input);
