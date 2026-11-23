using GymRatService.Repository.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymRatService.Repository.Configurations;

public class ExerciseTranslationConfiguration : IEntityTypeConfiguration<ExerciseTranslation>
{
    public void Configure(EntityTypeBuilder<ExerciseTranslation> builder)
    {
        builder.ToTable("ExerciseTranslations");

        builder.HasKey(translation => new { translation.ExerciseId, translation.Culture });

        builder.Property(translation => translation.Culture).HasMaxLength(10).IsRequired();

        builder.Property(translation => translation.Name).HasMaxLength(100).IsRequired();

        builder.Property(translation => translation.Description).HasMaxLength(1000);

        builder.Property(translation => translation.Setup).HasMaxLength(1000).IsRequired();

        builder.Property(translation => translation.Execution).HasMaxLength(1500).IsRequired();
    }
}
