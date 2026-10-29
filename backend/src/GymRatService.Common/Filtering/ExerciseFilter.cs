using GymRatService.Common.Enums;

namespace GymRatService.Common.Filtering;

public class ExerciseFilter : Filter<ExerciseSortField>
{
    public string? Muscle { get; set; }
}
