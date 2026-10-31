namespace GymRatService.Common.Errors;

public static class ErrorCodes
{
    public const string ExerciseNotFound = "BLException_ExercisesService_ExerciseNotFound";

    public const string EmailAlreadyExists = "BLException_AuthService_EmailAlreadyExists";

    public const string InvalidCredentials = "BLException_AuthService_InvalidCredentials";

    public const string UnauthorizedAccess = "BLException_AuthService_UnauthorizedAccess";

    public const string UserNotFound = "BLException_UsersService_UserNotFound";

    public const string WorkoutNotFound = "BLException_WorkoutsService_WorkoutNotFound";

    public const string WorkoutExerciseNotFound = "BLException_WorkoutsService_ExerciseNotFound";

    public const string SessionNotFound = "BLException_SessionsService_SessionNotFound";

    public const string SessionAlreadyFinished = "BLException_SessionsService_SessionAlreadyFinished";

    public const string InvalidRequest = "BLException_Validation_InvalidRequest";
}
