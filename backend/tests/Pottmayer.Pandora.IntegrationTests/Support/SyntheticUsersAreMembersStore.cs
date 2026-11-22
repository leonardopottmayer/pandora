using Npgsql;
using Pottmayer.Pandora.Modules.Admin.Application.Authorization;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.IntegrationTests.Support;

/// <summary>
/// Many tests dispatch use cases for a made-up user id (or seed users straight in SQL), so no account was
/// ever activated and no default role given. In production every account holds <c>member</c>; here an
/// id with no account gets the member role's patterns. Accounts created through sign-up keep their real
/// grants from the admin tables, so the authorization tests still see 403s.
/// </summary>
internal sealed class SyntheticUsersAreMembersStore(AuthorizationStore real, string connectionString) : IAuthorizationStore
{
    public async Task<AuthorizationGrants> GetGrantsAsync(string subjectId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(subjectId, out var userId) || await AccountExistsAsync(userId, cancellationToken))
            return await real.GetGrantsAsync(subjectId, cancellationToken);

        return new AuthorizationGrants(await MemberPatternsAsync(cancellationToken), [], []);
    }

    private async Task<bool> AccountExistsAsync(Guid userId, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM identity.idt001_user WHERE id = $1)", conn);
        cmd.Parameters.AddWithValue(userId);
        return (bool)(await cmd.ExecuteScalarAsync(ct))!;
    }

    private async Task<IReadOnlyList<string>> MemberPatternsAsync(CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT p.pattern FROM admin.adm003_role_permission p JOIN admin.adm002_role r ON r.id = p.role_id WHERE r.is_default", conn);
        var patterns = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            patterns.Add(reader.GetString(0));
        return patterns;
    }
}
