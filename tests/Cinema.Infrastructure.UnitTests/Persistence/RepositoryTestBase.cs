using Cinema.Infrastructure.Persistence.Write;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Infrastructure.UnitTests.Persistence;

public abstract class RepositoryTestBase : IDisposable
{
    protected readonly CinemaDbContext Context;

    protected RepositoryTestBase()
    {
        var options = new DbContextOptionsBuilder<CinemaDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        Context = new CinemaDbContext(options);
    }

    public void Dispose()
    {
        Context.Dispose();
        GC.SuppressFinalize(this);
    }
}
