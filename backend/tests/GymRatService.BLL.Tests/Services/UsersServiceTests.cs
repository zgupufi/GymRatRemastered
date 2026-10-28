using GymRatService.Common.DTOs.Users;
using GymRatService.Common.Enums;
using GymRatService.Common.Errors;
using GymRatService.Repository.Entities;
using GymRatService.Repository.Interfaces;
using GymRatService.Service.Exceptions;
using GymRatService.Service.Services;
using NSubstitute;

namespace GymRatService.BLL.Tests.Services;

public class UsersServiceTests
{
    private readonly IUsersRepository _repository = Substitute.For<IUsersRepository>();
    private readonly UsersService _service;

    public UsersServiceTests()
    {
        _service = new UsersService(_repository);
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
}
