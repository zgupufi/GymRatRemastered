using GymRatService.Repository.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymRatService.Repository.Configurations;

public class CoachSuggestionConfiguration : IEntityTypeConfiguration<CoachSuggestion>
{
    private const int RationaleMaximumLength = 500;

    public void Configure(EntityTypeBuilder<CoachSuggestion> builder)
    {
        builder.ToTable("CoachSuggestions");

        builder.HasKey(suggestion => suggestion.Id);

        builder.Property(suggestion => suggestion.Id).ValueGeneratedOnAdd().HasDefaultValueSql("uuid_generate_v7()");

        builder.Property(suggestion => suggestion.WeightStepKg).HasPrecision(5, 2);

        builder.Property(suggestion => suggestion.BeforeWeightStepKg).HasPrecision(5, 2).IsRequired();

        builder.Property(suggestion => suggestion.Rationale).HasMaxLength(RationaleMaximumLength).IsRequired();

        builder.Property(suggestion => suggestion.Status).IsRequired();

        builder.Property(suggestion => suggestion.CreatedAt).IsRequired();

        builder.HasOne<User>().WithMany().HasForeignKey(suggestion => suggestion.UserId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(suggestion => suggestion.Workout).WithMany().HasForeignKey(suggestion => suggestion.WorkoutId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(suggestion => suggestion.Exercise).WithMany().HasForeignKey(suggestion => suggestion.ExerciseId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(suggestion => new { suggestion.UserId, suggestion.Status });
    }
}
