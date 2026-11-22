using Microsoft.Extensions.Options;
using Pottmayer.Pandora.Modules.Admin.Abstractions;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;

namespace Pottmayer.Pandora.Modules.Admin.Application.Bootstrap;

/// <summary>
/// Identity's approval question, answered from the bootstrap e-mails: they never wait, and they are
/// the ones told when someone else does.
/// </summary>
public sealed class BootstrapApprovalPolicy(IOptions<AdminOptions> options) : IAccountApprovalPolicy
{
    public bool IsPreApproved(string email) => BootstrapEmails.Contains(options.Value, email);

    public IReadOnlyList<string> ApproverEmails() => [.. BootstrapEmails.From(options.Value)];
}
