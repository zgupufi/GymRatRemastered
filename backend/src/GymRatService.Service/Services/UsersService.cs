using GymRatService.Common.DTOs.Users;
using GymRatService.Common.Errors;
using GymRatService.Repository.Interfaces;
using GymRatService.Service.Exceptions;
using GymRatService.Service.Interfaces;
using GymRatService.Service.Mapping;
using GymRatService.Service.Security;

namespace GymRatService.Service.Services;

public class UsersService(
    IUsersRepository usersRepository,
    IPasswordHasher passwordHasher,
    ILoginAttemptLimiter loginAttemptLimiter) : IUsersService
{
    public async Task<UserProfileDto> GetProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await usersRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new BusinessLogicException(ErrorCodes.UserNotFound);

        return user.ToProfileDto();
    }

    public async Task UpdateSettingsAsync(Guid userId, UpdateUserSettingsRequest request, CancellationToken cancellationToken)
    {
        var user = await usersRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new BusinessLogicException(ErrorCodes.UserNotFound);

        await usersRepository.UpdateSettingsAsync(
            userId,
            request.WeightUnit ?? user.WeightUnit,
            request.DefaultRestTimeSeconds ?? user.DefaultRestTimeSeconds,
            cancellationToken);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        await ConfirmPasswordAsync(userId, request.CurrentPassword, cancellationToken);

        var hashedPassword = passwordHasher.Hash(request.NewPassword);

        await usersRepository.UpdatePasswordAsync(userId, hashedPassword.Hash, hashedPassword.Salt, cancellationToken);
    }

    public async Task DeleteAccountAsync(Guid userId, DeleteAccountRequest request, CancellationToken cancellationToken)
    {
        await ConfirmPasswordAsync(userId, request.Password, cancellationToken);

        await usersRepository.DeleteAsync(userId, cancellationToken);
    }

    private async Task ConfirmPasswordAsync(Guid userId, string password, CancellationToken cancellationToken)
    {
        var user = await usersRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new BusinessLogicException(ErrorCodes.UserNotFound);

        loginAttemptLimiter.RegisterAttempt(user.Email);

        if (!passwordHasher.Verify(password, user.PasswordHash, user.PasswordSalt))
        {
            throw new BusinessLogicException(ErrorCodes.InvalidPassword);
        }

        loginAttemptLimiter.Reset(user.Email);
    }
}
