using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;

namespace Pottmayer.Pandora.Modules.Admin.Application.Services;

/// <summary>
/// Keeps one way into the panel: some activated, enabled account must keep the <c>admin</c> role.
/// The bootstrap e-mails would restore it at the next start, but only after a restart.
/// </summary>
public sealed class LastAdminGuard(IUserDirectory directory)
{
    /// <summary>Whether an active admin is left once <paramref name="losing"/> lose the role or are disabled.</summary>
    public async Task<bool> LeavesAnAdminAsync(IEnumerable<Guid> adminHolders, IEnumerable<Guid> losing, CancellationToken ct)
    {
        var remaining = adminHolders.Except(losing).ToHashSet();
        if (remaining.Count == 0)
            return false;

        var users = await directory.ListAsync(ct);
        return users.Any(u => remaining.Contains(u.Id) && u.Activated && !u.Disabled);
    }
}
