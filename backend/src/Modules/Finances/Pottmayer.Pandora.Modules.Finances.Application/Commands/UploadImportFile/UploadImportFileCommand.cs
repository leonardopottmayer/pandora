using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.UploadImportFile;

public sealed record UploadImportFileInput(
    Guid UserId,
    Guid? AccountId,
    Guid? CardId,
    string FileName,
    byte[] FileContent);

public sealed class UploadImportFileCommand(UploadImportFileInput input)
    : CommandBase<UploadImportFileInput, ImportFileDto>(input);
