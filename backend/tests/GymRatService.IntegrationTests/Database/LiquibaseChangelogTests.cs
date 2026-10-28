using System.Text.RegularExpressions;
using GymRatService.Repository;
using GymRatService.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace GymRatService.IntegrationTests.Database;

public sealed partial class LiquibaseChangelogTests
{
    private const int CatalogSize = 89;

    private static readonly string ChangelogDirectory = Path.Combine(FindRepositoryRoot(), "database", "changelog");

    [Fact]
    public void MasterChangelog_IncludesEveryChangeFileInOrder()
    {
        // Act
        var master = File.ReadAllText(Path.Combine(ChangelogDirectory, "db.changelog-master.xml"));

        // Assert
        var included = IncludedFileRegex().Matches(master).Select(match => match.Groups[1].Value).ToList();
        var onDisk = Directory.GetFiles(Path.Combine(ChangelogDirectory, "changes"))
            .Select(path => $"changes/{Path.GetFileName(path)}")
            .Order()
            .ToList();
        Assert.Equal(onDisk, included);
    }

    [Fact]
    public void InitialSchema_DefinesEveryColumnOfTheExerciseEntity()
    {
        // Arrange
        var schema = File.ReadAllText(Path.Combine(ChangelogDirectory, "changes", "001-initial-schema.xml"));
        var options = new DbContextOptionsBuilder<GymRatDbContext>()
            .UseNpgsql("Host=localhost;Database=gymrat_model_check")
            .Options;
        using var context = new GymRatDbContext(options);
        var entityType = context.Model.FindEntityType(typeof(ExerciseCard))!;

        // Act
        var columns = entityType.GetProperties().Select(property => property.GetColumnName()).ToList();

        // Assert
        Assert.Equal("Exercises", entityType.GetTableName());
        Assert.Equal(8, columns.Count);
        Assert.All(columns, column => Assert.Contains($"<column name=\"{column}\"", schema));
    }

    [Fact]
    public void UsersSchema_DefinesEveryColumnOfTheUserEntity()
    {
        // Arrange
        var schema = File.ReadAllText(Path.Combine(ChangelogDirectory, "changes", "003-users.xml"));
        var options = new DbContextOptionsBuilder<GymRatDbContext>()
            .UseNpgsql("Host=localhost;Database=gymrat_model_check")
            .Options;
        using var context = new GymRatDbContext(options);
        var entityType = context.Model.FindEntityType(typeof(User))!;

        // Act
        var columns = entityType.GetProperties().Select(property => property.GetColumnName()).ToList();

        // Assert
        Assert.Equal("Users", entityType.GetTableName());
        Assert.Equal(10, columns.Count);
        Assert.All(columns, column => Assert.Contains($"<column name=\"{column}\"", schema));
        Assert.Contains("indexName=\"IX_Users_Email\" unique=\"true\"", schema);
        Assert.Contains("CREATE OR REPLACE FUNCTION uuid_generate_v7()", schema);
        Assert.Contains("<column name=\"Id\" type=\"uuid\" defaultValueComputed=\"uuid_generate_v7()\">", schema);
        Assert.Contains("<addDefaultValue tableName=\"Exercises\" columnName=\"Id\" defaultValueComputed=\"uuid_generate_v7()\" />", schema);
    }

    [Fact]
    public void SeedChangelog_HasEveryExerciseWithUniqueGuidV7IdsAndNames()
    {
        // Arrange
        var seed = File.ReadAllText(Path.Combine(ChangelogDirectory, "data", "exercise-catalog.csv"));

        // Act
        var rows = SeedRowRegex().Matches(seed).Select(match => (Id: Guid.Parse(match.Groups[1].Value), Name: match.Groups[2].Value)).ToList();

        // Assert
        Assert.Equal(CatalogSize, rows.Count);
        Assert.All(rows, row => Assert.Equal(7, row.Id.Version));
        Assert.Equal(CatalogSize, rows.Select(row => row.Id).Distinct().Count());
        Assert.Equal(CatalogSize, rows.Select(row => row.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains(rows, row => row.Name == "Bench Press");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "infra")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("infra directory was not found above the test output directory.");
    }

    [GeneratedRegex("<include file=\"([^\"]+)\"")]
    private static partial Regex IncludedFileRegex();

    [GeneratedRegex("^\"([0-9a-f-]{36})\",\"((?:[^\"]|\"\")*)\"", RegexOptions.Multiline)]
    private static partial Regex SeedRowRegex();
}
