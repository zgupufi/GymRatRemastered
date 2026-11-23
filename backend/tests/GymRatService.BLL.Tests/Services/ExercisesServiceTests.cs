using GymRatService.Common.Enums;
using GymRatService.Common.DTOs.Exercises;
using GymRatService.Common.Errors;
using GymRatService.Common.Filtering;
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
    public async Task GetExercisesAsync_MapsEveryEntityToADtoAndKeepsThePaging()
    {
        // Arrange
        var benchPress = new ExerciseCard { Name = "Bench Press", MainMuscle = MuscleGroup.Chest, Equipment = Equipment.Barbell };
        var squat = new ExerciseCard { Name = "Squat", MainMuscle = MuscleGroup.Legs, Difficulty = Difficulty.Intermediate };
        var filter = new ExerciseFilter { Muscle = "Chest" };
        _repository.GetAllAsync(filter, Arg.Any<CancellationToken>()).Returns(new PagedResult<ExerciseCard>([benchPress, squat], 1, 20, 2));

        // Act
        var exercises = await _service.GetExercisesAsync(filter, CancellationToken.None);

        // Assert
        Assert.Collection(
            exercises.Items,
            first =>
            {
                Assert.Equal(benchPress.Id, first.Id);
                Assert.Equal("Bench Press", first.Name);
                Assert.Equal(MuscleGroup.Chest, first.MainMuscle);
                Assert.Equal(Equipment.Barbell, first.Equipment);
            },
            second =>
            {
                Assert.Equal("Squat", second.Name);
                Assert.Equal(MuscleGroup.Legs, second.MainMuscle);
                Assert.Equal(Difficulty.Intermediate, second.Difficulty);
            });
        Assert.Equal(1, exercises.Page);
        Assert.Equal(20, exercises.PageSize);
        Assert.Equal(2, exercises.TotalCount);
    }

    [Fact]
    public async Task GetExerciseAsync_ReturnsTheDto_WhenTheExerciseExists()
    {
        // Arrange
        var deadlift = new ExerciseCard
        {
            Name = "Deadlift",
            MainMuscle = MuscleGroup.Back,
            SecondaryMuscles = [new ExerciseSecondaryMuscle { Muscle = Muscle.Hamstrings }],
            YoutubeLink = "https://youtu.be/deadlift",
        };
        _repository.GetByIdAsync(deadlift.Id, Arg.Any<CancellationToken>()).Returns(deadlift);

        // Act
        var exercise = await _service.GetExerciseAsync(deadlift.Id, CancellationToken.None);

        // Assert
        Assert.Equal(deadlift.Id, exercise.Id);
        Assert.Equal("Deadlift", exercise.Name);
        Assert.Equal(MuscleGroup.Back, exercise.MainMuscle);
        Assert.Equal([Muscle.Hamstrings], exercise.SecondaryMuscles);
        Assert.Equal("https://youtu.be/deadlift", exercise.YoutubeLink);
    }

    [Fact]
    public async Task GetExerciseAsync_ThrowsExerciseNotFound_WhenTheExerciseIsMissing()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((ExerciseCard?)null);

        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(
            () => _service.GetExerciseAsync(id, CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.ExerciseNotFound, exception.Code);
        Assert.Equal(id, Assert.Single(exception.Arguments));
    }
}
