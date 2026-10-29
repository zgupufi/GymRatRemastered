using FluentValidation;
using GymRatService.Common.DTOs.Exercises;
using GymRatService.Common.Errors;
using GymRatService.Common.Filtering;
using GymRatService.Service.Validators;
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
        _service = new ExercisesService(_repository, new ExerciseFilterValidator());
    }

    [Fact]
    public async Task GetExercisesAsync_MapsEveryEntityToADtoAndKeepsThePaging()
    {
        // Arrange
        var benchPress = new ExerciseCard { Name = "Bench Press", MainMuscle = "Chest", Equipment = "Barbell" };
        var squat = new ExerciseCard { Name = "Squat", MainMuscle = "Legs", Difficulty = "Intermediate" };
        var filter = new ExerciseFilter { Muscle = "Chest" };
        _repository.GetAllAsync(filter, Arg.Any<CancellationToken>()).Returns(new PagedResult<ExerciseCard>([benchPress, squat], 1, 20, 2));

        // Act
        var exercises = await _service.GetExercisesAsync(filter, CancellationToken.None);

        // Assert
        Assert.Equal(
        [
            new ExerciseDto(benchPress.Id, "Bench Press", "Chest", null, "Barbell", null, null, null),
            new ExerciseDto(squat.Id, "Squat", "Legs", null, null, "Intermediate", null, null),
        ], exercises.Items);
        Assert.Equal(1, exercises.Page);
        Assert.Equal(20, exercises.PageSize);
        Assert.Equal(2, exercises.TotalCount);
    }

    [Fact]
    public async Task GetExercisesAsync_ThrowsAValidationException_WhenThePageSizeIsTooLarge()
    {
        // Arrange
        var filter = new ExerciseFilter { Pagination = new PaginationFilter { PageSize = 101 } };

        // Act
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _service.GetExercisesAsync(filter, CancellationToken.None));

        // Assert
        Assert.Equal("Pagination.PageSize", Assert.Single(exception.Errors).PropertyName);
        await _repository.DidNotReceiveWithAnyArgs().GetAllAsync(default!, default);
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
