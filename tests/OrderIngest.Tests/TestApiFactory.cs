using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using OrderIngest.Business.Uber;

namespace OrderIngest.Tests;

/// <summary>
/// Boots the real API on a throwaway SQLite file so tests cannot see each
/// other's rows. An IUberOrderClient can be swapped in to simulate fetch
/// failures. The database (including WAL/SHM siblings) is removed on dispose.
/// </summary>
internal sealed class TestApiFactory(IUberOrderClient? uberClientOverride = null)
    : WebApplicationFactory<Program>
{
    public const string UberSecret = "integration-test-secret";

    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), $"order-ingest-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Orders", $"Data Source={_dbPath}");
        builder.UseSetting("Uber:ClientSecret", UberSecret);

        if (uberClientOverride is not null)
        {
            builder.ConfigureTestServices(services =>
                services.AddSingleton(uberClientOverride));
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        // Pooled connections hold the file open; SQLite in WAL mode also
        // leaves -wal/-shm siblings next to the database.
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var path = _dbPath + suffix;
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
