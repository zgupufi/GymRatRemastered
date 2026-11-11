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

    public const string ChatRateLimited = "BLException_ChatService_RateLimited";

    public const string ChatTokenBudgetExceeded = "BLException_ChatService_TokenBudgetExceeded";

    public const string ChatProviderUnavailable = "BLException_ChatService_ProviderUnavailable";

    public const string ChatInvalidReply = "BLException_ChatService_InvalidReply";

    public const string CoachSuggestionNotFound = "BLException_CoachService_SuggestionNotFound";

    public const string CoachSuggestionNotPending = "BLException_CoachService_SuggestionNotPending";

    public const string CoachSuggestionExerciseNotInWorkout = "BLException_CoachService_ExerciseNotInWorkout";

    public const string CoachSuggestionStale = "BLException_CoachService_SuggestionStale";

    public const string CoachSuggestionInvalidChange = "BLException_CoachService_InvalidChange";

    public const string CoachTooManyProposals = "BLException_CoachService_TooManyProposals";

    public const string InvalidRequest = "BLException_Validation_InvalidRequest";
}
