using GymRatService.Common.DTOs.Exercises;
using GymRatService.Common.Errors;
using GymRatService.Repository.Entities;
using GymRatService.Repository.Interfaces;
using GymRatService.Service.Exceptions;
using GymRatService.Service.Services;
using NSubstitute;

namespace GymRatService.BLL.Tests.Services;

public class ExercisesServiceTests
{
    private readonly IExerciseCardsRepository _repository = Substitute.For<IExerciseCardsRepository>();
    private readonly ExercisesService _service;

    public ExercisesServiceTests()
    {
        _service = new ExercisesService(_repository);
    }

    [Fact]
    public async Task GetExercisesAsync_MapsEveryEntityToADto()
    {
        // Arrange
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new ExerciseCard { Id = 1, Name = "Bench Press", MainMuscle = "Chest", Equipment = "Barbell" },
            new ExerciseCard { Id = 2, Name = "Squat", MainMuscle = "Legs", Difficulty = "Intermediate" },
        ]);

        // Act
        var exercises = await _service.GetExercisesAsync(CancellationToken.None);

        // Assert
        Assert.Equal(
        [
            new ExerciseDto(1, "Bench Press", "Chest", null, "Barbell", null, null, null),
            new ExerciseDto(2, "Squat", "Legs", null, null, "Intermediate", null, null),
        ], exercises);
    }

    [Fact]
    public async Task GetExerciseAsync_ReturnsTheDto_WhenTheExerciseExists()
    {
        // Arrange
        _repository.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(new ExerciseCard
        {
            Id = 7,
            Name = "Deadlift",
            MainMuscle = "Back",
            SubMuscles = "Hamstrings",
            YoutubeLink = "https://youtu.be/deadlift",
        });

        // Act
        var exercise = await _service.GetExerciseAsync(7, CancellationToken.None);

        // Assert
        Assert.Equal(
            new ExerciseDto(7, "Deadlift", "Back", "Hamstrings", null, null, null, "https://youtu.be/deadlift"),
            exercise);
    }

    [Fact]
    public async Task GetExerciseAsync_ThrowsExerciseNotFound_WhenTheExerciseIsMissing()
    {
        // Arrange
        _repository.GetByIdAsync(99, Arg.Any<CancellationToken>()).Returns((ExerciseCard?)null);

        // Act
        var exception = await Assert.ThrowsAsync<ServiceException>(
            () => _service.GetExerciseAsync(99, CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.ExerciseNotFound, exception.Code);
    }
}
