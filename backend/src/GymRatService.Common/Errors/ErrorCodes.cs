namespace GymRatService.Common.Errors;

/// <summary>Names of business errors, so a 400 response can be traced back to where it was raised.</summary>
public static class ErrorCodes
{
    public const string ExerciseNotFound = "Exercises.NotFound";
}
