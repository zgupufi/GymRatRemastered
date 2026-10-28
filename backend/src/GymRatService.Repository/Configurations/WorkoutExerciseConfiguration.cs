using GymRatService.Repository.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymRatService.Repository.Configurations;

public class WorkoutExerciseConfiguration : IEntityTypeConfiguration<WorkoutExercise>
{
    public void Configure(EntityTypeBuilder<WorkoutExercise> builder)
    {
        builder.ToTable("WorkoutExercises");

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Id).ValueGeneratedOnAdd().HasDefaultValueSql("uuid_generate_v7()");

        builder.Property(entry => entry.Position).IsRequired();

        builder.Property(entry => entry.TargetSets).IsRequired();

        builder.HasOne<Workout>().WithMany(workout => workout.Exercises).HasForeignKey(entry => entry.WorkoutId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(entry => entry.Exercise).WithMany().HasForeignKey(entry => entry.ExerciseId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(entry => new { entry.WorkoutId, entry.Position });
    }
}
