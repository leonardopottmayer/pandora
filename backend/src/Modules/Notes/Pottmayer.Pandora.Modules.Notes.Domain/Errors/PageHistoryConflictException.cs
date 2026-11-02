namespace Pottmayer.Pandora.Modules.Notes.Domain.Errors;

public sealed class PageHistoryConflictException() : Exception("The page changed. Reload it before saving or restoring.");
