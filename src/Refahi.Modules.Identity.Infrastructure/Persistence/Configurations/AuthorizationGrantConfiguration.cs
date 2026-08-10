using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Identity.Domain.Entities;

namespace Refahi.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class AuthorizationGrantConfiguration : IEntityTypeConfiguration<AuthorizationGrant>
{
    public void Configure(EntityTypeBuilder<AuthorizationGrant> builder)
    {
        builder.ToTable("authorization_grants");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.Issuer).HasColumnName("issuer").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Value).HasColumnName("value").HasMaxLength(256).IsRequired();
        builder.Property(x => x.EmittedRole).HasColumnName("emitted_role").HasMaxLength(50);
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.RevokedAt).HasColumnName("revoked_at");
        builder.Property(x => x.RevokedBy).HasColumnName("revoked_by");
        builder
            .HasIndex(x => new
            {
                x.UserId,
                x.Issuer,
                x.Value,
            })
            .IsUnique()
            .HasDatabaseName("ux_authorization_grants_user_issuer_value");
        builder
            .HasIndex(x => new
            {
                x.UserId,
                x.Issuer,
                x.IsActive,
            })
            .HasDatabaseName("ix_authorization_grants_lookup");
    }
}
