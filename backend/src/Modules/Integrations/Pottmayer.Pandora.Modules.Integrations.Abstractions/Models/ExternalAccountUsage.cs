namespace Pottmayer.Pandora.Modules.Integrations.Abstractions.Models;

/// <summary>
/// One place an account is used. <see cref="Module"/> and <see cref="Purpose"/> are stable keys the
/// screen translates (<c>assistant</c> / <c>chat</c>); <see cref="Detail"/> is optional user data shown
/// as-is (a mailbox address, a calendar name).
/// </summary>
public sealed record ExternalAccountUsage(
    Guid ExternalAccountId,
    string Module,
    string Purpose,
    string? Detail = null);
