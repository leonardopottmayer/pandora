using Microsoft.Extensions.Options;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;
using Pottmayer.Pandora.Modules.Identity.Application.Options;

namespace Pottmayer.Pandora.Modules.Identity.Application.Services;

/// <summary>Whether an account must wait for approval: the option is on and the policy does not pre-approve it.</summary>
public sealed class AccountApprovalRule(IOptions<AccountApprovalOptions> options, IAccountApprovalPolicy? policy = null)
{
    public bool Applies(string email) => options.Value.Required && policy?.IsPreApproved(email) != true;

    public IReadOnlyList<string> ApproverEmails() => policy?.ApproverEmails() ?? [];
}
