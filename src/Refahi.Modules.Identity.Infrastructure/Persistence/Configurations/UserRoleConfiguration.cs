using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Identity.Domain.Entities;

namespace Refahi.Modules.Identity.Infrastructure.Persistence.Configurations;

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("user_roles");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.UserId)
            .IsRequired()
            .HasColumnName("user_id");

        builder.Property(r => r.Role)
            .HasMaxLength(50)
            .IsRequired()
            .HasColumnName("role");

        builder.Property(r => r.AssignedAt)
            .IsRequired()
            .HasColumnName("assigned_at");

        builder.Property(r => r.AssignedBy)
            .HasColumnName("assigned_by");

        // Unique constraint: (UserId, Role)
        builder.HasIndex(r => new { r.UserId, r.Role })
            .IsUnique()
            .HasDatabaseName("ix_user_roles_user_role");

        builder.HasIndex(r => r.UserId)
            .HasDatabaseName("ix_user_roles_user_id");

        builder.HasIndex(r => r.Role)
            .HasDatabaseName("ix_user_roles_role");
    }
}
