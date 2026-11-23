using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GymRatService.IntegrationTests;

public sealed class QueryCounter : DbCommandInterceptor
{
    private int _count;

    public int Count => Volatile.Read(ref _count);

    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        Interlocked.Increment(ref _count);

        return result;
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _count);

        return ValueTask.FromResult(result);
    }
}
