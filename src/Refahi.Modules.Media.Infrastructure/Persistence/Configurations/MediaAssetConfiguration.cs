using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Media.Domain.Aggregates;

namespace Refahi.Modules.Media.Infrastructure.Persistence.Configurations;

public class MediaAssetConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    public void Configure(EntityTypeBuilder<MediaAsset> builder)
    {
        builder.ToTable("media_assets");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OriginalFileName)
            .HasMaxLength(500).IsRequired();

        builder.Property(x => x.StoredFileName)
            .HasMaxLength(200).IsRequired();

        builder.Property(x => x.StoragePath)
            .HasMaxLength(1000).IsRequired();

        builder.Property(x => x.ContentType)
            .HasMaxLength(100).IsRequired();

        builder.Property(x => x.FileSizeBytes).IsRequired();
        builder.Property(x => x.MediaType).IsRequired().HasConversion<int>();
        builder.Property(x => x.EntityType).HasMaxLength(100);
        builder.Property(x => x.UploadedByUserId).IsRequired();
        builder.Property(x => x.UploadedAt).IsRequired();
        builder.Property(x => x.IsDeleted).IsRequired();

        builder.HasIndex(x => new { x.EntityType, x.EntityId })
            .HasDatabaseName("ix_media_assets_entity");

        builder.HasIndex(x => x.UploadedByUserId)
            .HasDatabaseName("ix_media_assets_uploaded_by");

        builder.HasIndex(x => x.UploadedAt)
            .HasDatabaseName("ix_media_assets_uploaded_at");

        builder.HasIndex(x => x.IsDeleted)
            .HasDatabaseName("ix_media_assets_is_deleted");
    }
}
