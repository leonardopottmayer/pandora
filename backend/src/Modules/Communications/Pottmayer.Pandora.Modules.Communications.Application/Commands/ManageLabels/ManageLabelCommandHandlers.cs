using Pottmayer.Pandora.Modules.Communications.Application.Dtos;
using Pottmayer.Pandora.Modules.Communications.Domain.Errors;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Communications.Application.Commands.ManageLabels;

public sealed class CreateLabelCommandHandler(LabelOperation operation)
    : CommandHandlerBase<CreateLabelCommand, IReadOnlyList<LabelDto>>
{
    protected override async Task<Result<IReadOnlyList<LabelDto>>> HandleAsync(CreateLabelCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if (LabelOperation.ValidName(input.Name) is not { } name)
            return Fail(MailErrors.LabelNameInvalid);

        var mailbox = await operation.FindMailboxAsync(input.UserId, input.MailboxId, ct);
        if (mailbox is null)
            return Fail(MailErrors.MailboxNotFound);

        return await operation.RunAsync(input.UserId, mailbox, p => p.CreateLabelAsync(mailbox, name, ct), ct);
    }
}

public sealed class RenameLabelCommandHandler(LabelOperation operation)
    : CommandHandlerBase<RenameLabelCommand, IReadOnlyList<LabelDto>>
{
    protected override async Task<Result<IReadOnlyList<LabelDto>>> HandleAsync(RenameLabelCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if (LabelOperation.ValidName(input.Name) is not { } name)
            return Fail(MailErrors.LabelNameInvalid);

        var found = await operation.FindUserLabelAsync(input.UserId, input.LabelId, ct);
        if (found.IsFailure)
            return Result<IReadOnlyList<LabelDto>>.Failure(found.Errors);

        var (mailbox, label) = found.Value;
        return await operation.RunAsync(input.UserId, mailbox, p => p.RenameLabelAsync(mailbox, label.ProviderLabelId, name, ct), ct);
    }
}

public sealed class DeleteLabelCommandHandler(LabelOperation operation)
    : CommandHandlerBase<DeleteLabelCommand, IReadOnlyList<LabelDto>>
{
    protected override async Task<Result<IReadOnlyList<LabelDto>>> HandleAsync(DeleteLabelCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var found = await operation.FindUserLabelAsync(input.UserId, input.LabelId, ct);
        if (found.IsFailure)
            return Result<IReadOnlyList<LabelDto>>.Failure(found.Errors);

        var (mailbox, label) = found.Value;
        return await operation.RunAsync(input.UserId, mailbox, p => p.DeleteLabelAsync(mailbox, label.ProviderLabelId, ct), ct);
    }
}
