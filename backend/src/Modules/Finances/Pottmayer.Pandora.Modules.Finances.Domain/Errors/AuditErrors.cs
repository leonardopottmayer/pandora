using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Errors;

public static class AuditErrors
{
    public static Error MissingFilter =>
        Error.Validation(
            "Audit.MissingFilter",
            "Provide either entityType and entityId, or correlationId.");
}
