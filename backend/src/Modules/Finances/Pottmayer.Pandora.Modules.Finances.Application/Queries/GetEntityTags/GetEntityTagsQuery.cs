using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetEntityTags;

public sealed record GetEntityTagsInput(Guid UserId, string EntityType, Guid EntityId);

/// <summary>The ids of the tags on one entity (account, card, statement, transaction).</summary>
[RequiresPermission(FinancesPermissions.Tags.Read)]
public sealed class GetEntityTagsQuery(GetEntityTagsInput input)
    : QueryBase<GetEntityTagsInput, IReadOnlyList<Guid>>(input);
