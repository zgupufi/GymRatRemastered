using FluentValidation;
using GymRatService.Common.DTOs.Auth;
using GymRatService.Common.Errors;
using GymRatService.Repository.Entities;
using GymRatService.Repository.Interfaces;
using GymRatService.Service.Exceptions;
using GymRatService.Service.Extensions;
using GymRatService.Service.Interfaces;
using GymRatService.Service.Security;

namespace GymRatService.Service.Services;

public class AuthService(
    IUsersRepository usersRepository,
    IPasswordHasher passwordHasher,
    ITokenGenerator tokenGenerator,
    IValidator<RegisterRequest> registerValidator,
    TimeProvider timeProvider) : IAuthService
{
    public async Task RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        await registerValidator.ValidateAndThrowAsync(request, cancellationToken);

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

        await usersRepository.AddAsync(user, cancellationToken);
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await usersRepository.GetByEmailAsync(request.Email.NormalizeEmail(), cancellationToken);

        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            throw new BusinessLogicException(ErrorCodes.InvalidCredentials);
        }

        await usersRepository.UpdateLastLoginAsync(user.Id, timeProvider.GetUtcNow(), cancellationToken);

        return tokenGenerator.Generate(user);
    }
}
