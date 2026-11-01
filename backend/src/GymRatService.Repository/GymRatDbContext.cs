using GymRatService.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace GymRatService.Repository;

public class GymRatDbContext(DbContextOptions<GymRatDbContext> options) : DbContext(options)
{
    public DbSet<ExerciseCard> Exercises => Set<ExerciseCard>();

    public DbSet<User> Users => Set<User>();

    public DbSet<SplitDay> SplitDays => Set<SplitDay>();

    public DbSet<Workout> Workouts => Set<Workout>();

    public DbSet<WorkoutExercise> WorkoutExercises => Set<WorkoutExercise>();

    public DbSet<WorkoutSession> WorkoutSessions => Set<WorkoutSession>();

    public DbSet<SessionSet> SessionSets => Set<SessionSet>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GymRatDbContext).Assembly);
    }
}
