using System.Net;
using System.Net.Http.Json;
using GymRatService.Common.DTOs.Exercises;
using GymRatService.Common.Errors;
using GymRatService.Common.Filtering;
using GymRatService.Repository.Entities;

namespace GymRatService.IntegrationTests.Exercises;

public sealed class ExercisesEndpointsTests : IDisposable
{
    private readonly GymRatApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task GetExercises_ReturnsUnauthorized_WhenTheCallerIsNotAuthenticated()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/exercises");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetExercises_ReturnsEveryExerciseOrderedByName()
    {
        // Arrange
        await _factory.SeedAsync(context => context.Exercises.AddRange(
            new ExerciseCard { Name = "Squat", MainMuscle = "Legs", Equipment = "Barbell" },
            new ExerciseCard { Name = "Bench Press", MainMuscle = "Chest", Difficulty = "Intermediate" }));
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/exercises");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<ExerciseDto>>();
        Assert.NotNull(page);
        Assert.Equal(new[] { 1, 20, 2 }, new[] { page.Page, page.PageSize, page.TotalCount });
        Assert.Collection(
            page.Items,
            first =>
            {
                Assert.Equal("Bench Press", first.Name);
                Assert.Equal("Chest", first.MainMuscle);
                Assert.Equal("Intermediate", first.Difficulty);
            },
            second =>
            {
                Assert.Equal("Squat", second.Name);
                Assert.Equal("Legs", second.MainMuscle);
                Assert.Equal("Barbell", second.Equipment);
            });
    }

    [Theory]
    [InlineData("?search.term=PRESS", new[] { "Bench Press", "Shoulder Press" })]
    [InlineData("?search.term=glutes", new[] { "Squat" })]
    [InlineData("?search.term=legs", new[] { "Squat" })]
    [InlineData("?muscle=chest", new[] { "Bench Press" })]
    [InlineData("?muscle=Shoulders&search.term=press", new[] { "Shoulder Press" })]
    [InlineData("?muscle=Legs&search.term=press", new string[0])]
    [InlineData("?search.term=%20&muscle=", new[] { "Bench Press", "Shoulder Press", "Squat" })]
    [InlineData("?sort.direction=descending", new[] { "Squat", "Shoulder Press", "Bench Press" })]
    [InlineData("?sort.by=mainMuscle", new[] { "Bench Press", "Squat", "Shoulder Press" })]
    [InlineData("?sort.by=mainMuscle&sort.direction=descending", new[] { "Shoulder Press", "Squat", "Bench Press" })]
    [InlineData("?pagination.pageSize=2", new[] { "Bench Press", "Shoulder Press" })]
    [InlineData("?pagination.page=2&pagination.pageSize=2", new[] { "Squat" })]
    [InlineData("?pagination.page=3&pagination.pageSize=2", new string[0])]
    public async Task GetExercises_FiltersByNameMuscleAndSubMuscle(string query, string[] expectedNames)
    {
        // Arrange
        await _factory.SeedAsync(context => context.Exercises.AddRange(
            new ExerciseCard { Name = "Squat", MainMuscle = "Legs", SubMuscles = "Glutes" },
            new ExerciseCard { Name = "Bench Press", MainMuscle = "Chest" },
            new ExerciseCard { Name = "Shoulder Press", MainMuscle = "Shoulders" }));
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync($"/api/exercises{query}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<ExerciseDto>>();
        Assert.Equal(expectedNames, page!.Items.Select(exercise => exercise.Name));
    }

    [Fact]
    public async Task GetExercises_ReturnsThePageTogetherWithTheTotalNumberOfMatches()
    {
        // Arrange
        await _factory.SeedAsync(context => context.Exercises.AddRange(
            new ExerciseCard { Name = "Squat", MainMuscle = "Legs" },
            new ExerciseCard { Name = "Bench Press", MainMuscle = "Chest" },
            new ExerciseCard { Name = "Shoulder Press", MainMuscle = "Shoulders" }));
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var page = await client.GetFromJsonAsync<PagedResult<ExerciseDto>>(
            "/api/exercises?search.term=press&pagination.page=2&pagination.pageSize=1");

        // Assert
        Assert.NotNull(page);
        Assert.Equal("Shoulder Press", Assert.Single(page.Items).Name);
        Assert.Equal(2, page.Page);
        Assert.Equal(1, page.PageSize);
        Assert.Equal(2, page.TotalCount);
    }

    [Theory]
    [InlineData("?pagination.page=0", "Pagination.Page")]
    [InlineData("?pagination.pageSize=101", "Pagination.PageSize")]
    [InlineData("?search.term=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "Search.Term")]
    [InlineData("?muscle=mmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmm", "Muscle")]
    public async Task GetExercises_ReturnsBadRequestWithValidationErrors_WhenTheFilterIsNotValid(string query, string expectedProperty)
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync($"/api/exercises{query}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(ErrorCodes.InvalidRequest, error?.Code);
        Assert.Equal(expectedProperty, Assert.Single(error!.Errors!).Property);
    }

    [Fact]
    public async Task GetExercise_ReturnsTheExercise_WhenItExists()
    {
        // Arrange
        var deadlift = new ExerciseCard { Name = "Deadlift", MainMuscle = "Back", SubMuscles = "Hamstrings" };
        await _factory.SeedAsync(context => context.Exercises.Add(deadlift));
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync($"/api/exercises/{deadlift.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var exercise = await response.Content.ReadFromJsonAsync<ExerciseDto>();
        Assert.Equal(
            new ExerciseDto(deadlift.Id, "Deadlift", "Back", "Hamstrings", null, null, null, null),
            exercise);
    }

    [Theory]
    [InlineData("en", "Exercise {0} does not exist.")]
    [InlineData("ro", "Exercițiul {0} nu există.")]
    public async Task GetExercise_ReturnsBadRequestWithALocalizedError_WhenItDoesNotExist(
        string language,
        string expectedMessageFormat)
    {
        // Arrange
        var missingId = Guid.CreateVersion7();
        await _factory.SeedAsync(_ => { });
        var client = _factory.CreateAuthenticatedClient();
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);

        // Act
        var response = await client.GetAsync($"/api/exercises/{missingId}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(
            new ErrorResponse(ErrorCodes.ExerciseNotFound, string.Format(expectedMessageFormat, missingId)),
            error);
    }
}
