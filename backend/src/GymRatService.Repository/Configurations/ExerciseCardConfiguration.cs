using GymRatService.Repository.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymRatService.Repository.Configurations;

public class ExerciseCardConfiguration : IEntityTypeConfiguration<ExerciseCard>
{
    public void Configure(EntityTypeBuilder<ExerciseCard> builder)
    {
        builder.ToTable("Exercises");

        builder.HasKey(exercise => exercise.Id);

        builder.Property(exercise => exercise.Id).ValueGeneratedNever();

        builder.Property(exercise => exercise.Name).HasMaxLength(100).IsRequired();

        builder.Property(exercise => exercise.MainMuscle).HasMaxLength(50).IsRequired();

        builder.Property(exercise => exercise.SubMuscles).HasMaxLength(200);

        builder.Property(exercise => exercise.Equipment).HasMaxLength(50);

        builder.Property(exercise => exercise.Difficulty).HasMaxLength(20);

        builder.Property(exercise => exercise.Description).HasMaxLength(1000);

        builder.Property(exercise => exercise.YoutubeLink).HasMaxLength(200);

        builder.HasIndex(exercise => exercise.Name).IsUnique();
    }
}
