using GymRatService.Common.Enums;
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
            new ExerciseCard { Name = "Squat", MainMuscle = MuscleGroup.Legs, Equipment = Equipment.Barbell },
            new ExerciseCard { Name = "Bench Press", MainMuscle = MuscleGroup.Chest, Difficulty = Difficulty.Intermediate }));
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
                Assert.Equal(MuscleGroup.Chest, first.MainMuscle);
                Assert.Equal(Difficulty.Intermediate, first.Difficulty);
            },
            second =>
            {
                Assert.Equal("Squat", second.Name);
                Assert.Equal(MuscleGroup.Legs, second.MainMuscle);
                Assert.Equal(Equipment.Barbell, second.Equipment);
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
            new ExerciseCard { Name = "Squat", MainMuscle = MuscleGroup.Legs, SecondaryMuscles = [new ExerciseSecondaryMuscle { Muscle = Muscle.Glutes }] },
            new ExerciseCard { Name = "Bench Press", MainMuscle = MuscleGroup.Chest },
            new ExerciseCard { Name = "Shoulder Press", MainMuscle = MuscleGroup.Shoulders }));
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
            new ExerciseCard { Name = "Squat", MainMuscle = MuscleGroup.Legs },
            new ExerciseCard { Name = "Bench Press", MainMuscle = MuscleGroup.Chest },
            new ExerciseCard { Name = "Shoulder Press", MainMuscle = MuscleGroup.Shoulders }));
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
        var deadlift = new ExerciseCard { Name = "Deadlift", MainMuscle = MuscleGroup.Back, SecondaryMuscles = [new ExerciseSecondaryMuscle { Muscle = Muscle.Hamstrings }], MovementPattern = MovementPattern.Hinge };
        await _factory.SeedAsync(context => context.Exercises.Add(deadlift));
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync($"/api/exercises/{deadlift.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var exercise = await response.Content.ReadFromJsonAsync<ExerciseDto>();
        Assert.NotNull(exercise);
        Assert.Equal(deadlift.Id, exercise.Id);
        Assert.Equal("Deadlift", exercise.Name);
        Assert.Equal(MuscleGroup.Back, exercise.MainMuscle);
        Assert.Equal([Muscle.Hamstrings], exercise.SecondaryMuscles);
        Assert.Equal(MovementPattern.Hinge, exercise.MovementPattern);
        Assert.Null(exercise.Setup);
    }

    [Theory]
    [InlineData("en", "Deadlift", "Pull the bar from the floor.", "Set up over the bar.", "Stand up with the bar.")]
    [InlineData("ro", "Îndreptări", "Ridică bara de pe podea.", "Așază-te deasupra barei.", "Ridică-te cu bara.")]
    [InlineData("fr", "Deadlift", "Pull the bar from the floor.", "Set up over the bar.", "Stand up with the bar.")]
    public async Task GetExercise_ReturnsTheTranslationForTheRequestedCultureOrEnglish(
        string language,
        string expectedName,
        string expectedDescription,
        string expectedSetup,
        string expectedExecution)
    {
        // Arrange
        var deadlift = new ExerciseCard
        {
            Name = "Deadlift",
            MainMuscle = MuscleGroup.Back,
            Translations =
            [
                new ExerciseTranslation { Culture = "en", Name = "Deadlift", Description = "Pull the bar from the floor.", Setup = "Set up over the bar.", Execution = "Stand up with the bar." },
                new ExerciseTranslation { Culture = "ro", Name = "Îndreptări", Description = "Ridică bara de pe podea.", Setup = "Așază-te deasupra barei.", Execution = "Ridică-te cu bara." },
            ],
        };
        await _factory.SeedAsync(context => context.Exercises.Add(deadlift));
        var client = _factory.CreateAuthenticatedClient();
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);

        // Act
        var exercise = await client.GetFromJsonAsync<ExerciseDto>($"/api/exercises/{deadlift.Id}");

        // Assert
        Assert.NotNull(exercise);
        Assert.Equal(expectedName, exercise.Name);
        Assert.Equal(expectedDescription, exercise.Description);
        Assert.Equal(expectedSetup, exercise.Setup);
        Assert.Equal(expectedExecution, exercise.Execution);
    }

    [Theory]
    [InlineData("?equipment=Barbell", new[] { "Bench Press", "Squat" })]
    [InlineData("?equipment=cable", new[] { "Cable Fly" })]
    [InlineData("?pattern=Isolation", new[] { "Cable Fly" })]
    [InlineData("?pattern=push&equipment=barbell", new[] { "Bench Press" })]
    [InlineData("?muscle=Triceps", new string[0])]
    [InlineData("?muscle=NotAMuscle", new string[0])]
    [InlineData("?search.term=front%20delt", new[] { "Bench Press" })]
    [InlineData("?search.term=chest", new[] { "Bench Press", "Cable Fly" })]
    public async Task GetExercises_FiltersByEquipmentPatternAndMuscleNames(string query, string[] expectedNames)
    {
        // Arrange
        await _factory.SeedAsync(context => context.Exercises.AddRange(
            new ExerciseCard
            {
                Name = "Bench Press",
                MainMuscle = MuscleGroup.Chest,
                Equipment = Equipment.Barbell,
                MovementPattern = MovementPattern.Push,
                SecondaryMuscles = [new ExerciseSecondaryMuscle { Muscle = Muscle.FrontDeltoids }],
            },
            new ExerciseCard { Name = "Squat", MainMuscle = MuscleGroup.Legs, Equipment = Equipment.Barbell, MovementPattern = MovementPattern.Squat },
            new ExerciseCard { Name = "Cable Fly", MainMuscle = MuscleGroup.Chest, Equipment = Equipment.Cable, MovementPattern = MovementPattern.Isolation }));
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var page = await client.GetFromJsonAsync<PagedResult<ExerciseDto>>($"/api/exercises{query}");

        // Assert
        Assert.Equal(expectedNames, page!.Items.Select(exercise => exercise.Name));
    }

    [Fact]
    public async Task GetExercises_ReturnsBadRequest_WhenTheEquipmentIsNotKnown()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/exercises?equipment=Spaceship");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
