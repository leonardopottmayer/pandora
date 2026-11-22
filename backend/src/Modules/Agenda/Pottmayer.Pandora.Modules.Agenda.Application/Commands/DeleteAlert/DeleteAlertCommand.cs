using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.DeleteAlert;

public sealed record DeleteAlertInput(Guid UserId, Guid AlertId);

/// <summary>Removes an alert. Its dispatch ledger rows go with it (ON DELETE CASCADE).</summary>
[RequiresPermission(AgendaPermissions.Alerts.Delete)]
public sealed class DeleteAlertCommand(DeleteAlertInput input)
    : CommandBase<DeleteAlertInput, bool>(input);
