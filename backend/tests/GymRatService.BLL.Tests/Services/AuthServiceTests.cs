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
    private readonly IRefreshTokensRepository _refreshTokens = Substitute.For<IRefreshTokensRepository>();
    private readonly IRefreshTokenGenerator _refreshTokenGenerator = Substitute.For<IRefreshTokenGenerator>();
    private readonly RefreshTokenSettings _refreshSettings = new();
    private readonly AdjustableTimeProvider _time;
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        _passwordHasher.Hash(Arg.Any<string>()).Returns(new HashedPassword("hashed", "salt"));
        _refreshTokenGenerator.Generate().Returns(new GeneratedRefreshToken("new-token", "new-hash"));
        _refreshTokenGenerator.Hash("old-token").Returns("old-hash");
        _refreshTokens.TryRotateAsync(Arg.Any<Guid>(), Arg.Any<RefreshToken>(), Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(true);
        _time = new AdjustableTimeProvider(Now);
        _service = CreateService(_loginAttemptLimiter);
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
        var token = new LoginResponse("access-token", Now.AddMinutes(15));
        RefreshToken? issued = null;
        await _refreshTokens.AddAsync(Arg.Do<RefreshToken>(stored => issued = stored), Arg.Any<CancellationToken>());
        _repository.GetByEmailAsync("ana@example.com", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("password123", "hashed", "salt").Returns(true);
        _tokenGenerator.Generate(user).Returns(token);

        // Act
        var response = await _service.LoginAsync(new LoginRequest(" ANA@example.com", "password123"), CancellationToken.None);

        // Assert
        Assert.Equal(new AuthSession(token, "new-token", Now.AddDays(30)), response);
        Assert.NotNull(issued);
        Assert.Equal(user.Id, issued.UserId);
        Assert.Equal("new-hash", issued.TokenHash);
        Assert.Equal(Now, issued.CreatedAt);
        Assert.Equal(Now.AddDays(30), issued.ExpiresAt);
        Assert.Equal(7, issued.FamilyId.Version);
        await _repository.Received(1).UpdateLastLoginAsync(user.Id, Now, Arg.Any<CancellationToken>());
        await _refreshTokens.Received(1).DeleteStaleAsync(user.Id, Now, TimeSpan.FromDays(1), Arg.Any<CancellationToken>());
        Assert.Equal(Now, issued.SessionStartedAt);
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
        var service = CreateService(limiter);
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

    [Fact]
    public async Task RefreshAsync_RotatesTheTokenInTheSameFamilyAndSlidesTheExpiry_WhenTheTokenIsValid()
    {
        // Arrange
        var user = new User { Id = Guid.CreateVersion7(), Email = "ana@example.com" };
        var stored = StoredToken(user.Id);
        var access = new LoginResponse("access-token", Now.AddMinutes(15));
        RefreshToken? issued = null;
        _refreshTokens.GetByHashAsync("old-hash", Arg.Any<CancellationToken>()).Returns(stored);
        _repository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _tokenGenerator.Generate(user).Returns(access);
        _refreshTokens
            .TryRotateAsync(stored.Id, Arg.Do<RefreshToken>(token => issued = token), Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _time.Advance(TimeSpan.FromDays(10));

        // Act
        var response = await _service.RefreshAsync("old-token", CancellationToken.None);

        // Assert
        Assert.Equal(new AuthSession(access, "new-token", Now.AddDays(40)), response);
        Assert.NotNull(issued);
        Assert.Equal(stored.FamilyId, issued.FamilyId);
        Assert.Equal("new-hash", issued.TokenHash);
        Assert.Equal(stored.SessionStartedAt, issued.SessionStartedAt);
        Assert.Equal(user.Id, issued.UserId);
        await _refreshTokens.Received(1).TryRotateAsync(stored.Id, issued, Now.AddDays(10), 2, Arg.Any<CancellationToken>());
        await _refreshTokens.DidNotReceive().RevokeFamilyAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task RefreshAsync_ThrowsInvalidRefreshToken_WhenThereIsNoToken(string? token)
    {
        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(() => _service.RefreshAsync(token, CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.InvalidRefreshToken, exception.Code);
        await _refreshTokens.DidNotReceive().GetByHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_ThrowsInvalidRefreshToken_WhenTheTokenIsUnknown()
    {
        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(() => _service.RefreshAsync("old-token", CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.InvalidRefreshToken, exception.Code);
        await _refreshTokens.DidNotReceive().TryRotateAsync(Arg.Any<Guid>(), Arg.Any<RefreshToken>(), Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_ThrowsInvalidRefreshToken_WhenTheTokenIsRevoked()
    {
        // Arrange
        var stored = StoredToken(Guid.CreateVersion7());
        stored.RevokedAt = Now;
        _refreshTokens.GetByHashAsync("old-hash", Arg.Any<CancellationToken>()).Returns(stored);

        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(() => _service.RefreshAsync("old-token", CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.InvalidRefreshToken, exception.Code);
        await _refreshTokens.DidNotReceive().TryRotateAsync(Arg.Any<Guid>(), Arg.Any<RefreshToken>(), Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_ThrowsInvalidRefreshToken_WhenTheTokenHasExpired()
    {
        // Arrange
        _refreshTokens.GetByHashAsync("old-hash", Arg.Any<CancellationToken>()).Returns(StoredToken(Guid.CreateVersion7()));
        _time.Advance(TimeSpan.FromDays(30));

        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(() => _service.RefreshAsync("old-token", CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.InvalidRefreshToken, exception.Code);
        await _refreshTokens.DidNotReceive().TryRotateAsync(Arg.Any<Guid>(), Arg.Any<RefreshToken>(), Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_RevokesTheFamily_WhenARotatedTokenIsPresentedAfterTheGracePeriod()
    {
        // Arrange
        var stored = StoredToken(Guid.CreateVersion7());
        stored.RotatedAt = Now;
        _refreshTokens.GetByHashAsync("old-hash", Arg.Any<CancellationToken>()).Returns(stored);
        _time.Advance(TimeSpan.FromSeconds(11));

        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(() => _service.RefreshAsync("old-token", CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.InvalidRefreshToken, exception.Code);
        await _refreshTokens.Received(1).RevokeFamilyAsync(stored.FamilyId, Now.AddSeconds(11), Arg.Any<CancellationToken>());
        await _refreshTokens.DidNotReceive().TryRotateAsync(Arg.Any<Guid>(), Arg.Any<RefreshToken>(), Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_IssuesAnotherTokenWithoutRevoking_WhenARotatedTokenIsPresentedWithinTheGracePeriod()
    {
        // Arrange
        var user = new User { Id = Guid.CreateVersion7(), Email = "ana@example.com" };
        var stored = StoredToken(user.Id);
        stored.RotatedAt = Now;
        _refreshTokens.GetByHashAsync("old-hash", Arg.Any<CancellationToken>()).Returns(stored);
        _repository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _tokenGenerator.Generate(user).Returns(new LoginResponse("access-token", Now.AddMinutes(15)));
        _time.Advance(TimeSpan.FromSeconds(5));

        // Act
        var response = await _service.RefreshAsync("old-token", CancellationToken.None);

        // Assert
        Assert.Equal("new-token", response.RefreshToken);
        await _refreshTokens.DidNotReceive().RevokeFamilyAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_ThrowsInvalidRefreshToken_WhenTheUserNoLongerExists()
    {
        // Arrange
        _refreshTokens.GetByHashAsync("old-hash", Arg.Any<CancellationToken>()).Returns(StoredToken(Guid.CreateVersion7()));

        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(() => _service.RefreshAsync("old-token", CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.InvalidRefreshToken, exception.Code);
        await _refreshTokens.DidNotReceive().TryRotateAsync(Arg.Any<Guid>(), Arg.Any<RefreshToken>(), Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_ThrowsInvalidRefreshToken_WhenAnotherRequestAlreadyClaimedTheToken()
    {
        // Arrange
        var user = new User { Id = Guid.CreateVersion7(), Email = "ana@example.com" };
        _refreshTokens.GetByHashAsync("old-hash", Arg.Any<CancellationToken>()).Returns(StoredToken(user.Id));
        _repository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _refreshTokens
            .TryRotateAsync(Arg.Any<Guid>(), Arg.Any<RefreshToken>(), Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(() => _service.RefreshAsync("old-token", CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.InvalidRefreshToken, exception.Code);
        _tokenGenerator.DidNotReceive().Generate(Arg.Any<User>());
        await _refreshTokens.DidNotReceive().RevokeFamilyAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_RevokesTheNewTokenAndThrows_WhenTheFamilyWasRevokedWhileRotating()
    {
        // Arrange
        var user = new User { Id = Guid.CreateVersion7(), Email = "ana@example.com" };
        var stored = StoredToken(user.Id);
        _refreshTokens.GetByHashAsync("old-hash", Arg.Any<CancellationToken>()).Returns(stored);
        _repository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _refreshTokens.IsFamilyRevokedAsync(stored.FamilyId, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(() => _service.RefreshAsync("old-token", CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.InvalidRefreshToken, exception.Code);
        await _refreshTokens.Received(1).RevokeFamilyAsync(stored.FamilyId, Now, Arg.Any<CancellationToken>());
        _tokenGenerator.DidNotReceive().Generate(Arg.Any<User>());
    }

    [Fact]
    public async Task RefreshAsync_CapsTheExpiryAtTheAbsoluteSessionLifetime()
    {
        // Arrange
        var user = new User { Id = Guid.CreateVersion7(), Email = "ana@example.com" };
        var stored = StoredToken(user.Id);
        stored.ExpiresAt = Now.AddDays(80);
        RefreshToken? issued = null;
        _refreshTokens.GetByHashAsync("old-hash", Arg.Any<CancellationToken>()).Returns(stored);
        _repository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _tokenGenerator.Generate(user).Returns(new LoginResponse("access-token", Now.AddMinutes(15)));
        _refreshTokens
            .TryRotateAsync(stored.Id, Arg.Do<RefreshToken>(token => issued = token), Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _time.Advance(TimeSpan.FromDays(70));

        // Act
        var response = await _service.RefreshAsync("old-token", CancellationToken.None);

        // Assert
        Assert.Equal(Now.AddDays(90), response.RefreshTokenExpiresAt);
        Assert.NotNull(issued);
        Assert.Equal(Now.AddDays(90), issued.ExpiresAt);
    }

    [Fact]
    public async Task LogoutAsync_RevokesTheWholeFamily_WhenTheTokenIsKnown()
    {
        // Arrange
        var stored = StoredToken(Guid.CreateVersion7());
        _refreshTokens.GetByHashAsync("old-hash", Arg.Any<CancellationToken>()).Returns(stored);

        // Act
        await _service.LogoutAsync("old-token", CancellationToken.None);

        // Assert
        await _refreshTokens.Received(1).RevokeFamilyAsync(stored.FamilyId, Now, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("old-token")]
    public async Task LogoutAsync_DoesNothing_WhenThereIsNoKnownToken(string? token)
    {
        // Act
        await _service.LogoutAsync(token, CancellationToken.None);

        // Assert
        await _refreshTokens.DidNotReceive().RevokeFamilyAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    private AuthService CreateService(ILoginAttemptLimiter limiter)
    {
        return new AuthService(
            _repository,
            _refreshTokens,
            _passwordHasher,
            _tokenGenerator,
            _refreshTokenGenerator,
            limiter,
            Options.Create(_refreshSettings),
            _time);
    }

    private RefreshToken StoredToken(Guid userId)
    {
        return new RefreshToken
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            FamilyId = Guid.CreateVersion7(),
            TokenHash = "old-hash",
            SessionStartedAt = Now,
            CreatedAt = Now,
            ExpiresAt = Now.AddDays(30),
        };
    }
}
