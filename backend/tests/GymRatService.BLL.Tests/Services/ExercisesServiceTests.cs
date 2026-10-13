using System.Globalization;
using GymRatService.Common.DTOs.Exercises;
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

public class ExercisesServiceTests
{
    private readonly IExerciseCardsRepository _repository = Substitute.For<IExerciseCardsRepository>();
    private readonly ExercisesService _service;

    public ExercisesServiceTests()
    {
        var localizer = new ServiceCollection()
            .AddLogging()
            .AddServiceLayer(connectionString: null)
            .BuildServiceProvider()
            .GetRequiredService<IStringLocalizer<ExercisesService>>();

        _service = new ExercisesService(_repository, localizer);
    }

    [Fact]
    public async Task GetExercisesAsync_MapsEveryEntityToADto()
    {
        // Arrange
        var benchPress = new ExerciseCard { Name = "Bench Press", MainMuscle = "Chest", Equipment = "Barbell" };
        var squat = new ExerciseCard { Name = "Squat", MainMuscle = "Legs", Difficulty = "Intermediate" };
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([benchPress, squat]);

        // Act
        var exercises = await _service.GetExercisesAsync(CancellationToken.None);

        // Assert
        Assert.Equal(
        [
            new ExerciseDto(benchPress.Id, "Bench Press", "Chest", null, "Barbell", null, null, null),
            new ExerciseDto(squat.Id, "Squat", "Legs", null, null, "Intermediate", null, null),
        ], exercises);
    }

    [Fact]
    public async Task GetExerciseAsync_ReturnsTheDto_WhenTheExerciseExists()
    {
        // Arrange
        var deadlift = new ExerciseCard
        {
            Name = "Deadlift",
            MainMuscle = "Back",
            SubMuscles = "Hamstrings",
            YoutubeLink = "https://youtu.be/deadlift",
        };
        _repository.GetByIdAsync(deadlift.Id, Arg.Any<CancellationToken>()).Returns(deadlift);

        // Act
        var exercise = await _service.GetExerciseAsync(deadlift.Id, CancellationToken.None);

        // Assert
        Assert.Equal(
            new ExerciseDto(deadlift.Id, "Deadlift", "Back", "Hamstrings", null, null, null, "https://youtu.be/deadlift"),
            exercise);
    }

    [Theory]
    [InlineData("en", "Exercise {0} does not exist.")]
    [InlineData("ro", "Exercițiul {0} nu există.")]
    public async Task GetExerciseAsync_ThrowsLocalizedExerciseNotFound_WhenTheExerciseIsMissing(
        string culture,
        string expectedMessageFormat)
    {
        // Arrange
        var id = Guid.CreateVersion7();
        CultureInfo.CurrentUICulture = new CultureInfo(culture);
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((ExerciseCard?)null);

        // Act
        var exception = await Assert.ThrowsAsync<ServiceException>(
            () => _service.GetExerciseAsync(id, CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.ExerciseNotFound, exception.Code);
        Assert.Equal(string.Format(expectedMessageFormat, id), exception.Message);
    }

    [Fact]
    public void NewExerciseCard_GetsAVersion7Guid()
    {
        // Act
        var exercise = new ExerciseCard();

        // Assert
        Assert.Equal(7, exercise.Id.Version);
    }
}
