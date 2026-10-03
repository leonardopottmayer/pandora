namespace Pottmayer.Pandora.Modules.Assistant.Abstractions.Files;

/// <summary>
/// A module's queue for files the user shares with the assistant bot (a bank receipt, a boleto): the file
/// is parked there, unassigned, and the user files it under one of the module's items later, in the app.
/// Nothing is interpreted — the caption picks the queue by one of its <see cref="Keywords"/>, and no file
/// is sent to the model.
/// </summary>
public interface IAssistantFileQueue
{
    /// <summary>The queue's name in the replies (e.g. <c>Finances</c>).</summary>
    string Name { get; }

    /// <summary>
    /// Caption words that send a file here, lowercase and without accents (<c>financeiro</c>,
    /// <c>comprovante</c>). The first few are shown as examples when a caption matches no queue.
    /// </summary>
    IReadOnlyList<string> Keywords { get; }

    /// <summary>Parks the file; <c>false</c> when the module refused it.</summary>
    Task<bool> EnqueueAsync(SharedFile file, CancellationToken ct = default);
}
