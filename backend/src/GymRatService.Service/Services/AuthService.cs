using GymRatService.Common.DTOs.Auth;
using GymRatService.Common.Errors;
using GymRatService.Common.Settings;
using GymRatService.Repository.Entities;
using GymRatService.Repository.Interfaces;
using GymRatService.Service.Exceptions;
using GymRatService.Service.Extensions;
using GymRatService.Service.Interfaces;
using GymRatService.Service.Security;
using Microsoft.Extensions.Options;

namespace GymRatService.Service.Services;

public class AuthService(
    IUsersRepository usersRepository,
    IRefreshTokensRepository refreshTokensRepository,
    IPasswordHasher passwordHasher,
    ITokenGenerator tokenGenerator,
    IRefreshTokenGenerator refreshTokenGenerator,
    ILoginAttemptLimiter loginAttemptLimiter,
    IOptions<RefreshTokenSettings> refreshTokenOptions,
    TimeProvider timeProvider) : IAuthService
{
    public async Task RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.NormalizeEmail();

        if (await usersRepository.EmailExistsAsync(email, cancellationToken))
        {
            throw new BusinessLogicException(ErrorCodes.EmailAlreadyExists);
        }

        var hashedPassword = passwordHasher.Hash(request.Password);

        var user = new User
        {
            Email = email,
            PasswordHash = hashedPassword.Hash,
            PasswordSalt = hashedPassword.Salt,
            FirstName = request.FirstName.Trim(),
            LastName = string.IsNullOrWhiteSpace(request.LastName) ? null : request.LastName.Trim(),
            WeightUnit = request.WeightUnit,
            CreatedAt = timeProvider.GetUtcNow(),
        };

        if (!await usersRepository.TryAddAsync(user, cancellationToken))
        {
            throw new BusinessLogicException(ErrorCodes.EmailAlreadyExists);
        }
    }

    public async Task<AuthSession> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.NormalizeEmail();

        loginAttemptLimiter.RegisterAttempt(email);

        var user = await usersRepository.GetByEmailAsync(email, cancellationToken);

        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            throw new BusinessLogicException(ErrorCodes.InvalidCredentials);
        }

        loginAttemptLimiter.Reset(email);

        var now = timeProvider.GetUtcNow();

        await usersRepository.UpdateLastLoginAsync(user.Id, now, cancellationToken);
        await refreshTokensRepository.DeleteExpiredAsync(user.Id, now, cancellationToken);

        return await IssueSessionAsync(user, Guid.CreateVersion7(), now, cancellationToken);
    }

    public async Task<AuthSession> RefreshAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            throw new BusinessLogicException(ErrorCodes.InvalidRefreshToken);
        }

        var now = timeProvider.GetUtcNow();
        var stored = await refreshTokensRepository.GetByHashAsync(refreshTokenGenerator.Hash(refreshToken), cancellationToken);

        if (stored is null || stored.RevokedAt is not null || stored.ExpiresAt <= now)
        {
            throw new BusinessLogicException(ErrorCodes.InvalidRefreshToken);
        }

        var reuseGrace = TimeSpan.FromSeconds(refreshTokenOptions.Value.ReuseGraceSeconds);

        if (stored.RotatedAt is { } rotatedAt && now - rotatedAt > reuseGrace)
        {
            await refreshTokensRepository.RevokeFamilyAsync(stored.FamilyId, now, cancellationToken);

            throw new BusinessLogicException(ErrorCodes.InvalidRefreshToken);
        }

        var user = await usersRepository.GetByIdAsync(stored.UserId, cancellationToken)
            ?? throw new BusinessLogicException(ErrorCodes.InvalidRefreshToken);

        await refreshTokensRepository.MarkRotatedAsync(stored.Id, now, cancellationToken);

        return await IssueSessionAsync(user, stored.FamilyId, now, cancellationToken);
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            return;
        }

        var stored = await refreshTokensRepository.GetByHashAsync(refreshTokenGenerator.Hash(refreshToken), cancellationToken);

        if (stored is not null)
        {
            await refreshTokensRepository.RevokeFamilyAsync(stored.FamilyId, timeProvider.GetUtcNow(), cancellationToken);
        }
    }

    private async Task<AuthSession> IssueSessionAsync(User user, Guid familyId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var generated = refreshTokenGenerator.Generate();
        var expiresAt = now.AddDays(refreshTokenOptions.Value.LifetimeDays);

        await refreshTokensRepository.AddAsync(
            new RefreshToken
            {
                UserId = user.Id,
                FamilyId = familyId,
                TokenHash = generated.Hash,
                CreatedAt = now,
                ExpiresAt = expiresAt,
            },
            cancellationToken);

        return new AuthSession(tokenGenerator.Generate(user), generated.Token, expiresAt);
    }
}
