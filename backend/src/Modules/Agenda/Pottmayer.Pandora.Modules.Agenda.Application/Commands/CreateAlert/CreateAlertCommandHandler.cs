using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Application.Errors;
using Pottmayer.Pandora.Modules.Agenda.Application.Mapping;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateAlert;

public sealed class CreateAlertCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<CreateAlertCommand, AlertDto>
{
    protected override async Task<Result<AlertDto>> HandleAsync(CreateAlertCommand request, CancellationToken ct)
    {
        var input = request.Input;

        // Only tasks carry alerts in this version.
        if (!string.Equals(input.SubjectType, "task", StringComparison.OrdinalIgnoreCase))
            return Fail(AlertErrors.UnsupportedSubjectType);

        var result = await factory.ExecuteAsync(AgendaModule.Name, async (context, token) =>
        {
            var tasks = context.AcquireRepository<ITaskRepository>();
            var alerts = context.AcquireRepository<IAlertRepository>();

            var task = await tasks.FindAsync(input.UserId, input.SubjectId, token);
            if (task is null)
                return Result<AlertDto>.Failure([AlertErrors.SubjectNotFound]);
            if (task.DueAt is null)
                return Result<AlertDto>.Failure([AlertErrors.SubjectHasNoDueDate]);

            var alert = Alert.Create(
                input.UserId, AlertSubjectType.Task, input.SubjectId, input.OffsetMinutes, input.Channels, timeProvider);
            await alerts.AddAsync(alert, token);
            return Result<AlertDto>.Success(alert.ToDto());
        }, cancellationToken: ct);

        return result;
    }
}
