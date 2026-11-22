namespace Pottmayer.Pandora.Modules.Agenda.Abstractions;

/// <summary>
/// Feature flag keys of the Agenda module. The module declares them in its <c>ApplicationDI</c>.
/// </summary>
public static class AgendaFeatures
{
    /// <summary>Gates the whole module.</summary>
    public const string Agenda = AgendaModule.Name;
}
