using GymRatService.Repository.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymRatService.Repository.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(token => token.Id);

        builder.Property(token => token.Id).ValueGeneratedOnAdd().HasDefaultValueSql("uuid_generate_v7()");

        builder.Property(token => token.FamilyId).IsRequired();

        builder.Property(token => token.TokenHash).HasMaxLength(64).IsRequired();

        builder.Property(token => token.CreatedAt).IsRequired();

        builder.Property(token => token.ExpiresAt).IsRequired();

        builder.HasOne<User>().WithMany().HasForeignKey(token => token.UserId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(token => token.TokenHash).IsUnique();

        builder.HasIndex(token => token.FamilyId);

        builder.HasIndex(token => token.UserId);
    }
}
