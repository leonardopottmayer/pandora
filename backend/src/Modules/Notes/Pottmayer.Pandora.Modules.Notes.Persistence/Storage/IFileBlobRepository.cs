using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Notes.Persistence.Storage;

/// <summary>Access to the blob table behind <c>DatabaseFileStorage</c>; the base <c>GetByIdAsync</c>/<c>AddAsync</c>/<c>RemoveAsync</c> are enough.</summary>
public interface IFileBlobRepository : IStandardRepository<FileBlob, Guid>;
