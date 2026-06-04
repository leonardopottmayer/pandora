#nullable disable
using Pottmayer.Tars.Core.Ddd;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Abstractions.Repositories;

#nullable disable

namespace Pottmayer.Pandora.Modules.Identity.Tests.Fakes;

/// <summary>Minimal <see cref="IDataContext"/> backed by a type-keyed repository registry.</summary>
internal sealed class FakeDataContext : IDataContext
{
    private readonly Dictionary<Type, object> _repositories = [];

    public FakeDataContext Register<TRepository>(TRepository repository)
    {
        _repositories[typeof(TRepository)] = repository;
        return this;
    }

    public TRepository AcquireRepository<TRepository>() where TRepository : class, IRepository
        => (TRepository)_repositories[typeof(TRepository)];

    public IRepositoryResolver Resolver => throw new NotImplementedException();
    public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;
    public void CollectDomainEvents(IHasDomainEvents aggregate) { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
