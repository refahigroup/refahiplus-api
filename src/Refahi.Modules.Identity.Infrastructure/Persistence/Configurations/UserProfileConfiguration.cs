using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Identity.Domain.Entities;

namespace Refahi.Modules.Identity.Infrastructure.Persistence.Configurations;

public class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("user_profiles");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.UserId)
            .IsRequired()
            .HasColumnName("user_id");

        builder.Property(p => p.FirstName)
            .HasMaxLength(100)
            .IsRequired()
            .HasColumnName("first_name");

        builder.Property(p => p.LastName)
            .HasMaxLength(100)
            .IsRequired()
            .HasColumnName("last_name");

        builder.Property(p => p.NationalCode)
            .HasMaxLength(20)
            .HasColumnName("national_code");

        builder.Property(p => p.ProfileImageUrl)
            .HasMaxLength(500)
            .HasColumnName("profile_image_url");

        builder.Property(p => p.Birthday)
            .HasColumnName("birthday");

        builder.Property(p => p.Gender)
            .HasMaxLength(10)
            .HasConversion<string>()
            .HasColumnName("gender");

        builder.Property(p => p.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at");

        builder.Property(p => p.UpdatedAt)
            .IsRequired()
            .HasColumnName("updated_at");

        // Indexes
        builder.HasIndex(p => p.UserId)
            .IsUnique()
            .HasDatabaseName("ix_user_profiles_user_id");

        builder.HasIndex(p => p.NationalCode)
            .HasFilter("national_code IS NOT NULL")
            .HasDatabaseName("ix_user_profiles_national_code");
    }
}
