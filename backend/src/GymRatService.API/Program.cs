using System.Text;
using GymRatService.API.Cookies;
using GymRatService.API.Extensions;
using GymRatService.API.Filters;
using GymRatService.API.Health;
using GymRatService.API.Middleware;
using GymRatService.Common.Settings;
using GymRatService.Service;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;

const string FrontendCorsPolicy = "Frontend";
string[] supportedCultures = ["en", "ro"];

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsProduction())
{
    builder.Logging.AddJsonConsole();
}

builder.WebHost.ConfigureKestrel(options => options.LimitRequestBodySize());

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy => policy
        .WithOrigins(builder.Configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>()?.AllowedOrigins ?? [])
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

if (!HostEnvironmentExtensions.IsGeneratingOpenApiDocument())
{
    builder.Services.AddStartupSettings(builder.Configuration);
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();

        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = string.IsNullOrEmpty(jwtSettings.Key) ? null : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddSingleton<RefreshTokenCookie>();
builder.Services.AddSingleton<AllowedOriginFilter>();

builder.Services.AddAiCoach(builder.Configuration);

builder.Services.AddTrustedForwardedHeaders(builder.Configuration);
builder.Services.AddAuthRateLimiting(builder.Configuration);
builder.Services.AddTransportSecurity();

builder.Services.AddRequestValidation();
builder.Services.AddControllers(options => options.Filters.AddService<ValidationActionFilter>());
builder.Services.AddApiDocument();

builder.Services.AddServiceLayer(builder.Configuration.GetConnectionString("DefaultConnection"));

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", timeout: TimeSpan.FromSeconds(4));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseTrustedForwardedHeaders();
app.UseTransportSecurity();

app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture(supportedCultures[0])
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures));

var errorPipeline = ((IApplicationBuilder)app).New();
if (!app.Environment.AllowsDevelopmentSettings())
{
    // The exception middleware clears the original response headers.
    errorPipeline.UseHsts();
}
errorPipeline.Run(UnexpectedExceptionHandler.WriteAsync);

app.UseExceptionHandler(new ExceptionHandlerOptions
{
    ExceptionHandler = errorPipeline.Build(),
    SuppressDiagnosticsCallback = _ => true,
});

app.Use(UnexpectedExceptionHandler.AbortFailedResponseAsync);
app.UseRouting();
app.UseMiddleware<BusinessLogicExceptionMiddleware>();

app.UseCors(FrontendCorsPolicy);

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = (_, _) => Task.CompletedTask,
})
    .WithMetadata(new HttpMethodMetadata([HttpMethods.Get, HttpMethods.Head]))
    .AllowAnonymous()
    .ExcludeFromDescription();

app.Run();
