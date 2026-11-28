using GymRatService.API.Extensions;
using GymRatService.Common.DTOs.Export;
using GymRatService.Common.Enums;
using GymRatService.Repository;
using GymRatService.Repository.Entities;
using GymRatService.Service.Interfaces;
using GymRatService.Service.Security;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GymRatService.IntegrationTests.Platform;

public sealed class PlatformEndpointsTests
{
    [Fact]
    public async Task GetOpenApiDocument_ReturnsTheDocument_InDevelopment()
    {
        // Arrange
        using var factory = new GymRatApiFactory(environment: "Development");
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/openapi/v1.json");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(document.GetProperty("paths").TryGetProperty("/api/Exercises", out _));
    }

    [Fact]
    public async Task GetOpenApiDocument_DescribesTheResponsesAsTheApiSerializesThem()
    {
        // Arrange
        using var factory = new GymRatApiFactory(environment: "Development");
        var client = factory.CreateClient();

        // Act
        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");

        // Assert
        var schemas = document.GetProperty("components").GetProperty("schemas");
        var profileResponse = document.GetProperty("paths").GetProperty("/api/Users/me").GetProperty("get")
            .GetProperty("responses").GetProperty("200").GetProperty("content").GetProperty("application/json").GetProperty("schema");
        Assert.Equal("#/components/schemas/UserProfileDto", profileResponse.GetProperty("$ref").GetString());
        var restTime = schemas.GetProperty("UserProfileDto").GetProperty("properties").GetProperty("defaultRestTimeSeconds");
        Assert.Equal("integer", restTime.GetProperty("type").GetString());
        Assert.False(restTime.TryGetProperty("pattern", out _));
        var stepKg = schemas.GetProperty("WorkoutExerciseRequest").GetProperty("properties").GetProperty("weightStepKg");
        Assert.Equal(["null", "number"], stepKg.GetProperty("type").EnumerateArray().Select(type => type.GetString()).Order());
        Assert.Equal([0, 1], schemas.GetProperty("WeightUnit").GetProperty("enum").EnumerateArray().Select(value => value.GetInt32()));
        var statuses = schemas.GetProperty("SuggestionStatus").GetProperty("enum").EnumerateArray().Select(value => value.GetString());
        Assert.Equal(Enum.GetNames<SuggestionStatus>(), statuses);
    }

