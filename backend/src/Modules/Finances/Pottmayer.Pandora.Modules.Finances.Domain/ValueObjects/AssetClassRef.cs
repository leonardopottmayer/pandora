namespace Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

/// <summary>
/// Which class an asset belongs to: a system class (fin020) or one of the user's own (fin025). The ids
/// are uuids, so one id names one class whichever table it lives in.
/// </summary>
public readonly record struct AssetClassRef(Guid Id, bool IsSystem);
