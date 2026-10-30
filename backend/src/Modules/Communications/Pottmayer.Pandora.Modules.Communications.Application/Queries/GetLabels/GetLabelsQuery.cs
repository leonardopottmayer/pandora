using Pottmayer.Pandora.Modules.Communications.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Communications.Application.Queries.GetLabels;

public sealed record GetLabelsInput(Guid UserId, Guid MailboxId);

/// <summary>System labels first, then the user's, by name.</summary>
public sealed class GetLabelsQuery(GetLabelsInput input)
    : QueryBase<GetLabelsInput, IReadOnlyList<LabelDto>>(input);
