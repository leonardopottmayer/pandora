namespace Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;

/// <summary>
/// What an inbound update turned out to be, decided by the update's structure and never by what it
/// says.
/// </summary>
public enum InboundClassification
{
    /// <summary>An inline button press. Routed to the module that registered the button.</summary>
    Interaction = 0,

    /// <summary>A slash command this module handles itself. Never becomes an event.</summary>
    Command = 1,

    /// <summary>Free text or media, for whoever owns the conversation.</summary>
    Message = 2,

    /// <summary>Unknown chat, unknown command, or a kind the transport does not model.</summary>
    Discarded = 3,
}
