using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Files.Application.Commands.ExpireScans;

/// <param name="Cutoff">Running scans with no batch since then are aborted.</param>
public sealed record ExpireScansInput(DateTimeOffset Cutoff);

public sealed class ExpireScansCommand(ExpireScansInput input)
    : CommandBase<ExpireScansInput, int>(input);
