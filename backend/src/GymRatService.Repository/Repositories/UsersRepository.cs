using GymRatService.Repository.Entities;
using GymRatService.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

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

    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        context.Users.Add(user);

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateLastLoginAsync(Guid id, DateTimeOffset lastLoginAt, CancellationToken cancellationToken)
    {
        await context.Users
            .Where(user => user.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.LastLoginAt, lastLoginAt), cancellationToken);
    }
}
