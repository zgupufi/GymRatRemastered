using System.Data.Common;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using GymRatService.Common.DTOs.Auth;
using GymRatService.IntegrationTests.Authentication;
using GymRatService.Repository;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace GymRatService.IntegrationTests;

public sealed class GymRatApiFactory(string environment = "Testing") : WebApplicationFactory<Program>
{
    private const string SchemeSelector = "TestOrBearer";

    private const string JwtKey = "integration-test-signing-key-that-is-long-enough";

    public const string PostgresConnectionVariable = "GYMRAT_TEST_POSTGRES";

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"gymrat-tests-{Guid.NewGuid():N}.db");

    public static string? PostgresConnection { get; } = Environment.GetEnvironmentVariable(PostgresConnectionVariable);

    public static bool UsesPostgres => !string.IsNullOrWhiteSpace(PostgresConnection);

    public IChatClient? ChatClient { get; set; }

    public OffsetTimeProvider Time { get; } = new();

    public QueryCounter Queries { get; } = new();

    public Dictionary<string, string> Settings { get; } = [];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting("JwtSettings:Key", JwtKey);
        builder.UseSetting("PasswordSettings:Pepper", "integration-test-pepper");

        foreach (var (key, value) in Settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<GymRatDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<GymRatDbContext>>();

            if (ChatClient is not null)
            {
                services.RemoveAll<IChatClient>();
                services.AddSingleton(ChatClient);
            }

            services.AddSingleton<IStartupFilter, ClientIpStartupFilter>();
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);

            services.AddDbContext<GymRatDbContext>(options =>
            {
                if (UsesPostgres)
                {
                    options.UseNpgsql(PostgresConnection).AddInterceptors(Queries);
                }
                else
                {
                    options
                        .UseSqlite($"Data Source={_databasePath};Pooling=False")
                        .ReplaceService<IModelCustomizer, SqliteDateTimeOffsetModelCustomizer>()
                        .AddInterceptors(UuidFunctionInterceptor.Instance, Queries);
                }
            });

            services
                .AddAuthentication(SchemeSelector)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { })
                .AddPolicyScheme(SchemeSelector, SchemeSelector, options => options.ForwardDefaultSelector = context =>
                    context.Request.Headers.Authorization == TestAuthHandler.SchemeName
                        ? TestAuthHandler.SchemeName
                        : JwtBearerDefaults.AuthenticationScheme);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var lifetime = new GatedHostLifetime();
        builder.ConfigureServices(services => services.AddSingleton<IHostLifetime>(lifetime));

        var host = builder.Build();
        var start = host.StartAsync();
        lifetime.Release();
        start.GetAwaiter().GetResult();

        return host;
    }

    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.SchemeName);

        return client;
    }

    public HttpClient CreateClientWithIp(string clientIp, string? forwardedFor = null)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(ClientIpStartupFilter.HeaderName, clientIp);

        if (forwardedFor is not null)
        {
            client.DefaultRequestHeaders.Add("X-Forwarded-For", forwardedFor);
        }

        return client;
    }

    public HttpClient CreateBearerClient(string accessToken)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, accessToken);

        return client;
    }

    public async Task<string> RegisterAndLoginAsync(RegisterRequest request)
    {
        var client = CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", request);

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(request.Email, request.Password));
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();

        return login?.AccessToken ?? throw new InvalidOperationException("Login did not return an access token.");
    }

    public static string CreateAccessToken(params Claim[] claims)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = "GymRatService",
            Audience = "GymRatClient",
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey)),
                SecurityAlgorithms.HmacSha256),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    public async Task EnsureDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GymRatDbContext>();

        if (!UsesPostgres)
        {
            await context.Database.EnsureCreatedAsync();
        }
    }

    public async Task SeedAsync(Action<GymRatDbContext> seed)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GymRatDbContext>();

        if (!UsesPostgres)
        {
            await context.Database.EnsureCreatedAsync();
        }

        seed(context);
        await context.SaveChangesAsync();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            File.Delete(_databasePath);
        }
    }
}

internal sealed class UuidFunctionInterceptor : DbConnectionInterceptor
{
    public static UuidFunctionInterceptor Instance { get; } = new();

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        Register(connection);
    }

    public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Register(connection);

        return Task.CompletedTask;
    }

    private static void Register(DbConnection connection)
    {
        var sqlite = (SqliteConnection)connection;
        sqlite.CreateFunction("uuid_generate_v7", () => Guid.CreateVersion7().ToString().ToUpperInvariant());

        using var command = sqlite.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=MEMORY; PRAGMA synchronous=OFF;";
        command.ExecuteNonQuery();
    }
}
