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

        builder.Property(exercise => exercise.Id).ValueGeneratedOnAdd().HasDefaultValueSql("uuid_generate_v7()");

        builder.Property(exercise => exercise.Name).HasMaxLength(100).IsRequired();

        builder.Property(exercise => exercise.MainMuscle).HasConversion<string>().HasMaxLength(50).IsRequired();

        builder.Property(exercise => exercise.Equipment).HasConversion<string>().HasMaxLength(50).IsRequired();

        builder.Property(exercise => exercise.Difficulty).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(exercise => exercise.MovementPattern).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(exercise => exercise.YoutubeLink).HasMaxLength(200);

        builder.HasIndex(exercise => exercise.Name).IsUnique();

        builder.HasMany(exercise => exercise.SecondaryMuscles)
            .WithOne()
            .HasForeignKey(secondary => secondary.ExerciseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(exercise => exercise.Translations)
            .WithOne()
            .HasForeignKey(translation => translation.ExerciseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
