using Pottmayer.Pandora.Modules.Communications.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Communications.Application.Queries.GetMailboxes;

public sealed record GetMailboxesInput(Guid UserId);

public sealed class GetMailboxesQuery(GetMailboxesInput input)
    : QueryBase<GetMailboxesInput, IReadOnlyList<MailboxDto>>(input);
