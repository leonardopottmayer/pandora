using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.RetryImportFile;

public sealed record RetryImportFileInput(Guid UserId, Guid ImportFileId);

public sealed class RetryImportFileCommand(RetryImportFileInput input)
    : CommandBase<RetryImportFileInput, bool>(input);
