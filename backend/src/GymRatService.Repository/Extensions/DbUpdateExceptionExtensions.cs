using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GymRatService.Repository.Extensions;

public static class DbUpdateExceptionExtensions
{
    public static bool IsUniqueViolation(this DbUpdateException exception)
    {
        return exception.InnerException switch
        {
            PostgresException postgresException => postgresException.SqlState == PostgresErrorCodes.UniqueViolation,
            { } inner => inner.Message.Contains("UNIQUE constraint failed", StringComparison.Ordinal),
            _ => false,
        };
    }
}
