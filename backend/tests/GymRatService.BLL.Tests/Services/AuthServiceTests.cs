using GymRatService.Common.DTOs.Auth;
using GymRatService.Common.Enums;
using GymRatService.Common.Errors;
using GymRatService.Common.Settings;
using GymRatService.Repository.Entities;
using GymRatService.Repository.Interfaces;
using GymRatService.Service.Exceptions;
using GymRatService.Service.Interfaces;
using GymRatService.Service.Security;
using GymRatService.Service.Services;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GymRatService.BLL.Tests.Services;

public class AuthServiceTests
{
    private readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private readonly IUsersRepository _repository = Substitute.For<IUsersRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenGenerator _tokenGenerator = Substitute.For<ITokenGenerator>();
    private readonly ILoginAttemptLimiter _loginAttemptLimiter = Substitute.For<ILoginAttemptLimiter>();
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        _passwordHasher.Hash(Arg.Any<string>()).Returns(new HashedPassword("hashed", "salt"));
        _service = new AuthService(_repository, _passwordHasher, _tokenGenerator, _loginAttemptLimiter);
    }

    [Fact]
    public async Task RegisterAsync_StoresTheUserWithAHashedPasswordAndANormalizedEmail()
    {
        // Arrange
        var request = new RegisterRequest(" Ana@Example.com ", "password123", " Ana ", " Pop ", WeightUnit.Pounds);
        User? stored = null;
        _repository.TryAddAsync(Arg.Do<User>(user => stored = user), Arg.Any<CancellationToken>()).Returns(true);

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
        Assert.InRange(stored.CreatedAt, Now, DateTimeOffset.UtcNow);
        _passwordHasher.Received(1).Hash("password123");
    }

    [Fact]
    public async Task RegisterAsync_StoresANullLastName_WhenItIsBlank()
    {
        // Arrange
        User? stored = null;
        _repository.TryAddAsync(Arg.Do<User>(user => stored = user), Arg.Any<CancellationToken>()).Returns(true);

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
        await _repository.DidNotReceive().TryAddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
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
        await _repository.Received(1).UpdateLastLoginAsync(user.Id, Arg.Is<DateTimeOffset>(at => at >= Now), Arg.Any<CancellationToken>());
        _loginAttemptLimiter.Received(1).RegisterAttempt("ana@example.com");
        _loginAttemptLimiter.Received(1).Reset("ana@example.com");
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
        _loginAttemptLimiter.Received(1).RegisterAttempt("ana@example.com");
        _loginAttemptLimiter.DidNotReceive().Reset(Arg.Any<string>());
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
        _loginAttemptLimiter.Received(1).RegisterAttempt("nobody@example.com");
        _loginAttemptLimiter.DidNotReceive().Reset(Arg.Any<string>());
    }

    [Fact]
    public async Task LoginAsync_ThrowsTooManyAttemptsWithoutCheckingThePassword_WhenTheEmailIsLimited()
    {
        // Arrange
        _loginAttemptLimiter
            .When(limiter => limiter.RegisterAttempt("ana@example.com"))
            .Do(_ => throw new RateLimitedException(ErrorCodes.TooManyAttempts, TimeSpan.FromMinutes(3)));

        // Act
        var exception = await Assert.ThrowsAsync<RateLimitedException>(
            () => _service.LoginAsync(new LoginRequest(" Ana@example.com", "password123"), CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.TooManyAttempts, exception.Code);
        Assert.Equal(TimeSpan.FromMinutes(3), exception.RetryAfter);
        await _repository.DidNotReceive().GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        _passwordHasher.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        _loginAttemptLimiter.DidNotReceive().Reset(Arg.Any<string>());
    }

    [Fact]
    public async Task LoginAsync_VerifiesThePasswordOnlyForTheAllowedAttempts_WhenMoreLoginsStartBeforeAnyCompletes()
    {
        // Arrange
        var settings = new AuthRateLimitSettings { FailedLoginLimit = 3, FailedLoginWindowSeconds = 900 };
        var limiter = new LoginAttemptLimiter(Options.Create(settings), TimeProvider.System);
        var service = new AuthService(_repository, _passwordHasher, _tokenGenerator, limiter);
        var user = new User { Email = "ana@example.com", PasswordHash = "hashed", PasswordSalt = "salt" };
        var lookup = new TaskCompletionSource<User?>();
        _repository.GetByEmailAsync("ana@example.com", Arg.Any<CancellationToken>()).Returns(lookup.Task);
        _passwordHasher.Verify("wrong-password", "hashed", "salt").Returns(false);

        // Act
        var logins = Enumerable.Range(0, 8)
            .Select(_ => service.LoginAsync(new LoginRequest("ana@example.com", "wrong-password"), CancellationToken.None))
            .ToList();
        lookup.SetResult(user);
        var exceptions = new List<BusinessLogicException>();

        foreach (var login in logins)
        {
            exceptions.Add(await Assert.ThrowsAnyAsync<BusinessLogicException>(() => login));
        }

        // Assert
        Assert.Equal(3, exceptions.Count(exception => exception.Code == ErrorCodes.InvalidCredentials));
        Assert.Equal(5, exceptions.Count(exception => exception.Code == ErrorCodes.TooManyAttempts));
        _passwordHasher.Received(3).Verify(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }
}
