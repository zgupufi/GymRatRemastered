using GymRatService.Common.Enums;
using GymRatService.Common.Filtering;
using GymRatService.Service.Validators;

namespace GymRatService.BLL.Tests.Validators;

public class FilterValidatorsTests
{
    private readonly ExerciseFilterValidator _exerciseValidator = new();
    private readonly WorkoutFilterValidator _workoutValidator = new();

    [Fact]
    public async Task Validate_Passes_ForTheDefaultFilter()
    {
        // Act
        var exercise = await _exerciseValidator.ValidateAsync(new ExerciseFilter());
        var workout = await _workoutValidator.ValidateAsync(new WorkoutFilter());

        // Assert
        Assert.True(exercise.IsValid);
        Assert.True(workout.IsValid);
    }

    [Fact]
    public async Task Validate_Passes_ForAFullyPopulatedFilter()
    {
        // Arrange
        var filter = new ExerciseFilter
        {
            Muscle = "Chest",
            Search = new SearchFilter { Term = "press" },
            Sort = new SortFilter<ExerciseSortField> { By = ExerciseSortField.MainMuscle, Direction = SortDirection.Descending },
            Pagination = new PaginationFilter { Page = 3, PageSize = 100 },
        };

        // Act
        var result = await _exerciseValidator.ValidateAsync(filter);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Validate_Fails_WhenThePageIsBelowOne(int page)
    {
        // Act
        var result = await _workoutValidator.ValidateAsync(new WorkoutFilter { Pagination = new PaginationFilter { Page = page } });

        // Assert
        Assert.Equal("Pagination.Page", Assert.Single(result.Errors).PropertyName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Validate_Fails_WhenThePageSizeIsOutOfRange(int pageSize)
    {
        // Act
        var result = await _workoutValidator.ValidateAsync(new WorkoutFilter { Pagination = new PaginationFilter { PageSize = pageSize } });

        // Assert
        Assert.Equal("Pagination.PageSize", Assert.Single(result.Errors).PropertyName);
    }

    [Fact]
    public async Task Validate_Fails_WhenTheSortFieldOrDirectionIsNotDefined()
    {
        // Arrange
        var filter = new WorkoutFilter
        {
            Sort = new SortFilter<WorkoutSortField> { By = (WorkoutSortField)9, Direction = (SortDirection)7 },
        };

        // Act
        var result = await _workoutValidator.ValidateAsync(filter);

        // Assert
        Assert.Equal(["Sort.By", "Sort.Direction"], result.Errors.Select(error => error.PropertyName));
    }

    [Fact]
    public async Task Validate_Fails_WhenTheSearchTermOrMuscleIsTooLong()
    {
        // Arrange
        var filter = new ExerciseFilter { Muscle = new string('m', 51), Search = new SearchFilter { Term = new string('s', 101) } };

        // Act
        var result = await _exerciseValidator.ValidateAsync(filter);

        // Assert
        Assert.Equal(["Search.Term", "Muscle"], result.Errors.Select(error => error.PropertyName));
    }

    [Fact]
    public void Skip_IsTheNumberOfItemsOnThePreviousPages()
    {
        // Act
        var skip = new PaginationFilter { Page = 3, PageSize = 20 }.Skip;

        // Assert
        Assert.Equal(40, skip);
    }

    [Fact]
    public void Map_KeepsThePagingAndConvertsEveryItem()
    {
        // Arrange
        var page = new PagedResult<int>([1, 2], 2, 2, 5);

        // Act
        var mapped = page.Map(number => number.ToString());

        // Assert
        Assert.Equal(["1", "2"], mapped.Items);
        Assert.Equal(2, mapped.Page);
        Assert.Equal(2, mapped.PageSize);
        Assert.Equal(5, mapped.TotalCount);
    }
}
