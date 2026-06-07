using Pottmayer.Pandora.Modules.Identity.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Identity.Application.Commands.Mfa.Challenge;

public sealed record CompleteMfaChallengeInput(string Ticket, string Code);

public sealed class CompleteMfaChallengeCommand(CompleteMfaChallengeInput input)
    : CommandBase<CompleteMfaChallengeInput, TokenDto>(input);
