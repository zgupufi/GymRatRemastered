namespace GymRatService.Common.Errors;

/// <summary>Body of the 400 response returned when a business rule fails in the service layer.</summary>
public sealed record ErrorResponse(string Code, string Message);
