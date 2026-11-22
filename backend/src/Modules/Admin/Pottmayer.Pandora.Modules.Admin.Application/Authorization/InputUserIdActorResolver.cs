using System.Collections.Concurrent;
using System.Reflection;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Admin.Application.Authorization;

/// <summary>
/// Whose request it is, for every module: every user command and query carries <c>Input.UserId</c>,
/// filled from the token by the controller, from the linked account by the bot, and from the context by
/// the Assistant. Requests without it resolve to no subject, which the behavior denies when they are
/// marked.
/// </summary>
public sealed class InputUserIdActorResolver : IAuthorizationActorResolver
{
    private static readonly ConcurrentDictionary<Type, Func<object, Guid?>?> Readers = new();

    public string? Resolve(object request)
    {
        var reader = Readers.GetOrAdd(request.GetType(), BuildReader);
        return reader?.Invoke(request)?.ToString();
    }

    private static Func<object, Guid?>? BuildReader(Type type)
    {
        var input = type.GetProperty("Input", BindingFlags.Public | BindingFlags.Instance);
        var userId = input?.PropertyType.GetProperty("UserId", BindingFlags.Public | BindingFlags.Instance);
        if (input is null || userId is null || userId.PropertyType != typeof(Guid))
            return null;

        return request => input.GetValue(request) is { } value ? (Guid)userId.GetValue(value)! : null;
    }
}
