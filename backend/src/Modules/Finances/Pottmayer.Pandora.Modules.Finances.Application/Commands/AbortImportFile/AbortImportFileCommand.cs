using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.AbortImportFile;

public sealed record AbortImportFileInput(Guid UserId, Guid ImportFileId);

/// <summary>Cancels an import file for good. Fails if the file already reached a terminal state.</summary>
public sealed class AbortImportFileCommand(AbortImportFileInput input)
    : CommandBase<AbortImportFileInput, bool>(input);
