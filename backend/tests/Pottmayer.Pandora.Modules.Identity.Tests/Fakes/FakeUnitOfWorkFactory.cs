#nullable disable
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Identity.Tests.Fakes;

/// <summary>
/// Executes the supplied work against a fixed <see cref="FakeDataContext"/>,
/// bypassing any real transaction handling.
/// </summary>
internal sealed class FakeUnitOfWorkFactory(FakeDataContext context) : IUnitOfWorkFactory
{
    public Task<T> ExecuteAsync<T>(string key, Func<IDataContext, CancellationToken, Task<T>> work, UnitOfWorkOptions options = null, CancellationToken ct = default)
        => work(context, ct);

    public Task ExecuteAsync(string key, Func<IDataContext, CancellationToken, Task> work, UnitOfWorkOptions options = null, CancellationToken ct = default)
        => work(context, ct);

    public Task<T> ExecuteAsync<T>(Func<IDataContext, CancellationToken, Task<T>> work, UnitOfWorkOptions options = null, CancellationToken ct = default)
        => work(context, ct);

    public Task ExecuteAsync(Func<IDataContext, CancellationToken, Task> work, UnitOfWorkOptions options = null, CancellationToken ct = default)
        => work(context, ct);

    public IUnitOfWork Create(string key) => throw new NotImplementedException();
    public IUnitOfWork Create() => throw new NotImplementedException();
}
