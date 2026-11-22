namespace Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;

/// <summary>The lifecycle of an <see cref="Aggregates.Event"/> (doc agd002).</summary>
public enum EventStatus
{
    Confirmed,
    Tentative,
    Cancelled,
}
