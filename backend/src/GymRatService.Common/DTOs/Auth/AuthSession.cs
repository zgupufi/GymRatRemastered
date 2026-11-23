namespace GymRatService.Common.DTOs.Auth;

public sealed record AuthSession(LoginResponse Login, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);
