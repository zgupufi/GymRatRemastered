using FluentValidation;
using GymRatService.Common.DTOs.Auth;
using GymRatService.Common.Enums;
using GymRatService.Common.Errors;
using GymRatService.Repository.Entities;
using GymRatService.Repository.Interfaces;
using GymRatService.Service.Exceptions;
using GymRatService.Service.Security;
using GymRatService.Service.Services;
using GymRatService.Service.Validators;
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
        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(Now);

        _passwordHasher.Hash(Arg.Any<string>()).Returns(new HashedPassword("hashed", "salt"));
        _service = new AuthService(_repository, _passwordHasher, _tokenGenerator, new RegisterRequestValidator(), timeProvider);
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

    [Fact]
    public async Task RegisterAsync_ThrowsEmailAlreadyExists_WhenTheEmailIsTaken()
    {
        // Arrange
        _repository.EmailExistsAsync("ana@example.com", Arg.Any<CancellationToken>()).Returns(true);

        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(() => _service.RegisterAsync(
            new RegisterRequest("ANA@example.com", "password123", "Ana", null, WeightUnit.Kilograms),
            CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.EmailAlreadyExists, exception.Code);
        await _repository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_ThrowsEveryValidationError_WhenTheRequestIsInvalid()
    {
        // Act
        var exception = await Assert.ThrowsAsync<ValidationException>(() => _service.RegisterAsync(
            new RegisterRequest("not-an-email", "short", "", null, (WeightUnit)5),
            CancellationToken.None));

        // Assert
        Assert.Equal(
            ["Email", "Password", "FirstName", "WeightUnit"],
            exception.Errors.Select(error => error.PropertyName));
        await _repository.DidNotReceive().EmailExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_ReturnsTheTokenAndRecordsTheLogin_WhenTheCredentialsAreValid()
    {
        // Arrange
        var user = new User { Id = Guid.CreateVersion7(), Email = "ana@example.com", PasswordHash = "hashed", PasswordSalt = "salt" };
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

    [Fact]
    public async Task LoginAsync_ThrowsInvalidCredentials_WhenThePasswordIsWrong()
    {
        // Arrange
        var user = new User { Email = "ana@example.com", PasswordHash = "hashed", PasswordSalt = "salt" };
        _repository.GetByEmailAsync("ana@example.com", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("wrong-password", "hashed", "salt").Returns(false);

        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(
            () => _service.LoginAsync(new LoginRequest("ana@example.com", "wrong-password"), CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.InvalidCredentials, exception.Code);
        _tokenGenerator.DidNotReceive().Generate(Arg.Any<User>());
        await _repository.DidNotReceive().UpdateLastLoginAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_ThrowsInvalidCredentials_WhenTheEmailIsUnknown()
    {
        // Arrange
        _repository.GetByEmailAsync("nobody@example.com", Arg.Any<CancellationToken>()).Returns((User?)null);

        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(
            () => _service.LoginAsync(new LoginRequest("nobody@example.com", "password123"), CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.InvalidCredentials, exception.Code);
        _passwordHasher.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }
}
