using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// A segunda tentativa só acontece numa violação de unicidade (23505), uma
/// só vez, e com o ChangeTracker limpo: o que a primeira tentativa
/// acrescentou não volta a ser gravado.
/// </summary>
public sealed class UniqueViolationRetryTests : IDisposable
{
    private readonly DmoDbContext _db = new(new DbContextOptionsBuilder<DmoDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    public void Dispose() => _db.Dispose();

    private static DbUpdateException Violation(string sqlState) =>
        new("falhou", new PostgresException("falhou", "ERROR", "ERROR", sqlState));

    [Fact]
    public async Task UniqueViolation_RunsAgainOnce_WithAClearChangeTracker()
    {
        var attempts = 0;
        var result = await UniqueViolationRetry.RunAsync(_db, () =>
        {
            attempts++;
            if (attempts == 1)
            {
                _db.Machines.Add(new Machine { Code = "B1" });
                throw Violation(PostgresErrorCodes.UniqueViolation);
            }

            return Task.FromResult(_db.ChangeTracker.Entries().Count());
        });

        Assert.Equal(2, attempts);
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task SecondUniqueViolation_Propagates()
    {
        var attempts = 0;
        await Assert.ThrowsAsync<DbUpdateException>(() => UniqueViolationRetry.RunAsync<int>(_db, () =>
        {
            attempts++;
            throw Violation(PostgresErrorCodes.UniqueViolation);
        }));

        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task OtherFailures_AreNotRetried()
    {
        var attempts = 0;
        await Assert.ThrowsAsync<DbUpdateException>(() => UniqueViolationRetry.RunAsync<int>(_db, () =>
        {
            attempts++;
            throw Violation(PostgresErrorCodes.ForeignKeyViolation);
        }));

        Assert.Equal(1, attempts);
    }
}
