using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.RetryImportFile;

public sealed record RetryImportFileInput(Guid UserId, Guid ImportFileId);

/// <summary>Resets a failed import file back to received so it can be parsed again.</summary>
[RequiresPermission(FinancesPermissions.Imports.Run)]
public sealed class RetryImportFileCommand(RetryImportFileInput input)
    : CommandBase<RetryImportFileInput, bool>(input);
