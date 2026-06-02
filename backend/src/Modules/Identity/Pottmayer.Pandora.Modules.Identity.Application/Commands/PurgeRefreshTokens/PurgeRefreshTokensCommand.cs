using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Identity.Application.Commands.PurgeRefreshTokens;

public sealed record PurgeRefreshTokensInput(
    DateTimeOffset ConsumedOlderThan,
    DateTimeOffset ExpiredOlderThan);

public sealed class PurgeRefreshTokensCommand(PurgeRefreshTokensInput input)
    : CommandBase<PurgeRefreshTokensInput, int>(input);
