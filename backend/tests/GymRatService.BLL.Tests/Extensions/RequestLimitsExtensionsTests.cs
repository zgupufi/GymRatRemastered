using System.Net;
using GymRatService.API.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;

namespace GymRatService.BLL.Tests.Extensions;

public class RequestLimitsExtensionsTests
{
    [Fact]
    public void LimitRequestBodySize_CapsTheBodyAt256Kilobytes()
    {
        // Arrange
        var options = new KestrelServerOptions();

        // Act
        options.LimitRequestBodySize();

        // Assert
        Assert.Equal(262144, options.Limits.MaxRequestBodySize);
    }

    [Theory]
    [InlineData(262144, HttpStatusCode.OK)]
    [InlineData(262145, HttpStatusCode.RequestEntityTooLarge)]
    public async Task LimitRequestBodySize_MakesKestrelRejectLargerBodies(int bodySize, HttpStatusCode expected)
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Loopback, 0);
            options.LimitRequestBodySize();
        });
        await using var app = builder.Build();
        app.MapPost("/echo", async (HttpRequest request) =>
        {
            using var buffer = new MemoryStream();
            await request.Body.CopyToAsync(buffer);

            return Results.Ok(buffer.Length);
        });
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        using var client = new HttpClient { BaseAddress = new Uri(address) };

        // Act
        var response = await client.PostAsync("/echo", new ByteArrayContent(new byte[bodySize]));

        // Assert
        Assert.Equal(expected, response.StatusCode);
        await app.StopAsync();
    }
}
