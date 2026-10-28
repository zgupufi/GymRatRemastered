namespace GymRatService.Common.Errors;

public sealed record ErrorResponse(string Code, string Message, IReadOnlyList<ValidationError>? Errors = null);
