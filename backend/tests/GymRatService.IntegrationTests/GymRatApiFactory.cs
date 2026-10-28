using System.Net.Http.Headers;
using GymRatService.IntegrationTests.Authentication;
using GymRatService.Repository;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GymRatService.IntegrationTests;

public sealed class GymRatApiFactory : WebApplicationFactory<Program>
{
    private const string SchemeSelector = "TestOrBearer";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("JwtSettings:Key", "integration-test-signing-key-that-is-long-enough");
        builder.UseSetting("PasswordSettings:Pepper", "integration-test-pepper");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<GymRatDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<GymRatDbContext>>();

            _connection.Open();
            _connection.CreateFunction("uuid_generate_v7", () => Guid.CreateVersion7().ToString().ToUpperInvariant());
            services.AddDbContext<GymRatDbContext>(options => options.UseSqlite(_connection));

            services
                .AddAuthentication(SchemeSelector)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { })
                .AddPolicyScheme(SchemeSelector, SchemeSelector, options => options.ForwardDefaultSelector = context =>
                    context.Request.Headers.Authorization == TestAuthHandler.SchemeName
                        ? TestAuthHandler.SchemeName
                        : JwtBearerDefaults.AuthenticationScheme);
        });
    }

    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.SchemeName);

        return client;
    }

    public HttpClient CreateBearerClient(string accessToken)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, accessToken);

        return client;
    }

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
