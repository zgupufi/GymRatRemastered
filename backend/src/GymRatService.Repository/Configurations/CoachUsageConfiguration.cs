using GymRatService.Repository.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymRatService.Repository.Configurations;

public class CoachUsageConfiguration : IEntityTypeConfiguration<CoachUsage>
{
    public void Configure(EntityTypeBuilder<CoachUsage> builder)
    {
        builder.ToTable("CoachUsages");

        builder.HasKey(usage => usage.Id);

        builder.Property(usage => usage.Id).ValueGeneratedOnAdd().HasDefaultValueSql("uuid_generate_v7()");

        builder.Property(usage => usage.Day).IsRequired();

        builder.Property(usage => usage.RequestCount).IsRequired();

        builder.Property(usage => usage.TokensUsed).IsRequired();

        builder.Property(usage => usage.RecentRequests).IsRequired();

        builder.Property(usage => usage.Version).IsRequired();

        builder.HasOne<User>().WithMany().HasForeignKey(usage => usage.UserId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(usage => usage.UserId).IsUnique();
    }
}
