namespace GymRatService.Common.DTOs.Auth;

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt);
