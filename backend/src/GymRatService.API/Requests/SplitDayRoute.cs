using Microsoft.AspNetCore.Mvc;

namespace GymRatService.API.Requests;

public sealed class SplitDayRoute
{
    [FromRoute(Name = "day")]
    public int Day { get; init; }
}
