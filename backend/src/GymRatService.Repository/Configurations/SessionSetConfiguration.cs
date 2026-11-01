using GymRatService.Repository.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymRatService.Repository.Configurations;

public class SessionSetConfiguration : IEntityTypeConfiguration<SessionSet>
{
    public void Configure(EntityTypeBuilder<SessionSet> builder)
    {
        builder.ToTable("SessionSets");

        builder.HasKey(set => set.Id);

        builder.Property(set => set.Id).ValueGeneratedOnAdd().HasDefaultValueSql("uuid_generate_v7()");

        builder.Property(set => set.SetNumber).IsRequired();

        builder.Property(set => set.Reps).IsRequired();

        builder.Property(set => set.WeightKg).HasPrecision(7, 2).IsRequired();

        builder.HasOne(set => set.Session).WithMany(session => session.Sets).HasForeignKey(set => set.SessionId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ExerciseCard>().WithMany().HasForeignKey(set => set.ExerciseId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(set => new { set.SessionId, set.ExerciseId, set.SetNumber });

        builder.HasIndex(set => set.ExerciseId);
    }
}
