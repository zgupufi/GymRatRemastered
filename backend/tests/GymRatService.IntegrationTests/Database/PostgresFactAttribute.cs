namespace GymRatService.IntegrationTests.Database;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (!GymRatApiFactory.UsesPostgres)
        {
            Skip = $"Set {GymRatApiFactory.PostgresConnectionVariable} to run against PostgreSQL.";
        }
    }
}
