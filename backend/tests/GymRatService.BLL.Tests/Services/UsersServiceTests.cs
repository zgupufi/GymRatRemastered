using FluentValidation;
using GymRatService.Common.DTOs.Users;
using GymRatService.Common.Enums;
using GymRatService.Common.Errors;
using GymRatService.Repository.Entities;
using GymRatService.Repository.Interfaces;
using GymRatService.Service.Exceptions;
using GymRatService.Service.Services;
using GymRatService.Service.Validators;
using NSubstitute;

namespace GymRatService.BLL.Tests.Services;

public class UsersServiceTests
{
    private readonly IUsersRepository _repository = Substitute.For<IUsersRepository>();
    private readonly UsersService _service;

    public UsersServiceTests()
    {
        _service = new UsersService(_repository, new UpdateUserSettingsRequestValidator());
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
    public async Task UpdateSettingsAsync_ThrowsEveryValidationError_WhenTheRequestIsInvalid()
    {
        // Act
        var exception = await Assert.ThrowsAsync<ValidationException>(() => _service.UpdateSettingsAsync(
            Guid.CreateVersion7(),
            new UpdateUserSettingsRequest((WeightUnit)5, 1000),
            CancellationToken.None));

        // Assert
        Assert.Equal(
            ["WeightUnit", "DefaultRestTimeSeconds"],
            exception.Errors.Select(error => error.PropertyName));
        await _repository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().UpdateSettingsAsync(
            Arg.Any<Guid>(),
            Arg.Any<WeightUnit>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }
}
