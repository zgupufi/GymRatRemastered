using GymRatService.Common.Enums;
using GymRatService.Repository.Entities;
using GymRatService.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GymRatService.Repository.Repositories;

public class UsersRepository(GymRatDbContext context) : IUsersRepository
{
    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken)
    {
        return await context.Users.AnyAsync(user => user.Email == email, cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        return await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Email == email, cancellationToken);
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == id, cancellationToken);
    }

    public async Task<bool> TryAddAsync(User user, CancellationToken cancellationToken)
    {
        context.Users.Add(user);

        try
        {
            await context.SaveChangesAsync(cancellationToken);

            return true;
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            context.Entry(user).State = EntityState.Detached;

            return false;
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
    {
        return exception.InnerException switch
        {
            PostgresException postgresException => postgresException.SqlState == PostgresErrorCodes.UniqueViolation,
            { } inner => inner.Message.Contains("UNIQUE constraint failed", StringComparison.Ordinal),
            _ => false,
        };
    }

    public async Task UpdateLastLoginAsync(Guid id, DateTimeOffset lastLoginAt, CancellationToken cancellationToken)
    {
        await context.Users
            .Where(user => user.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.LastLoginAt, lastLoginAt), cancellationToken);
    }

    public async Task UpdateSettingsAsync(Guid id, WeightUnit weightUnit, int defaultRestTimeSeconds, CancellationToken cancellationToken)
    {
        await context.Users
            .Where(user => user.Id == id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(user => user.WeightUnit, weightUnit)
                    .SetProperty(user => user.DefaultRestTimeSeconds, defaultRestTimeSeconds),
                cancellationToken);
    }

    public async Task UpdatePasswordAndRevokeSessionsAsync(Guid id, string passwordHash, string passwordSalt, DateTimeOffset revokedAt, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        await context.Users
            .Where(user => user.Id == id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(user => user.PasswordHash, passwordHash)
                    .SetProperty(user => user.PasswordSalt, passwordSalt),
                cancellationToken);

        await context.RefreshTokens
            .Where(token => token.UserId == id && token.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedAt, revokedAt), cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await context.Users
            .Where(user => user.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
