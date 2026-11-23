using GymRatService.Common.DTOs.Users;
using GymRatService.Common.Enums;
using GymRatService.Common.Errors;
using GymRatService.Repository.Entities;
using GymRatService.Repository.Interfaces;
using GymRatService.Service.Exceptions;
using GymRatService.Service.Interfaces;
using GymRatService.Service.Security;
using GymRatService.Service.Services;
using NSubstitute;

namespace GymRatService.BLL.Tests.Services;

public class UsersServiceTests
{
    private readonly IUsersRepository _repository = Substitute.For<IUsersRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly ILoginAttemptLimiter _loginAttemptLimiter = Substitute.For<ILoginAttemptLimiter>();
    private readonly UsersService _service;

    public UsersServiceTests()
    {
        _service = new UsersService(_repository, _passwordHasher, _loginAttemptLimiter);
    }

    [Fact]
    public async Task GetProfileAsync_MapsTheUserToAProfile()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.CreateVersion7(),
            Email = "ana@example.com",
            FirstName = "Ana",
            LastName = "Pop",
            WeightUnit = WeightUnit.Pounds,
            DefaultRestTimeSeconds = 120,
        };
        _repository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        // Act
        var profile = await _service.GetProfileAsync(user.Id, CancellationToken.None);

        // Assert
        Assert.Equal(new UserProfileDto("Ana", "Pop", "ana@example.com", WeightUnit.Pounds, 120), profile);
    }

    [Fact]
    public async Task GetProfileAsync_ThrowsUserNotFound_WhenTheUserIsMissing()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((User?)null);

        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(
            () => _service.GetProfileAsync(id, CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.UserNotFound, exception.Code);
    }

    [Fact]
    public void NewUser_GetsTheDefaultPreferences()
    {
        // Act
        var user = new User();

        // Assert
        Assert.Equal(WeightUnit.Kilograms, user.WeightUnit);
        Assert.Equal(90, user.DefaultRestTimeSeconds);
    }

    [Theory]
    [InlineData(WeightUnit.Pounds, 120, WeightUnit.Pounds, 120)]
    [InlineData(WeightUnit.Pounds, null, WeightUnit.Pounds, 90)]
    [InlineData(null, 45, WeightUnit.Kilograms, 45)]
    [InlineData(null, null, WeightUnit.Kilograms, 90)]
    public async Task UpdateSettingsAsync_StoresTheGivenSettingsAndKeepsTheOthers(
        WeightUnit? weightUnit,
        int? defaultRestTimeSeconds,
        WeightUnit expectedWeightUnit,
        int expectedDefaultRestTimeSeconds)
    {
        // Arrange
        var user = new User { Id = Guid.CreateVersion7(), WeightUnit = WeightUnit.Kilograms, DefaultRestTimeSeconds = 90 };
        _repository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        // Act
        await _service.UpdateSettingsAsync(
            user.Id,
            new UpdateUserSettingsRequest(weightUnit, defaultRestTimeSeconds),
            CancellationToken.None);

        // Assert
        await _repository.Received(1).UpdateSettingsAsync(
            user.Id,
            expectedWeightUnit,
            expectedDefaultRestTimeSeconds,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateSettingsAsync_ThrowsUserNotFound_WhenTheUserIsMissing()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((User?)null);

        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(() => _service.UpdateSettingsAsync(
            id,
            new UpdateUserSettingsRequest(WeightUnit.Pounds, 120),
            CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.UserNotFound, exception.Code);
        await _repository.DidNotReceive().UpdateSettingsAsync(
            Arg.Any<Guid>(),
            Arg.Any<WeightUnit>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangePasswordAsync_StoresTheNewHash_WhenTheCurrentPasswordMatches()
    {
        // Arrange
        var user = new User { Id = Guid.CreateVersion7(), Email = "ana@example.com", PasswordHash = "old-hash", PasswordSalt = "old-salt" };
        _repository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("current-password", "old-hash", "old-salt").Returns(true);
        _passwordHasher.Hash("new-password").Returns(new HashedPassword("new-hash", "new-salt"));

        // Act
        await _service.ChangePasswordAsync(user.Id, new ChangePasswordRequest("current-password", "new-password"), CancellationToken.None);

        // Assert
        await _repository.Received(1).UpdatePasswordAsync(user.Id, "new-hash", "new-salt", Arg.Any<CancellationToken>());
        _loginAttemptLimiter.Received(1).RegisterAttempt("ana@example.com");
        _loginAttemptLimiter.Received(1).Reset("ana@example.com");
    }

    [Fact]
    public async Task ChangePasswordAsync_ThrowsInvalidPassword_WhenTheCurrentPasswordIsWrong()
    {
        // Arrange
        var user = new User { Id = Guid.CreateVersion7(), Email = "ana@example.com", PasswordHash = "old-hash", PasswordSalt = "old-salt" };
        _repository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(
            () => _service.ChangePasswordAsync(user.Id, new ChangePasswordRequest("wrong", "new-password"), CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.InvalidPassword, exception.Code);
        await _repository.DidNotReceive().UpdatePasswordAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        _loginAttemptLimiter.DidNotReceive().Reset(Arg.Any<string>());
    }

    [Fact]
    public async Task ChangePasswordAsync_ThrowsUserNotFound_WhenTheUserIsMissing()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((User?)null);

        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(
            () => _service.ChangePasswordAsync(id, new ChangePasswordRequest("current-password", "new-password"), CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.UserNotFound, exception.Code);
    }

    [Fact]
    public async Task DeleteAccountAsync_DeletesTheUser_WhenThePasswordMatches()
    {
        // Arrange
        var user = new User { Id = Guid.CreateVersion7(), Email = "ana@example.com", PasswordHash = "hash", PasswordSalt = "salt" };
        _repository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("password123", "hash", "salt").Returns(true);

        // Act
        await _service.DeleteAccountAsync(user.Id, new DeleteAccountRequest("password123"), CancellationToken.None);

        // Assert
        await _repository.Received(1).DeleteAsync(user.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAccountAsync_ThrowsInvalidPassword_WhenThePasswordIsWrong()
    {
        // Arrange
        var user = new User { Id = Guid.CreateVersion7(), Email = "ana@example.com", PasswordHash = "hash", PasswordSalt = "salt" };
        _repository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(
            () => _service.DeleteAccountAsync(user.Id, new DeleteAccountRequest("wrong"), CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.InvalidPassword, exception.Code);
        await _repository.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAccountAsync_ThrowsUserNotFound_WhenTheUserIsMissing()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((User?)null);

        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(
            () => _service.DeleteAccountAsync(id, new DeleteAccountRequest("password123"), CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.UserNotFound, exception.Code);
    }
}
