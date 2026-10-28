using GymRatService.Repository.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymRatService.Repository.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(user => user.Id);

        builder.Property(user => user.Id).ValueGeneratedOnAdd().HasDefaultValueSql("uuid_generate_v7()");

        builder.Property(user => user.Email).HasMaxLength(100).IsRequired();

        builder.Property(user => user.PasswordHash).HasMaxLength(100).IsRequired();

        builder.Property(user => user.PasswordSalt).HasMaxLength(100).IsRequired();

        builder.Property(user => user.FirstName).HasMaxLength(50).IsRequired();

        builder.Property(user => user.LastName).HasMaxLength(50);

        builder.Property(user => user.WeightUnit).IsRequired();

        builder.Property(user => user.DefaultRestTimeSeconds).IsRequired();

        builder.Property(user => user.CreatedAt).IsRequired();

        builder.HasIndex(user => user.Email).IsUnique();
    }
}
