using System.Globalization;
using GymRatService.Common.DTOs.Users;
using GymRatService.Common.Enums;
using GymRatService.Common.Errors;
using GymRatService.Repository.Entities;
using GymRatService.Repository.Interfaces;
using GymRatService.Service;
using GymRatService.Service.Exceptions;
using GymRatService.Service.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;

namespace GymRatService.BLL.Tests.Services;

public class UsersServiceTests
{
    private readonly IUsersRepository _repository = Substitute.For<IUsersRepository>();
    private readonly UsersService _service;

    public UsersServiceTests()
    {
        var localizer = new ServiceCollection()
            .AddLogging()
            .AddServiceLayer(connectionString: null)
            .BuildServiceProvider()
            .GetRequiredService<IStringLocalizer<UsersService>>();

        _service = new UsersService(_repository, localizer);
    }

    [Fact]
    public async Task GetProfileAsync_MapsTheUserToAProfile()
    {
        // Arrange
        var user = new User
        {
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

    [Theory]
    [InlineData("en", "The user does not exist.")]
    [InlineData("ro", "Utilizatorul nu există.")]
    public async Task GetProfileAsync_ThrowsLocalizedUserNotFound_WhenTheUserIsMissing(string culture, string expectedMessage)
    {
        // Arrange
        var id = Guid.CreateVersion7();
        CultureInfo.CurrentUICulture = new CultureInfo(culture);
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((User?)null);

        // Act
        var exception = await Assert.ThrowsAsync<ServiceException>(
            () => _service.GetProfileAsync(id, CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.UserNotFound, exception.Code);
        Assert.Equal(expectedMessage, exception.Message);
    }

    [Fact]
    public void NewUser_GetsAVersion7GuidAndTheDefaultPreferences()
    {
        // Act
        var user = new User();

        // Assert
        Assert.Equal(7, user.Id.Version);
        Assert.Equal(WeightUnit.Kilograms, user.WeightUnit);
        Assert.Equal(90, user.DefaultRestTimeSeconds);
    }
}
