using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace GymRatService.API.Extensions;

public static class RequestLimitsExtensions
{
    public const long MaximumRequestBodySize = 256 * 1024;

    public static KestrelServerOptions LimitRequestBodySize(this KestrelServerOptions options)
    {
        options.Limits.MaxRequestBodySize = MaximumRequestBodySize;

        return options;
    }
}
