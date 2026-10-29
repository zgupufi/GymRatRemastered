using GymRatService.Repository.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymRatService.Repository.Configurations;

public class SplitDayConfiguration : IEntityTypeConfiguration<SplitDay>
{
    public void Configure(EntityTypeBuilder<SplitDay> builder)
    {
        builder.ToTable("SplitDays");

        builder.HasKey(splitDay => splitDay.Id);

        builder.Property(splitDay => splitDay.Id).ValueGeneratedOnAdd().HasDefaultValueSql("uuid_generate_v7()");

        builder.Property(splitDay => splitDay.Day).IsRequired();

        builder.HasOne<User>().WithMany().HasForeignKey(splitDay => splitDay.UserId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(splitDay => splitDay.Workout).WithMany().HasForeignKey(splitDay => splitDay.WorkoutId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(splitDay => new { splitDay.UserId, splitDay.Day }).IsUnique();
    }
}
