namespace Pottmayer.Pandora.Modules.Channels.Application.Dtos;

/// <summary>One of the user's addresses, for the settings screen.</summary>
public sealed record UserChannelDto(
    string Channel,
    string Address,
    bool IsVerified,
    bool IsEnabled,
    string? DisabledReason,
    DateTimeOffset? VerifiedAt);

/// <summary>The deep link the user taps to finish linking, and how long it is good for.</summary>
public sealed record ChannelLinkDto(string Url, DateTimeOffset ExpiresAt);

/// <summary>A user's channel choice for one category. Empty channels means the category is muted.</summary>
public sealed record NotificationPreferenceDto(string Category, IReadOnlyList<string> Channels);

/// <summary>One row of the delivery history: what went out, on which channel, and how it ended.</summary>
public sealed record NotificationHistoryDto(
    Guid Id,
    string Channel,
    string? Category,
    string TemplateKey,
    string Subject,
    string Status,
    int AttemptCount,
    string? LastError,
    string? Provider,
    Guid CorrelationId,
    Guid? GroupId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
