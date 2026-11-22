using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Shared.Persistence.Storage;

/// <summary>Access to the blob table behind <see cref="DatabaseFileStorage"/>; the base <c>GetByIdAsync</c>/<c>AddAsync</c>/<c>RemoveAsync</c> are enough.</summary>
internal interface IFileBlobRepository : IStandardRepository<FileBlob, Guid>;
