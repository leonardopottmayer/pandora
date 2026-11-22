using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.DeleteTag;

public sealed record DeleteTagInput(Guid UserId, Guid TagId);

/// <summary>
/// Permanently removes a tag. Every link to it is cascade-deleted at the database level — the
/// audit event records what was severed before that happens.
/// </summary>
[RequiresPermission(FinancesPermissions.Tags.Delete)]
public sealed class DeleteTagCommand(DeleteTagInput input)
    : CommandBase<DeleteTagInput, bool>(input);
