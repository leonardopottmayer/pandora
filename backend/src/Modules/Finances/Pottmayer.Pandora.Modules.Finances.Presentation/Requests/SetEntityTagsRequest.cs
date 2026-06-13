namespace Pottmayer.Pandora.Modules.Finances.Presentation.Requests;

public sealed record SetEntityTagsRequest(IReadOnlyList<Guid> TagIds);
