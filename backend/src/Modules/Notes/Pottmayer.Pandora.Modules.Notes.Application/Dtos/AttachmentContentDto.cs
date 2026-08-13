namespace Pottmayer.Pandora.Modules.Notes.Application.Dtos;

/// <summary>The bytes and headers needed to serve an attachment download.</summary>
public sealed record AttachmentContentDto(string FileName, string ContentType, byte[] Content);
