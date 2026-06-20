namespace Pottmayer.Pandora.Modules.Finances.Infrastructure.Jobs;

/// <summary>
/// Helpers for the daily jobs that both ensure card statements exist. They run concurrently at
/// startup, so two transactions can pass their "does this statement exist?" check and then both
/// insert the same (card, reference_month) row — the loser hits the unique constraint. The work is
/// idempotent, so the caller simply re-runs once: on the retry the row is committed and gets skipped.
/// </summary>
internal static class JobConcurrency
{
    /// <summary>True when <paramref name="ex"/> (or an inner exception) is a Postgres unique-violation (23505).</summary>
    public static bool IsUniqueViolation(Exception ex)
    {
        // The infrastructure project doesn't reference Npgsql/EF directly, so match by shape rather
        // than type: a PostgresException carries a string SqlState of "23505" for a unique violation.
        for (Exception? e = ex; e is not null; e = e.InnerException)
        {
            if (e.GetType().Name == "PostgresException" &&
                e.GetType().GetProperty("SqlState")?.GetValue(e) as string == "23505")
                return true;
        }
        return false;
    }
}
