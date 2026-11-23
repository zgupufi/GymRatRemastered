using GymRatService.Repository.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymRatService.Repository.Configurations;

public class ExerciseSecondaryMuscleConfiguration : IEntityTypeConfiguration<ExerciseSecondaryMuscle>
{
    public void Configure(EntityTypeBuilder<ExerciseSecondaryMuscle> builder)
    {
        builder.ToTable("ExerciseSecondaryMuscles");

        builder.HasKey(secondary => new { secondary.ExerciseId, secondary.Muscle });

        builder.Property(secondary => secondary.Muscle).HasConversion<string>().HasMaxLength(30).IsRequired();
    }
}
