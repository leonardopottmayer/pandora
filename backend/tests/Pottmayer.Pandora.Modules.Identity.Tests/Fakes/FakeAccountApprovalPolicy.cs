using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;

namespace Pottmayer.Pandora.Modules.Identity.Tests.Fakes;

/// <summary>Pre-approves the given e-mails; they are also the approvers.</summary>
internal sealed class FakeAccountApprovalPolicy(params string[] preApproved) : IAccountApprovalPolicy
{
    public bool IsPreApproved(string email) => preApproved.Contains(email, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> ApproverEmails() => preApproved;
}
