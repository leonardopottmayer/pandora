namespace Pottmayer.Pandora.Modules.Agenda.Application.Tasks;

/// <summary>
/// Encodes what an inline button carries back for a task alert: a <c>task|{guid}</c> pair. The
/// <c>task</c> discriminator keeps it disjoint from a reminder's payload (a bare guid, or
/// <c>guid|ticks</c>), so the reminder handler ignores a task button and this handler ignores a
/// reminder button. The value is opaque to Channels, which stores and returns it verbatim.
/// </summary>
public static class TaskButtonPayload
{
    private const string Prefix = "task|";

    public static string For(Guid taskId) => $"{Prefix}{taskId}";

    /// <summary>Parses a task button payload. Returns false for anything that is not a <c>task|{guid}</c> pair.</summary>
    public static bool TryParse(string? payload, out Guid taskId)
    {
        taskId = Guid.Empty;
        return payload is not null
            && payload.StartsWith(Prefix, StringComparison.Ordinal)
            && Guid.TryParse(payload.AsSpan(Prefix.Length), out taskId);
    }
}
