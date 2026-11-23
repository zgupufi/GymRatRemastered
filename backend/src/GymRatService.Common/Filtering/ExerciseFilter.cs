using GymRatService.Common.Enums;

namespace GymRatService.Common.Filtering;

public class ExerciseFilter : Filter<ExerciseSortField>
{
    public string? Muscle { get; set; }

    public Equipment? Equipment { get; set; }

    public MovementPattern? Pattern { get; set; }
}
