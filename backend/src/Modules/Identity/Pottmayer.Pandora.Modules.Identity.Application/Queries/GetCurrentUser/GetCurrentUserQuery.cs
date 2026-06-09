using Pottmayer.Pandora.Modules.Identity.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Identity.Application.Queries.GetCurrentUser;

public sealed record GetCurrentUserInput(Guid UserId);

public sealed class GetCurrentUserQuery(GetCurrentUserInput input)
    : QueryBase<GetCurrentUserInput, CurrentUserDto>(input);
