using GymRatService.Common.Enums;

namespace GymRatService.Common.Filtering;

public class ProgressFilter
{
    public string? TimeZone { get; set; }

    public ProgressRange Range { get; set; } = ProgressRange.OneYear;
}
