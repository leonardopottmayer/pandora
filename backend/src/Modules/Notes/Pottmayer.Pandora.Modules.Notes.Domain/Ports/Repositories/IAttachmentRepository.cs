using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;

/// <summary>
/// Attachments are looked up only by their own id (the download URL). They are not scoped per user in
/// the MVP â€” this is a single-user personal system and the module has one owner (nte001).
/// </summary>
public interface IAttachmentRepository : IStandardRepository<Attachment, Guid>;
