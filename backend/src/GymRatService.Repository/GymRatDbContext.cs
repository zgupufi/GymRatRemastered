using GymRatService.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace GymRatService.Repository;

public class GymRatDbContext(DbContextOptions<GymRatDbContext> options) : DbContext(options)
{
    public DbSet<ExerciseCard> Exercises => Set<ExerciseCard>();
}
