using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Identity.Domain.Aggregates;
using Refahi.Modules.Identity.Domain.Entities;

namespace Refahi.Modules.Identity.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.MobileNumber)
            .HasMaxLength(20)
            .HasColumnName("mobile_number");

        builder.Property(u => u.Email)
            .HasMaxLength(255)
            .HasColumnName("email");

        builder.Property(u => u.Username)
            .HasMaxLength(30)
            .HasColumnName("username");

        builder.Property(u => u.PasswordHash)
            .HasMaxLength(255)
            .HasColumnName("password_hash");

        builder.Property(u => u.IsActive)
            .IsRequired()
            .HasColumnName("is_active");

        builder.Property(u => u.MobileApproved)
            .IsRequired()
            .HasDefaultValue(false)
            .HasColumnName("mobile_approved");

        builder.Property(u => u.EmailApproved)
            .IsRequired()
            .HasDefaultValue(false)
            .HasColumnName("email_approved");

        builder.Property(u => u.LockedUntil)
            .HasColumnName("locked_until");

        builder.Property(u => u.FailedLoginAttempts)
            .IsRequired()
            .HasDefaultValue(0)
            .HasColumnName("failed_login_attempts");

        builder.Property(u => u.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at");

        builder.Property(u => u.UpdatedAt)
            .IsRequired()
            .HasColumnName("updated_at");

        // Indexes
        builder.HasIndex(u => u.MobileNumber)
            .IsUnique()
            .HasFilter("mobile_number IS NOT NULL")
            .HasDatabaseName("ix_users_mobile_number");

        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasFilter("email IS NOT NULL")
            .HasDatabaseName("ix_users_email");

        builder.HasIndex(u => u.Username)
            .IsUnique()
            .HasFilter("username IS NOT NULL")
            .HasDatabaseName("ix_users_username");

        // Check constraint: at least one of mobile or email must be provided
        builder.ToTable(t => t.HasCheckConstraint(
            "chk_mobile_or_email",
            "mobile_number IS NOT NULL OR email IS NOT NULL"));

        // Relationships
        builder.HasOne(u => u.Profile)
            .WithOne(p => p.User)
            .HasForeignKey<UserProfile>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.Roles)
            .WithOne(r => r.User)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
