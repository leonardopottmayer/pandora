using System.Runtime.CompilerServices;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Finances.Tests.Fakes;

/// <summary>
/// Builds an <see cref="ImportLayout"/> for parser tests. The aggregate is intentionally read-only
/// (private ctor, private setters) and has no factory, so tests construct it via reflection and set
/// only the fields the parsers read: <c>FileFormat</c> and <c>Config</c>.
/// </summary>
internal static class ImportLayoutFactory
{
    public static ImportLayout Csv(string config, string layoutCode = "test-layout") => Build("csv", config, layoutCode);
    public static ImportLayout Ofx(string config, string layoutCode = "test-layout") => Build("ofx", config, layoutCode);

    /// <summary>A bare layout carrying only its code — for detector tests that match by <c>LayoutCode</c>.</summary>
    public static ImportLayout WithCode(string layoutCode, string fileFormat = "ofx") => Build(fileFormat, "{}", layoutCode);

    private static ImportLayout Build(string fileFormat, string config, string layoutCode)
    {
        var layout = (ImportLayout)RuntimeHelpers.GetUninitializedObject(typeof(ImportLayout));
        Set(layout, nameof(ImportLayout.FileFormat), LayoutFileFormat.FromValue(fileFormat));
        Set(layout, nameof(ImportLayout.Config), config);
        Set(layout, nameof(ImportLayout.LayoutCode), layoutCode);
        return layout;
    }

    private static void Set(ImportLayout layout, string property, object value) =>
        typeof(ImportLayout).GetProperty(property)!.SetValue(layout, value);
}
