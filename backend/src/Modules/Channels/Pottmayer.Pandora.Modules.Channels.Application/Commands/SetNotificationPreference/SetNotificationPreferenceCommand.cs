using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Channels.Application.Commands.SetNotificationPreference;

public sealed record SetNotificationPreferenceInput(Guid UserId, string Category, IReadOnlyList<string> Channels);

public sealed class SetNotificationPreferenceCommand(SetNotificationPreferenceInput input)
    : CommandBase<SetNotificationPreferenceInput, bool>(input);
