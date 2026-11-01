using GymRatService.Repository.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymRatService.Repository.Configurations;

public class WorkoutSessionConfiguration : IEntityTypeConfiguration<WorkoutSession>
{
    public void Configure(EntityTypeBuilder<WorkoutSession> builder)
    {
        builder.ToTable("WorkoutSessions");

        builder.HasKey(session => session.Id);

        builder.Property(session => session.Id).ValueGeneratedOnAdd().HasDefaultValueSql("uuid_generate_v7()");

        builder.Property(session => session.StartedAt).IsRequired();

        builder.HasOne<User>().WithMany().HasForeignKey(session => session.UserId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Workout>().WithMany().HasForeignKey(session => session.WorkoutId).OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(session => new { session.UserId, session.WorkoutId });
    }
}
