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
    private const int MaxRotations = 2;

    private static readonly TimeSpan RevokedRetention = TimeSpan.FromDays(1);

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
        await refreshTokensRepository.DeleteStaleAsync(user.Id, now, RevokedRetention, cancellationToken);

        var settings = refreshTokenOptions.Value;
        var generated = refreshTokenGenerator.Generate();
        var expiresAt = now.AddDays(Math.Min(settings.LifetimeDays, settings.AbsoluteLifetimeDays));

        await refreshTokensRepository.AddAsync(CreateToken(user.Id, Guid.CreateVersion7(), generated.Hash, now, now, expiresAt), cancellationToken);

        return new AuthSession(tokenGenerator.Generate(user), generated.Token, expiresAt);
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

        var settings = refreshTokenOptions.Value;
        var generated = refreshTokenGenerator.Generate();
        var slidingExpiry = now.AddDays(settings.LifetimeDays);
        var absoluteExpiry = stored.SessionStartedAt.AddDays(settings.AbsoluteLifetimeDays);
        var expiresAt = slidingExpiry < absoluteExpiry ? slidingExpiry : absoluteExpiry;
        var successor = CreateToken(user.Id, stored.FamilyId, generated.Hash, stored.SessionStartedAt, now, expiresAt);

        if (!await refreshTokensRepository.TryRotateAsync(stored.Id, successor, now, MaxRotations, cancellationToken))
        {
            throw new BusinessLogicException(ErrorCodes.InvalidRefreshToken);
        }

        if (await refreshTokensRepository.IsFamilyRevokedAsync(stored.FamilyId, cancellationToken))
        {
            await refreshTokensRepository.RevokeFamilyAsync(stored.FamilyId, now, cancellationToken);

            throw new BusinessLogicException(ErrorCodes.InvalidRefreshToken);
        }

        return new AuthSession(tokenGenerator.Generate(user), generated.Token, expiresAt);
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

    private static RefreshToken CreateToken(
        Guid userId,
        Guid familyId,
        string tokenHash,
        DateTimeOffset sessionStartedAt,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        return new RefreshToken
        {
            UserId = userId,
            FamilyId = familyId,
            TokenHash = tokenHash,
            SessionStartedAt = sessionStartedAt,
            CreatedAt = createdAt,
            ExpiresAt = expiresAt,
        };
    }
}
