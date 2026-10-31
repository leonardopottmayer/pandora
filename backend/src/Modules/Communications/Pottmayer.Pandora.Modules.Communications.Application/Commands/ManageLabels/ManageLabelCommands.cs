using Pottmayer.Pandora.Modules.Communications.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Communications.Application.Commands.ManageLabels;

public sealed record CreateLabelInput(Guid UserId, Guid MailboxId, string? Name);

public sealed record RenameLabelInput(Guid UserId, Guid LabelId, string? Name);

public sealed record DeleteLabelInput(Guid UserId, Guid LabelId);

/// <summary>Returns the mailbox's labels as they are now.</summary>
public sealed class CreateLabelCommand(CreateLabelInput input) : CommandBase<CreateLabelInput, IReadOnlyList<LabelDto>>(input);

/// <inheritdoc cref="CreateLabelCommand"/>
public sealed class RenameLabelCommand(RenameLabelInput input) : CommandBase<RenameLabelInput, IReadOnlyList<LabelDto>>(input);

/// <inheritdoc cref="CreateLabelCommand"/>
public sealed class DeleteLabelCommand(DeleteLabelInput input) : CommandBase<DeleteLabelInput, IReadOnlyList<LabelDto>>(input);
