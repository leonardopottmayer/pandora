using Pottmayer.Pandora.Modules.Files.Domain.ReadModels;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Files.Application.Commands.DecideReview;

public sealed record ReviewFolder(Guid RootId, string Path);

/// <summary>A decision on entries, and on folders (everything waiting at or below each one). Returns how many it touched.</summary>
public sealed record DecideReviewInput(
    Guid UserId,
    ReviewDecision Decision,
    IReadOnlyList<Guid>? EntryIds,
    IReadOnlyList<ReviewFolder>? Folders);

public sealed class DecideReviewCommand(DecideReviewInput input)
    : CommandBase<DecideReviewInput, int>(input);
