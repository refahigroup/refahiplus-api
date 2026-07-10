using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Charge.Domain.Aggregates;
namespace Refahi.Modules.Charge.Infrastructure.Persistence.Configurations;
public sealed class ChargePinConfiguration : IEntityTypeConfiguration<ChargePin>
{
    public void Configure(EntityTypeBuilder<ChargePin> b)
    {
        b.ToTable("charge_pins"); 
        
        b.HasKey(x => x.Id); 

        b.Property(x => x.Id)
            .HasColumnName("id");

        b.Property(x => x.ChargeRequestId)
            .HasColumnName("charge_request_id"); 
        
        b.Property(x => x.EncryptedSerial)
            .HasColumnName("encrypted_serial")
            .HasMaxLength(2000);

        b.Property(x => x.EncryptedCode)
            .HasColumnName("encrypted_code")
            .HasMaxLength(2000); 
        
        b.Property(x => x.AmountMinor)
            .HasColumnName("amount_minor");

        b.Property(x => x.CreatedAt)
            .HasColumnName("created_at");
    }
}
