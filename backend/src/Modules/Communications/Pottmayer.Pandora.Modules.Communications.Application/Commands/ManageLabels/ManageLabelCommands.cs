using Pottmayer.Pandora.Modules.Communications.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Abstractions;

namespace Pottmayer.Pandora.Modules.Communications.Application.Commands.ManageLabels;

public sealed record CreateLabelInput(Guid UserId, Guid MailboxId, string? Name);

public sealed record RenameLabelInput(Guid UserId, Guid LabelId, string? Name);

public sealed record DeleteLabelInput(Guid UserId, Guid LabelId);

/// <summary>Returns the mailbox's labels as they are now.</summary>
[RequiresPermission(CommunicationsPermissions.Labels.Update)]
public sealed class CreateLabelCommand(CreateLabelInput input) : CommandBase<CreateLabelInput, IReadOnlyList<LabelDto>>(input);

/// <inheritdoc cref="CreateLabelCommand"/>
[RequiresPermission(CommunicationsPermissions.Labels.Update)]
public sealed class RenameLabelCommand(RenameLabelInput input) : CommandBase<RenameLabelInput, IReadOnlyList<LabelDto>>(input);

/// <inheritdoc cref="CreateLabelCommand"/>
[RequiresPermission(CommunicationsPermissions.Labels.Update)]
public sealed class DeleteLabelCommand(DeleteLabelInput input) : CommandBase<DeleteLabelInput, IReadOnlyList<LabelDto>>(input);
