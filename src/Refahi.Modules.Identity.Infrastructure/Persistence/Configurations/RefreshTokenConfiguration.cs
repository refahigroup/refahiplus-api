using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Identity.Domain.Entities;

namespace Refahi.Modules.Identity.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(rt => rt.Id);

        builder.Property(rt => rt.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(rt => rt.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(rt => rt.Token)
            .HasColumnName("token")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(rt => rt.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(rt => rt.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(rt => rt.IsRevoked)
            .HasColumnName("is_revoked")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(rt => rt.RevokedAt)
            .HasColumnName("revoked_at");

        builder.Property(rt => rt.RevokedReason)
            .HasColumnName("revoked_reason")
            .HasMaxLength(500);

        builder.Property(rt => rt.IsUsed)
            .HasColumnName("is_used")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(rt => rt.UsedAt)
            .HasColumnName("used_at");

        // Indexes for performance
        builder.HasIndex(rt => rt.Token)
            .IsUnique()
            .HasDatabaseName("ix_refresh_tokens_token");

        builder.HasIndex(rt => rt.UserId)
            .HasDatabaseName("ix_refresh_tokens_user_id");

        builder.HasIndex(rt => new { rt.UserId, rt.IsRevoked, rt.IsUsed, rt.ExpiresAt })
            .HasDatabaseName("ix_refresh_tokens_user_active");
    }
}
