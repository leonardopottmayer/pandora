namespace Pottmayer.Pandora.Modules.Finances.Abstractions;

/// <summary>
/// Feature flag keys of the Finances module. The module declares them in its <c>ApplicationDI</c>.
/// </summary>
public static class FinancesFeatures
{
    /// <summary>Gates the whole module.</summary>
    public const string Finances = FinancesModule.Name;

    /// <summary>Gates statement imports; a child of <see cref="Finances"/>.</summary>
    public const string Import = Finances + ".import";
}
