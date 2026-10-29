using GymRatService.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace GymRatService.Repository;

public class GymRatDbContext(DbContextOptions<GymRatDbContext> options) : DbContext(options)
{
    public DbSet<ExerciseCard> Exercises => Set<ExerciseCard>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Workout> Workouts => Set<Workout>();

    public DbSet<WorkoutExercise> WorkoutExercises => Set<WorkoutExercise>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GymRatDbContext).Assembly);
    }
}
