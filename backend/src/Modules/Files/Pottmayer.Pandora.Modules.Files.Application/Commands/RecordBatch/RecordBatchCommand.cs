using Pottmayer.Pandora.Modules.Files.Agent;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Files.Abstractions;

namespace Pottmayer.Pandora.Modules.Files.Application.Commands.RecordBatch;

public sealed record RecordBatchInput(Guid UserId, Guid DeviceId, Guid ScanId, IReadOnlyList<ScannedEntry>? Entries);

[RequiresPermission(FilesPermissions.Scans.Run)]
public sealed class RecordBatchCommand(RecordBatchInput input)
    : CommandBase<RecordBatchInput, ScanBatchResult>(input);
