using Microsoft.Extensions.Options;
using Pottmayer.Pandora.Modules.Admin.Abstractions;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;

namespace Pottmayer.Pandora.Modules.Admin.Application.Bootstrap;

/// <summary>
/// Makes sure every bootstrap e-mail that already has an activated account holds the <c>admin</c> role
/// and is not waiting for approval.
/// Run at startup; accounts activated later are covered by <c>AccountActivatedHandler</c>.
/// </summary>
public sealed class AdminBootstrapper(IUserDirectory users, RoleAssigner assigner, IOptions<AdminOptions> options)
{
    public async Task RunAsync(CancellationToken ct = default)
    {
        foreach (var email in BootstrapEmails.From(options.Value))
        {
            if (await users.FindActivatedUserIdByEmailAsync(email, ct) is { } userId)
            {
                await assigner.AssignAdminAsync(userId, ct);
                // A bootstrap e-mail added after its account started waiting: it never waits.
                await users.ApproveAsync(userId, ct);
            }
        }
    }
}
