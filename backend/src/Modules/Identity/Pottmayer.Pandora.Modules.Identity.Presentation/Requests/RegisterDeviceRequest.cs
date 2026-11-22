namespace Pottmayer.Pandora.Modules.Identity.Presentation.Requests;

public sealed record RegisterDeviceRequest(string? Name, string? Platform, string? Form, IReadOnlyList<string>? Scopes);
