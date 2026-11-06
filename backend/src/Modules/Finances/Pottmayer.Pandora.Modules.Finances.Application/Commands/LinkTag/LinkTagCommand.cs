using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.LinkTag;

public sealed record LinkTagInput(Guid UserId, Guid TagId, string EntityType, Guid EntityId);

/// <summary>
/// Attaches a tag to an entity (transaction, plan, etc.). Idempotent: linking an already-linked
/// pair returns the existing link instead of erroring or recording a duplicate event.
/// </summary>
[RequiresPermission(FinancesPermissions.Tags.Assign)]
public sealed class LinkTagCommand(LinkTagInput input)
    : CommandBase<LinkTagInput, TagLinkDto>(input);
