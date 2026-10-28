using GymRatService.Repository.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymRatService.Repository.Configurations;

public class WorkoutConfiguration : IEntityTypeConfiguration<Workout>
{
    public void Configure(EntityTypeBuilder<Workout> builder)
    {
        builder.ToTable("Workouts");

        builder.HasKey(workout => workout.Id);

        builder.Property(workout => workout.Id).ValueGeneratedOnAdd().HasDefaultValueSql("uuid_generate_v7()");

        builder.Property(workout => workout.Name).HasMaxLength(100).IsRequired();

        builder.Property(workout => workout.CreatedAt).IsRequired();

        builder.HasOne<User>().WithMany().HasForeignKey(workout => workout.UserId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(workout => workout.UserId);
    }
}
