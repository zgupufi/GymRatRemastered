namespace GymRatService.Common.Errors;

public sealed record ValidationError(string Property, string Message);
