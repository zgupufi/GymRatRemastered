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
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace GymRatService.IntegrationTests;

public sealed class GymRatApiFactory(string environment = "Testing") : WebApplicationFactory<Program>
{
    private const string SchemeSelector = "TestOrBearer";

    private const string JwtKey = "integration-test-signing-key-that-is-long-enough";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting("JwtSettings:Key", JwtKey);
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
