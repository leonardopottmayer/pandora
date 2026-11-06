using Pottmayer.Pandora.Modules.Files.Agent;
using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Files.Abstractions;

namespace Pottmayer.Pandora.Modules.Files.Application.Commands.SaveSelection;

/// <summary>Replaces the root's selection marks as a set. An empty set catalogs the whole root.</summary>
public sealed record SaveSelectionInput(Guid UserId, Guid RootId, IReadOnlyList<MarkConfig>? Marks);

[RequiresPermission(FilesPermissions.Review.Update)]
public sealed class SaveSelectionCommand(SaveSelectionInput input)
    : CommandBase<SaveSelectionInput, RootDto>(input);
