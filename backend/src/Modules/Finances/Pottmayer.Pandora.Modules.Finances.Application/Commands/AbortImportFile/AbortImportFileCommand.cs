using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.AbortImportFile;

public sealed record AbortImportFileInput(Guid UserId, Guid ImportFileId);

public sealed class AbortImportFileCommand(AbortImportFileInput input)
    : CommandBase<AbortImportFileInput, bool>(input);
