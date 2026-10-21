using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Media.Domain.Aggregates;
namespace Refahi.Modules.Media.Infrastructure.Persistence.Configurations;
public sealed class ExternalMediaConfiguration : IEntityTypeConfiguration<ExternalMediaSource>, IEntityTypeConfiguration<ExternalMediaOrigin>
{
    public void Configure(EntityTypeBuilder<ExternalMediaSource> b)
    {
        b.ToTable("external_sources"); b.HasKey(x => x.Id);
        b.Property(x => x.SourceUrl).HasMaxLength(8192).IsRequired();
        b.Property(x => x.FileName).HasMaxLength(64).IsRequired(); b.HasIndex(x => x.FileName).IsUnique();
        b.Property(x => x.Status).HasConversion<int>(); b.HasIndex(x => x.ExpiresAt);
    }
    public void Configure(EntityTypeBuilder<ExternalMediaOrigin> b)
    {
        b.ToTable("external_source_origins"); b.HasKey(x => new { x.SourceId, x.Module, x.Provider });
        b.Property(x => x.Module).HasMaxLength(100); b.Property(x => x.Provider).HasMaxLength(100);
        b.HasOne<ExternalMediaSource>().WithMany().HasForeignKey(x => x.SourceId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.Module, x.Provider }); b.HasIndex(x => x.Provider);
    }
}
