using System.Text;
using GymRatService.API.Cookies;
using GymRatService.API.Extensions;
using GymRatService.API.Filters;
using GymRatService.API.Middleware;
using GymRatService.Common.Settings;
using GymRatService.Service;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

const string FrontendCorsPolicy = "Frontend";
string[] supportedCultures = ["en", "ro"];

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy => policy
        .WithOrigins(builder.Configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>()?.AllowedOrigins ?? [])
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

builder.Services.AddStartupSettings(builder.Configuration);

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
builder.Services.AddOpenApi();

builder.Services.AddServiceLayer(builder.Configuration.GetConnectionString("DefaultConnection"));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseTrustedForwardedHeaders();
app.UseTransportSecurity();

app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture(supportedCultures[0])
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures));

app.UseMiddleware<BusinessLogicExceptionMiddleware>();

app.UseCors(FrontendCorsPolicy);

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