    [Fact]
    public async Task GetOpenApiDocument_ReturnsNotFound_OutsideDevelopment()
    {
        // Arrange
        using var factory = new GymRatApiFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/openapi/v1.json");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    public async Task Health_ReturnsOk_WhenTheDatabaseIsReachable(string method)
    {
        using var factory = new GymRatApiFactory();
        await factory.EnsureDatabaseAsync();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), "/health");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        if (method == "HEAD")
        {
            Assert.Empty(await response.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task Health_ReturnsServiceUnavailable_WhenTheDatabaseCannotBeOpened()
    {
        using var factory = new GymRatApiFactory();
        using var unavailableDatabase = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<GymRatDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<GymRatDbContext>>();
            services.AddDbContext<GymRatDbContext>(options => options.UseSqlite(
                $"Data Source={Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.db")};Mode=ReadOnly;Pooling=False"));
        }));
        using var client = unavailableDatabase.CreateClient();

        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task GetExercise_ReturnsLocalizedCorrelatedError_AndLogsSafeRequestFields_InProduction()
    {
        const string email = "private@example.test";
        var userId = Guid.NewGuid();
        using var factory = new GymRatApiFactory(environment: "Production");
        factory.Settings["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=gymrat";
        const string frontendOrigin = "https://frontend.example.test";
        factory.Settings["Cors:AllowedOrigins:0"] = frontendOrigin;
        factory.Settings["JwtSettings:Key"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        factory.Settings["PasswordSettings:Pepper"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        using var logs = new CapturedLogProvider();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<ILoggerProvider>(logs);
            services.RemoveAll<IExercisesService>();
            services.AddScoped<IExercisesService>(_ => throw new InvalidOperationException(email));
        }));
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new("https://gymrat.example.test") });
        var token = host.Services.GetRequiredService<ITokenGenerator>().Generate(new User { Id = userId, Email = email }).AccessToken;
        client.DefaultRequestHeaders.Add("Origin", frontendOrigin);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ro");

        using var response = await client.GetAsync($"/api/exercises/{Guid.NewGuid()}?email={email}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(frontendOrigin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.True(response.Headers.Contains("Strict-Transport-Security"));
        Assert.Equal("InternalServerError", body.GetProperty("code").GetString());
        Assert.Equal("A apărut o eroare neașteptată. Încearcă din nou.", body.GetProperty("message").GetString());
        Assert.DoesNotContain(email, body.GetRawText());
        var traceId = body.GetProperty("traceId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(traceId));
        Assert.Equal(ConsoleFormatterNames.Json, host.Services.GetRequiredService<IOptions<ConsoleLoggerOptions>>().Value.FormatterName);

        await logs.RequestCompleted.WaitAsync(TimeSpan.FromSeconds(5));
        var request = Assert.Single(logs.Entries, entry => entry.Fields.ContainsKey("DurationMs"));
        Assert.Equal(traceId, request.Fields["TraceId"]);
        Assert.Equal("GET", request.Fields["Method"]);
        Assert.Equal("api/Exercises/{id}", request.Fields["PathTemplate"]);
        Assert.Equal(500, request.Fields["StatusCode"]);
        Assert.Equal(userId, request.Fields["UserId"]);
        Assert.True(Assert.IsType<double>(request.Fields["DurationMs"]) >= 0);
        Assert.Contains(logs.Entries, entry =>
            entry.Level == LogLevel.Error && Equals(traceId, entry.Fields.GetValueOrDefault("TraceId")));
        Assert.All(logs.Entries, entry =>
        {
            Assert.DoesNotContain(email, entry.Message);
            Assert.DoesNotContain(token, entry.Message);
        });
    }

    [Fact]
    public async Task Register_ReturnsPayloadTooLarge_WhenKestrelRejectsTheRequestBody()
    {
        using var factory = new GymRatApiFactory();
        factory.UseKestrel(0);
        using var client = factory.CreateClient();
        using var body = new StringContent(
            new string(' ', checked((int)RequestLimitsExtensions.MaximumRequestBodySize + 1)), Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/api/auth/register", body);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task ExportSets_AbortsAnAlreadyStartedResponse_AndLogsTheFailureSafely()
    {
        const string email = "private@example.test";
        var releaseFailure = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var factory = new GymRatApiFactory();
        using var logs = new CapturedLogProvider();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<ILoggerProvider>(logs);
            services.RemoveAll<IExportService>();
            services.AddSingleton<IExportService>(new FailingCsvExport(releaseFailure.Task, email));
        }));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer",
            GymRatApiFactory.CreateAccessToken(new Claim("sub", Guid.NewGuid().ToString())));

        using var response = await client.GetAsync("/api/users/me/export/sets", HttpCompletionOption.ResponseHeadersRead);
        releaseFailure.SetResult();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await Assert.ThrowsAnyAsync<Exception>(() => response.Content.ReadAsStringAsync());
        var error = Assert.Single(logs.Entries, entry => entry.Level == LogLevel.Error);
        await logs.RequestCompleted.WaitAsync(TimeSpan.FromSeconds(5));
        var request = Assert.Single(logs.Entries, entry => entry.Fields.ContainsKey("DurationMs"));
        Assert.Equal(error.Fields["TraceId"], request.Fields["TraceId"]);
        Assert.All(logs.Entries, entry => Assert.DoesNotContain(email, entry.Message));
    }

    [Fact]
    public async Task UnmatchedRequest_DoesNotLogRawPathOrUnrecognizedMethod()
    {
        const string unrecognizedMethod = "private-method";
        const string unmatchedPath = "/private@example.test";
        using var factory = new GymRatApiFactory();
        using var logs = new CapturedLogProvider();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<ILoggerProvider>(logs)));
        using var client = host.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(unrecognizedMethod), unmatchedPath);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await logs.RequestCompleted.WaitAsync(TimeSpan.FromSeconds(5));
        var entry = Assert.Single(logs.Entries, entry => entry.Fields.ContainsKey("DurationMs"));
        Assert.Equal("OTHER", entry.Fields["Method"]);
        Assert.Null(entry.Fields["PathTemplate"]);
        Assert.DoesNotContain(unrecognizedMethod, entry.Message);
        Assert.DoesNotContain(unmatchedPath, entry.Message);
    }

    [Fact]
    public async Task AuthenticatedRequest_DoesNotLogANonGuidSubject()
    {
        const string subject = "private@example.test";
        using var factory = new GymRatApiFactory();
        await factory.EnsureDatabaseAsync();
        using var logs = new CapturedLogProvider();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<ILoggerProvider>(logs)));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", GymRatApiFactory.CreateAccessToken(new Claim("sub", subject)));

        using var response = await client.GetAsync("/api/exercises");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await logs.RequestCompleted.WaitAsync(TimeSpan.FromSeconds(5));
        var entry = Assert.Single(logs.Entries, entry => entry.Fields.ContainsKey("DurationMs"));
        Assert.Null(entry.Fields["UserId"]);
        Assert.DoesNotContain(subject, entry.Message);
    }

    private sealed class FailingCsvExport(Task releaseFailure, string diagnostic) : IExportService
    {
        public Task<UserExportDto> GetExportAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IAsyncEnumerable<string>> GetSetsCsvLinesAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(FailAfterHeadersAsync(releaseFailure.WaitAsync(cancellationToken), diagnostic));

        private static async IAsyncEnumerable<string> FailAfterHeadersAsync(Task releaseFailure, string diagnostic)
        {
            await releaseFailure;
            yield return await Task.FromException<string>(new InvalidOperationException(diagnostic));
        }
    }

    private sealed class CapturedLogProvider : ILoggerProvider
    {
        private readonly TaskCompletionSource _requestCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ConcurrentQueue<(LogLevel Level, string Message, Dictionary<string, object?> Fields)> Entries { get; } = new();

        public Task RequestCompleted => _requestCompleted.Task;

        public ILogger CreateLogger(string categoryName) => new CapturedLogger(this);

        public void Dispose()
        {
        }

        private sealed class CapturedLogger(CapturedLogProvider provider) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (state is IEnumerable<KeyValuePair<string, object?>> fields)
                {
                    var message = formatter(state, exception);
                    var structuredFields = new Dictionary<string, object?>(fields);
                    provider.Entries.Enqueue((logLevel, exception is null ? message : $"{message} {exception}", structuredFields));
                    if (structuredFields.ContainsKey("DurationMs"))
                    {
                        provider._requestCompleted.TrySetResult();
                    }
                }
            }
        }
    }
}
