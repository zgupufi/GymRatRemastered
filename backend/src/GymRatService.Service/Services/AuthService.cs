using System.Net.Mail;
using GymRatService.Common.DTOs.Auth;
using GymRatService.Common.Errors;
using GymRatService.Repository.Entities;
using GymRatService.Repository.Interfaces;
using GymRatService.Service.Exceptions;
using GymRatService.Service.Interfaces;
using GymRatService.Service.Security;
using Microsoft.Extensions.Localization;

namespace GymRatService.Service.Services;

public class AuthService(
    IUsersRepository usersRepository,
    IPasswordHasher passwordHasher,
    ITokenGenerator tokenGenerator,
    TimeProvider timeProvider,
    IStringLocalizer<AuthService> localizer) : IAuthService
{
    private const int MinimumPasswordLength = 8;

    private const int MaximumEmailLength = 100;

    private const int MaximumNameLength = 50;

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var firstName = request.FirstName.Trim();
        var lastName = string.IsNullOrWhiteSpace(request.LastName) ? null : request.LastName.Trim();

        ValidateRegistration(request, email, firstName, lastName);

        if (await usersRepository.EmailExistsAsync(email, cancellationToken))
        {
            throw new ServiceException(ErrorCodes.EmailAlreadyExists, localizer["EmailAlreadyExists"]);
        }

        var hashedPassword = passwordHasher.Hash(request.Password);

        var user = new User
        {
            Email = email,
            PasswordHash = hashedPassword.Hash,
            PasswordSalt = hashedPassword.Salt,
            FirstName = firstName,
            LastName = lastName,
            WeightUnit = request.WeightUnit,
            CreatedAt = timeProvider.GetUtcNow(),
        };

        await usersRepository.AddAsync(user, cancellationToken);

        return new RegisterResponse(user.Id);
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await usersRepository.GetByEmailAsync(NormalizeEmail(request.Email), cancellationToken);

        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            throw new ServiceException(ErrorCodes.InvalidCredentials, localizer["InvalidCredentials"]);
        }

        await usersRepository.UpdateLastLoginAsync(user.Id, timeProvider.GetUtcNow(), cancellationToken);

        return tokenGenerator.Generate(user);
    }

    private void ValidateRegistration(RegisterRequest request, string email, string firstName, string? lastName)
    {
        if (email.Length > MaximumEmailLength || !MailAddress.TryCreate(email, out var address) || address.Address != email)
        {
            throw new ServiceException(ErrorCodes.InvalidEmail, localizer["InvalidEmail"]);
        }

        if (request.Password.Length < MinimumPasswordLength)
        {
            throw new ServiceException(ErrorCodes.PasswordTooShort, localizer["PasswordTooShort", MinimumPasswordLength]);
        }

        if (firstName.Length == 0)
        {
            throw new ServiceException(ErrorCodes.FirstNameRequired, localizer["FirstNameRequired"]);
        }

        if (firstName.Length > MaximumNameLength || lastName?.Length > MaximumNameLength)
        {
            throw new ServiceException(ErrorCodes.NameTooLong, localizer["NameTooLong", MaximumNameLength]);
        }

        if (!Enum.IsDefined(request.WeightUnit))
        {
            throw new ServiceException(ErrorCodes.InvalidWeightUnit, localizer["InvalidWeightUnit"]);
        }
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
