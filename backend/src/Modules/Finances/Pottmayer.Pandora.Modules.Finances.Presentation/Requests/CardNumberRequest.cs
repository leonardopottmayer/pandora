namespace Pottmayer.Pandora.Modules.Finances.Presentation.Requests;

/// <summary>Body for adding or editing a card number.</summary>
public sealed record CardNumberRequest(string Kind, string LastFour, string? Nickname);
