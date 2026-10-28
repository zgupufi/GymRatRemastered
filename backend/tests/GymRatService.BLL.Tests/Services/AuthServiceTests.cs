using System.Globalization;
using FluentValidation;
using GymRatService.Common.DTOs.Auth;
using GymRatService.Common.Enums;
using GymRatService.Common.Errors;
using GymRatService.Repository.Entities;
using GymRatService.Repository.Interfaces;
using GymRatService.Service;
using GymRatService.Service.Exceptions;
using GymRatService.Service.Security;
using GymRatService.Service.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;

namespace GymRatService.BLL.Tests.Services;

public class AuthServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    private readonly IUsersRepository _repository = Substitute.For<IUsersRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenGenerator _tokenGenerator = Substitute.For<ITokenGenerator>();
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        var provider = new ServiceCollection()
            .AddLogging()
            .AddServiceLayer(connectionString: null)
            .BuildServiceProvider();
        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(Now);

        CultureInfo.CurrentUICulture = new CultureInfo("en");
        _passwordHasher.Hash(Arg.Any<string>()).Returns(new HashedPassword("hashed", "salt"));
        _service = new AuthService(
            _repository,
            _passwordHasher,
            _tokenGenerator,
            provider.GetRequiredService<IValidator<RegisterRequest>>(),
            timeProvider,
            provider.GetRequiredService<IStringLocalizer<AuthService>>());
    }

    [Fact]
    public async Task RegisterAsync_StoresTheUserWithAHashedPasswordAndANormalizedEmail()
    {
        // Arrange
        var request = new RegisterRequest(" Ana@Example.com ", "password123", " Ana ", " Pop ", WeightUnit.Pounds);
        User? stored = null;
        await _repository.AddAsync(Arg.Do<User>(user => stored = user), Arg.Any<CancellationToken>());

        // Act
        await _service.RegisterAsync(request, CancellationToken.None);

        // Assert
        Assert.NotNull(stored);
        Assert.Equal("ana@example.com", stored.Email);
        Assert.Equal("hashed", stored.PasswordHash);
        Assert.Equal("salt", stored.PasswordSalt);
        Assert.Equal("Ana", stored.FirstName);
        Assert.Equal("Pop", stored.LastName);
        Assert.Equal(WeightUnit.Pounds, stored.WeightUnit);
        Assert.Equal(90, stored.DefaultRestTimeSeconds);
        Assert.Equal(Now, stored.CreatedAt);
        _passwordHasher.Received(1).Hash("password123");
    }

    [Fact]
    public async Task RegisterAsync_StoresANullLastName_WhenItIsBlank()
    {
        // Arrange
        User? stored = null;
        await _repository.AddAsync(Arg.Do<User>(user => stored = user), Arg.Any<CancellationToken>());

        // Act
        await _service.RegisterAsync(
            new RegisterRequest("ana@example.com", "password123", "Ana", "  ", WeightUnit.Kilograms),
            CancellationToken.None);

        // Assert
        Assert.NotNull(stored);
        Assert.Null(stored.LastName);
    }

    [Theory]
    [InlineData("en", "An account with this email already exists.")]
    [InlineData("ro", "Există deja un cont cu această adresă de email.")]
    public async Task RegisterAsync_ThrowsLocalizedEmailAlreadyExists_WhenTheEmailIsTaken(string culture, string expectedMessage)
    {
        // Arrange
        CultureInfo.CurrentUICulture = new CultureInfo(culture);
        _repository.EmailExistsAsync("ana@example.com", Arg.Any<CancellationToken>()).Returns(true);

        // Act
        var exception = await Assert.ThrowsAsync<ServiceException>(() => _service.RegisterAsync(
            new RegisterRequest("ANA@example.com", "password123", "Ana", null, WeightUnit.Kilograms),
            CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.EmailAlreadyExists, exception.Code);
        Assert.Equal(expectedMessage, exception.Message);
        await _repository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("not-an-email", "password123", "Ana", null, WeightUnit.Kilograms, ErrorCodes.InvalidEmail, "The email address is not valid.")]
    [InlineData("Ana <ana@example.com>", "password123", "Ana", null, WeightUnit.Kilograms, ErrorCodes.InvalidEmail, "The email address is not valid.")]
    [InlineData("ana@example.com", "short", "Ana", null, WeightUnit.Kilograms, ErrorCodes.PasswordTooShort, "The password must have at least 8 characters.")]
    [InlineData("ana@example.com", "password123", "   ", null, WeightUnit.Kilograms, ErrorCodes.FirstNameRequired, "The first name is required.")]
    [InlineData("ana@example.com", "password123", "Ana", null, (WeightUnit)5, ErrorCodes.InvalidWeightUnit, "The weight unit is not valid.")]
    public async Task RegisterAsync_ThrowsAValidationError_WhenTheRequestIsInvalid(
        string email,
        string password,
        string firstName,
        string? lastName,
        WeightUnit weightUnit,
        string expectedCode,
        string expectedMessage)
    {
        // Act
        var exception = await Assert.ThrowsAsync<ServiceException>(() => _service.RegisterAsync(
            new RegisterRequest(email, password, firstName, lastName, weightUnit),
            CancellationToken.None));

        // Assert
        Assert.Equal(expectedCode, exception.Code);
        Assert.Equal(expectedMessage, exception.Message);
        await _repository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_ThrowsNameTooLong_WhenTheLastNameExceedsTheLimit()
    {
        // Act
        var exception = await Assert.ThrowsAsync<ServiceException>(() => _service.RegisterAsync(
            new RegisterRequest("ana@example.com", "password123", "Ana", new string('p', 51), WeightUnit.Kilograms),
            CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.NameTooLong, exception.Code);
        Assert.Equal("The first and last name can have at most 50 characters each.", exception.Message);
    }

    [Fact]
    public async Task LoginAsync_ReturnsTheTokenAndRecordsTheLogin_WhenTheCredentialsAreValid()
    {
        // Arrange
        var user = new User { Email = "ana@example.com", PasswordHash = "hashed", PasswordSalt = "salt" };
        var token = new LoginResponse("access-token", Now.AddHours(6));
        _repository.GetByEmailAsync("ana@example.com", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("password123", "hashed", "salt").Returns(true);
        _tokenGenerator.Generate(user).Returns(token);

        // Act
        var response = await _service.LoginAsync(new LoginRequest(" ANA@example.com", "password123"), CancellationToken.None);

        // Assert
        Assert.Equal(token, response);
        await _repository.Received(1).UpdateLastLoginAsync(user.Id, Now, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("en", "The email or password is incorrect.")]
    [InlineData("ro", "Adresa de email sau parola este incorectă.")]
    public async Task LoginAsync_ThrowsLocalizedInvalidCredentials_WhenThePasswordIsWrong(string culture, string expectedMessage)
    {
        // Arrange
        CultureInfo.CurrentUICulture = new CultureInfo(culture);
        var user = new User { Email = "ana@example.com", PasswordHash = "hashed", PasswordSalt = "salt" };
        _repository.GetByEmailAsync("ana@example.com", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("wrong-password", "hashed", "salt").Returns(false);

        // Act
        var exception = await Assert.ThrowsAsync<ServiceException>(
            () => _service.LoginAsync(new LoginRequest("ana@example.com", "wrong-password"), CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.InvalidCredentials, exception.Code);
        Assert.Equal(expectedMessage, exception.Message);
        _tokenGenerator.DidNotReceive().Generate(Arg.Any<User>());
        await _repository.DidNotReceive().UpdateLastLoginAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_ThrowsInvalidCredentials_WhenTheEmailIsUnknown()
    {
        // Arrange
        _repository.GetByEmailAsync("nobody@example.com", Arg.Any<CancellationToken>()).Returns((User?)null);

        // Act
        var exception = await Assert.ThrowsAsync<ServiceException>(
            () => _service.LoginAsync(new LoginRequest("nobody@example.com", "password123"), CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.InvalidCredentials, exception.Code);
        _passwordHasher.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }
}
