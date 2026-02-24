using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg;
using Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.ValueObjects;

namespace Refahi.Modules.Hotels.Infrastructure.Persistence.Configurations
{
    public sealed class BookingEntityTypeConfiguration : IEntityTypeConfiguration<Booking>
    {
        public void Configure(EntityTypeBuilder<Booking> builder)
        {
            builder.ToTable("hotel_bookings");

            // Key
            builder.HasKey(b => b.Id);

            builder.Property(b => b.Id)
                .HasConversion(
                    id => id.Value,
                    guid => new BookingId(guid))
                .HasColumnName("Id");

            // Provider info
            builder.Property(b => b.Provider)
                .HasConversion<int>()
                .HasColumnName("Provider")
                .IsRequired();

            builder.Property(b => b.ProviderBookingCode)
                .HasConversion(
                    v => v.Value,
                    s => new ProviderBookingCode(s))
                .HasColumnName("ProviderBookingCode")
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(b => b.ProviderHotelId)
                .HasConversion(
                    v => v.Value,
                    l => new ProviderHotelId(l))
                .HasColumnName("ProviderHotelId")
                .IsRequired();

            builder.Property(b => b.ProviderRoomId)
                .HasConversion(
                    v => v.Value,
                    l => new ProviderRoomId(l))
                .HasColumnName("ProviderRoomId")
                .IsRequired();

            // DateRange (StayRange)
            var stay = builder.OwnsOne(b => b.StayRange);
            stay.Property(p => p.CheckIn)
                .HasColumnName("CheckIn")
                .IsRequired();

            stay.Property(p => p.CheckOut)
                .HasColumnName("CheckOut")
                .IsRequired();

            // Prices
            builder.OwnsOne(b => b.BasePrice, money =>
            {
                money.Property(p => p.Amount)
                    .HasColumnName("BasePriceAmount")
                    .IsRequired();

                money.Property(p => p.Currency)
                    .HasColumnName("BasePriceCurrency")
                    .HasMaxLength(10)
                    .IsRequired();
            });

            builder.OwnsOne(b => b.MarginAmount, money =>
            {
                money.Property(p => p.Amount)
                    .HasColumnName("MarginAmount")
                    .IsRequired();

                money.Property(p => p.Currency)
                    .HasColumnName("MarginCurrency")
                    .HasMaxLength(10)
                    .IsRequired();
            });

            builder.OwnsOne(b => b.CustomerPrice, money =>
            {
                money.Property(p => p.Amount)
                    .HasColumnName("CustomerPriceAmount")
                    .IsRequired();

                money.Property(p => p.Currency)
                    .HasColumnName("CustomerPriceCurrency")
                    .HasMaxLength(10)
                    .IsRequired();
            });

            // Enum Status
            builder.Property(b => b.Status)
                .HasConversion<int>()
                .HasColumnName("Status")
                .IsRequired();

            // BoardType
            builder.Property(b => b.BoardType)
                .HasConversion<int>()
                .HasColumnName("BoardType")
                .IsRequired();

            // RoomsCount
            builder.Property(b => b.RoomsCount)
                .HasColumnName("RoomsCount")
                .IsRequired();

            // Guests (owned collection)
            builder.OwnsMany(b => b.Guests, guests =>
            {
                guests.ToTable("hotel_booking_guests");

                guests.WithOwner().HasForeignKey("BookingId");

                guests.Property<int>("Id"); // shadow key
                guests.HasKey("Id");

                guests.Property(g => g.FullName)
                    .HasColumnName("FullName")
                    .HasMaxLength(200)
                    .IsRequired();

                guests.Property(g => g.Age)
                    .HasColumnName("Age")
                    .IsRequired();

                guests.Property(g => g.Type)
                    .HasConversion<int>()
                    .HasColumnName("Type")
                    .IsRequired();
            });

            // Voucher
            builder.OwnsOne(b => b.Voucher, voucher =>
            {
                voucher.Property(v => v.VoucherNumber)
                    .HasColumnName("VoucherNumber")
                    .HasMaxLength(100);

                voucher.Property(v => v.Url)
                    .HasColumnName("VoucherUrl")
                    .HasMaxLength(1000);
            });

            // Timestamps
            builder.Property(b => b.CreatedAt)
                .HasColumnName("CreatedAt")
                .IsRequired();

            builder.Property(b => b.UpdatedAt)
                .HasColumnName("UpdatedAt")
                .IsRequired();

            builder.Property(b => b.LockedUntil)
                .HasColumnName("LockedUntil");

            // DomainEvents collection را EF نباید map کند
            builder.Ignore(b => b.DomainEvents);
        }
    }
}
