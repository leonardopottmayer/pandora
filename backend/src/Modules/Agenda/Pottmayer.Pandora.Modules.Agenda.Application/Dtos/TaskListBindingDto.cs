namespace Pottmayer.Pandora.Modules.Agenda.Application.Dtos;

/// <summary>A Pandora task list synced with a Google Tasks list, as the settings screen lists it.</summary>
public sealed record TaskListBindingDto(
    Guid Id,
    Guid ListId,
    string ListName,
    Guid ExternalAccountId,
    string RemoteListId,
    string Status,
    DateTimeOffset? LastSyncedAt,
    string? LastError);

/// <summary>A task list of the Google account, and the binding that syncs it, if any.</summary>
public sealed record GoogleTaskListDto(string Id, string Name, Guid? BindingId);
