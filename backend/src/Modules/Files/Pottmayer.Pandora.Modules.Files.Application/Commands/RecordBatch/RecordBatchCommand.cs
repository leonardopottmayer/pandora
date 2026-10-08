using Pottmayer.Pandora.Modules.Files.Agent;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Files.Application.Commands.RecordBatch;

public sealed record RecordBatchInput(Guid UserId, Guid DeviceId, Guid ScanId, IReadOnlyList<ScannedEntry>? Entries);

public sealed class RecordBatchCommand(RecordBatchInput input)
    : CommandBase<RecordBatchInput, ScanBatchResult>(input);
