using GymRatService.Repository;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GymRatService.IntegrationTests;

/// <summary>Hosts the real API in memory, with PostgreSQL swapped for a private in-memory SQLite database.</summary>
public sealed class GymRatApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<GymRatDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<GymRatDbContext>>();

            // The in-memory database lives only as long as this connection stays open.
            _connection.Open();
            services.AddDbContext<GymRatDbContext>(options => options.UseSqlite(_connection));
        });
    }

    /// <summary>Creates the schema and lets the test arrange its data.</summary>
    public async Task SeedAsync(Action<GymRatDbContext> seed)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GymRatDbContext>();

        await context.Database.EnsureCreatedAsync();
        seed(context);
        await context.SaveChangesAsync();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
