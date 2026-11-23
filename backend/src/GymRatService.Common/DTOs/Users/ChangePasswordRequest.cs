namespace GymRatService.Common.DTOs.Users;

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
