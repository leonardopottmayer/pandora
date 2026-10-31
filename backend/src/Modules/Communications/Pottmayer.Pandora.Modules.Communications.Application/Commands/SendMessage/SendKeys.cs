using System.Collections.Concurrent;

namespace Pottmayer.Pandora.Modules.Communications.Application.Commands.SendMessage;

/// <summary>
/// The send keys handled recently: what stops a double click, a second tab or a retry after a timeout from sending twice.
/// </summary>
/// <remarks>
/// ponytail: in process memory for an hour, so a restart forgets them, and a retry right after one can send twice. A
/// table with a unique key if that ever matters, or if the host scales out.
/// </remarks>
public sealed class SendKeys(TimeProvider timeProvider)
{
    private static readonly TimeSpan Keep = TimeSpan.FromHours(1);

    private readonly ConcurrentDictionary<(Guid UserId, Guid Key), DateTimeOffset> _keys = new();

    /// <summary>False when this key was already taken: the caller must not send.</summary>
    public bool TryReserve(Guid userId, Guid key)
    {
        var now = timeProvider.GetUtcNow();
        foreach (var old in _keys.Where(k => now - k.Value > Keep))
            _keys.TryRemove(old.Key, out _);

        return _keys.TryAdd((userId, key), now);
    }

    /// <summary>Only when the provider certainly refused: the same form can then be sent again.</summary>
    public void Release(Guid userId, Guid key) => _keys.TryRemove((userId, key), out _);
}
