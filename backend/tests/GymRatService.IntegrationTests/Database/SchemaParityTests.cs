using GymRatService.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;

namespace GymRatService.IntegrationTests.Database;

[Trait("Category", "Postgres")]
public sealed class SchemaParityTests
{
    [PostgresFact]
    public async Task EntityModel_MatchesTheColumnsCreatedByLiquibase()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<GymRatDbContext>().UseNpgsql(GymRatApiFactory.PostgresConnection).Options;
        await using var context = new GymRatDbContext(options);
        var expected = context.Model.GetEntityTypes()
            .SelectMany(entityType => entityType.GetProperties().Select(property => ExpectedColumn(entityType, property)))
            .ToDictionary(column => column.Key, column => column.Value);

        // Act
        var actual = await ReadActualColumnsAsync();

        // Assert
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), actual.Keys.Order(StringComparer.Ordinal));
        var mismatches = expected
            .Where(column => actual[column.Key] != column.Value)
            .Select(column => $"{column.Key}: model {column.Value}, database {actual[column.Key]}")
            .ToList();
        Assert.Empty(mismatches);
    }

    [PostgresFact]
    public async Task EntityModel_MatchesTheForeignKeysCreatedByLiquibase()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<GymRatDbContext>().UseNpgsql(GymRatApiFactory.PostgresConnection).Options;
        await using var context = new GymRatDbContext(options);
        var expected = context.Model.GetEntityTypes()
            .SelectMany(entityType => entityType.GetForeignKeys())
            .Select(key => $"{key.DeclaringEntityType.GetTableName()}.{string.Join(",", key.Properties.Select(property => property.GetColumnName()))}->{key.PrincipalEntityType.GetTableName()}:{DeleteRule(key.DeleteBehavior)}")
            .Order(StringComparer.Ordinal)
            .ToList();

        // Act
        var actual = await ReadActualForeignKeysAsync();

        // Assert
        Assert.Equal(expected, actual);
    }

    private static KeyValuePair<string, string> ExpectedColumn(IEntityType entityType, IProperty property)
    {
        var storeType = (property.GetColumnType() ?? string.Empty).ToLowerInvariant();
        var parenthesis = storeType.IndexOf('(');
        var baseType = parenthesis >= 0 ? storeType[..parenthesis] : storeType;
        var nullability = property.IsNullable ? "null" : "not null";

        return new($"{entityType.GetTableName()}.{property.GetColumnName()}", $"{baseType} {nullability}");
    }

    private static string DeleteRule(DeleteBehavior behavior) => behavior switch
    {
        DeleteBehavior.Cascade => "CASCADE",
        DeleteBehavior.SetNull => "SET NULL",
        _ => "NO ACTION",
    };

    private static async Task<Dictionary<string, string>> ReadActualColumnsAsync()
    {
        await using var connection = new NpgsqlConnection(GymRatApiFactory.PostgresConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT table_name, column_name, data_type, is_nullable
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name NOT IN ('databasechangelog', 'databasechangeloglock')
            """;

        var columns = new Dictionary<string, string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var nullability = reader.GetString(3) == "YES" ? "null" : "not null";
            columns[$"{reader.GetString(0)}.{reader.GetString(1)}"] = $"{reader.GetString(2)} {nullability}";
        }

        return columns;
    }

    private static async Task<List<string>> ReadActualForeignKeysAsync()
    {
        await using var connection = new NpgsqlConnection(GymRatApiFactory.PostgresConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT child.relname,
                   (SELECT string_agg(attribute.attname, ',' ORDER BY keys.ordinality)
                    FROM unnest(constraint_row.conkey) WITH ORDINALITY AS keys(attnum, ordinality)
                    JOIN pg_attribute attribute ON attribute.attrelid = constraint_row.conrelid AND attribute.attnum = keys.attnum),
                   parent.relname,
                   CASE constraint_row.confdeltype WHEN 'c' THEN 'CASCADE' WHEN 'n' THEN 'SET NULL' ELSE 'NO ACTION' END
            FROM pg_constraint constraint_row
            JOIN pg_class child ON child.oid = constraint_row.conrelid
            JOIN pg_class parent ON parent.oid = constraint_row.confrelid
            JOIN pg_namespace namespace_row ON namespace_row.oid = child.relnamespace
            WHERE constraint_row.contype = 'f' AND namespace_row.nspname = 'public'
            """;

        var keys = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            keys.Add($"{reader.GetString(0)}.{reader.GetString(1)}->{reader.GetString(2)}:{reader.GetString(3)}");
        }

        return keys.Order(StringComparer.Ordinal).ToList();
    }
}
