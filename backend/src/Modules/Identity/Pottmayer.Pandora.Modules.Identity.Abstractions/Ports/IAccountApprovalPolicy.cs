namespace Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;

/// <summary>
/// Who skips account approval and who is told about an account waiting for it. Identity owns the
/// question; the module that decides (Admin) answers it, so Identity never learns about roles.
/// </summary>
public interface IAccountApprovalPolicy
{
    /// <summary>Whether the account with this e-mail is approved without waiting.</summary>
    bool IsPreApproved(string email);

    /// <summary>The addresses told when an account starts waiting for approval.</summary>
    IReadOnlyList<string> ApproverEmails();
}
