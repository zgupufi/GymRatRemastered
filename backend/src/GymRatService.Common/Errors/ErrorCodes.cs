namespace GymRatService.Common.Errors;

public static class ErrorCodes
{
    public const string ExerciseNotFound = "Exercises.NotFound";

    public const string EmailAlreadyExists = "Auth.EmailAlreadyExists";

    public const string InvalidEmail = "Auth.InvalidEmail";

    public const string PasswordTooShort = "Auth.PasswordTooShort";

    public const string FirstNameRequired = "Auth.FirstNameRequired";

    public const string InvalidWeightUnit = "Auth.InvalidWeightUnit";

    public const string NameTooLong = "Auth.NameTooLong";

    public const string InvalidCredentials = "Auth.InvalidCredentials";

    public const string UserNotFound = "Users.NotFound";
}
