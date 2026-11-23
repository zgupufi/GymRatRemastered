using GymRatService.Common.DTOs.Auth;

namespace GymRatService.Service.Interfaces;

public interface IAuthService
{
    Task RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    Task<AuthSession> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<AuthSession> RefreshAsync(string? refreshToken, CancellationToken cancellationToken);

    Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken);
}
