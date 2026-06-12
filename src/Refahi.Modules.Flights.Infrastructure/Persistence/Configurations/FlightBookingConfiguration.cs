using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg;
using Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg.Entities;
using Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg.ValueObjects;

namespace Refahi.Modules.Flights.Infrastructure.Persistence.Configurations;

public sealed class FlightBookingConfiguration : IEntityTypeConfiguration<FlightBooking>
{
    public void Configure(EntityTypeBuilder<FlightBooking> builder)
    {
        builder.ToTable("flight_bookings");

        builder.HasKey(booking => booking.Id);

        builder.Property(booking => booking.Id)
            .HasConversion(id => id.Value, value => new FlightBookingId(value))
            .HasColumnName("id");

        builder.Property(booking => booking.UserId)
            .IsRequired()
            .HasColumnName("user_id");

        builder.Property(booking => booking.Status)
            .IsRequired()
            .HasConversion<short>()
            .HasColumnName("status");

        builder.Property(booking => booking.OrderId)
            .HasColumnName("order_id");

        builder.Property(booking => booking.OrderNumber)
            .HasMaxLength(50)
            .HasColumnName("order_number");

        builder.Property(booking => booking.IdempotencyKey)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("idempotency_key");

        builder.Property(booking => booking.CreatedAtUtc)
            .IsRequired()
            .HasColumnName("created_at_utc");

        builder.Property(booking => booking.UpdatedAtUtc)
            .IsRequired()
            .HasColumnName("updated_at_utc");

        builder.Property(booking => booking.ExpiresAtUtc)
            .HasColumnName("expires_at_utc");

        builder.Property(booking => booking.IssueFailureReason)
            .HasMaxLength(1000)
            .HasColumnName("issue_failure_reason");

        ConfigureProvider(builder);
        ConfigureContact(builder);
        ConfigureFareBreakdown(builder);
        ConfigureProviderBooking(builder);
        ConfigureSelectedFare(builder);
        ConfigureLatestCancellationQuote(builder);
        ConfigurePassengers(builder);
        ConfigureSegments(builder);
        ConfigureIssuedTickets(builder);
        ConfigureCancellationRequests(builder);

        builder.HasIndex(booking => booking.UserId)
            .HasDatabaseName("ix_flight_bookings_user_id");

        builder.HasIndex(booking => booking.OrderId)
            .IsUnique()
            .HasDatabaseName("ux_flight_bookings_order_id");

        builder.HasIndex(booking => booking.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("ux_flight_bookings_idempotency_key");

        builder.HasIndex(booking => booking.Status)
            .HasDatabaseName("ix_flight_bookings_status");
    }

    private static void ConfigureProvider(EntityTypeBuilder<FlightBooking> builder)
    {
        builder.OwnsOne(booking => booking.Provider, provider =>
        {
            provider.Property(item => item.ProviderName)
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnName("provider_name");

            provider.Property(item => item.ProviderId)
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnName("provider_id");

            provider.Property(item => item.ProviderCaption)
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnName("provider_caption");

            provider.Property(item => item.ProviderTraceId)
                .HasMaxLength(200)
                .HasColumnName("provider_trace_id");

            provider.Property(item => item.SnapshotJson)
                .HasColumnType("jsonb")
                .HasColumnName("provider_snapshot");
        });
    }

    private static void ConfigureContact(EntityTypeBuilder<FlightBooking> builder)
    {
        builder.OwnsOne(booking => booking.Contact, contact =>
        {
            contact.Property(item => item.MobileNumber)
                .IsRequired()
                .HasMaxLength(30)
                .HasColumnName("contact_mobile_number");

            contact.Property(item => item.Email)
                .HasMaxLength(320)
                .HasColumnName("contact_email");
        });
    }

    private static void ConfigureFareBreakdown(EntityTypeBuilder<FlightBooking> builder)
    {
        builder.OwnsOne(booking => booking.FareBreakdown, fare =>
        {
            ConfigureMoney(fare.OwnsOne(item => item.BaseFare), "base_fare");
            ConfigureMoney(fare.OwnsOne(item => item.Taxes), "taxes");
            ConfigureMoney(fare.OwnsOne(item => item.Fees), "fees");
            ConfigureMoney(fare.OwnsOne(item => item.Discount), "discount");
            ConfigureMoney(fare.OwnsOne(item => item.PayableAmount), "payable_amount");
        });
    }

    private static void ConfigureProviderBooking(EntityTypeBuilder<FlightBooking> builder)
    {
        builder.OwnsOne(booking => booking.ProviderBooking, providerBooking =>
        {
            providerBooking.Property(item => item.ProviderBookingId)
                .HasMaxLength(200)
                .HasColumnName("provider_booking_id");

            providerBooking.Property(item => item.ProviderBookingCaption)
                .HasMaxLength(300)
                .HasColumnName("provider_booking_caption");

            providerBooking.Property(item => item.ProviderPnr)
                .HasMaxLength(100)
                .HasColumnName("provider_pnr");

            providerBooking.Property(item => item.ProviderTraceId)
                .HasMaxLength(200)
                .HasColumnName("provider_booking_trace_id");

            providerBooking.Property(item => item.SnapshotJson)
                .HasColumnType("jsonb")
                .HasColumnName("provider_booking_snapshot");

            providerBooking.Property(item => item.BookedAtUtc)
                .HasColumnName("provider_booked_at_utc");
        });
    }

    private static void ConfigureSelectedFare(EntityTypeBuilder<FlightBooking> builder)
    {
        builder.OwnsOne(booking => booking.SelectedFare, fare =>
        {
            fare.ToTable("flight_offer_snapshots");

            fare.Property<FlightBookingId>("FlightBookingId")
                .HasConversion(id => id.Value, value => new FlightBookingId(value))
                .HasColumnName("flight_booking_id");

            fare.WithOwner().HasForeignKey("FlightBookingId");
            fare.HasKey("FlightBookingId");

            fare.Property(item => item.ProviderFareId)
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnName("provider_fare_id");

            fare.Property(item => item.FareCaption)
                .IsRequired()
                .HasMaxLength(300)
                .HasColumnName("fare_caption");

            fare.Property(item => item.CabinClass)
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnName("cabin_class");

            fare.Property(item => item.BookingClass)
                .HasMaxLength(100)
                .HasColumnName("booking_class");

            fare.Property(item => item.FareRulesSnapshotJson)
                .HasColumnType("jsonb")
                .HasColumnName("fare_rules_snapshot");

            fare.Property(item => item.ProviderTraceId)
                .HasMaxLength(200)
                .HasColumnName("provider_trace_id");
        });
    }

    private static void ConfigureLatestCancellationQuote(EntityTypeBuilder<FlightBooking> builder)
    {
        builder.OwnsOne(booking => booking.LatestCancellationQuote, quote =>
        {
            ConfigureCancellationQuote(quote, "latest_cancellation_quote");
        });
    }

    private static void ConfigurePassengers(EntityTypeBuilder<FlightBooking> builder)
    {
        builder.OwnsMany(booking => booking.Passengers, passengers =>
        {
            passengers.ToTable("flight_booking_passengers");

            passengers.WithOwner().HasForeignKey("flight_booking_id");

            passengers.HasKey(passenger => passenger.Id);

            passengers.Property(passenger => passenger.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");

            passengers.Property(passenger => passenger.FirstName)
                .IsRequired()
                .HasMaxLength(150)
                .HasColumnName("first_name");

            passengers.Property(passenger => passenger.LastName)
                .IsRequired()
                .HasMaxLength(150)
                .HasColumnName("last_name");

            passengers.Property(passenger => passenger.Type)
                .IsRequired()
                .HasConversion<short>()
                .HasColumnName("type");

            passengers.Property(passenger => passenger.BirthDate)
                .IsRequired()
                .HasColumnType("date")
                .HasColumnName("birth_date");

            passengers.Property(passenger => passenger.NationalCode)
                .HasMaxLength(30)
                .HasColumnName("national_code");

            passengers.Property(passenger => passenger.PassportNumber)
                .HasMaxLength(50)
                .HasColumnName("passport_number");

            passengers.Property(passenger => passenger.NationalityCode)
                .IsRequired()
                .HasMaxLength(3)
                .HasColumnName("nationality_code");

            passengers.Ignore(passenger => passenger.DisplayName);

            passengers.HasIndex("flight_booking_id")
                .HasDatabaseName("ix_flight_booking_passengers_booking_id");
        });

        builder.Navigation(booking => booking.Passengers)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureSegments(EntityTypeBuilder<FlightBooking> builder)
    {
        builder.OwnsMany(booking => booking.Segments, segments =>
        {
            segments.ToTable("flight_booking_segments");

            segments.WithOwner().HasForeignKey("flight_booking_id");

            segments.HasKey(segment => segment.Id);

            segments.Property(segment => segment.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");

            segments.Property(segment => segment.Sequence)
                .IsRequired()
                .HasColumnName("sequence");

            segments.Property(segment => segment.ProviderSegmentId)
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnName("provider_segment_id");

            segments.Property(segment => segment.FlightNumber)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("flight_number");

            segments.Property(segment => segment.AirlineCode)
                .IsRequired()
                .HasMaxLength(20)
                .HasColumnName("airline_code");

            segments.Property(segment => segment.AirlineName)
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnName("airline_name");

            segments.Property(segment => segment.OriginAirportCode)
                .IsRequired()
                .HasMaxLength(10)
                .HasColumnName("origin_airport_code");

            segments.Property(segment => segment.OriginCaption)
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnName("origin_caption");

            segments.Property(segment => segment.DestinationAirportCode)
                .IsRequired()
                .HasMaxLength(10)
                .HasColumnName("destination_airport_code");

            segments.Property(segment => segment.DestinationCaption)
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnName("destination_caption");

            segments.Property(segment => segment.DepartureAtUtc)
                .IsRequired()
                .HasColumnName("departure_at_utc");

            segments.Property(segment => segment.ArrivalAtUtc)
                .IsRequired()
                .HasColumnName("arrival_at_utc");

            segments.HasIndex("flight_booking_id", nameof(FlightSegment.Sequence))
                .IsUnique()
                .HasDatabaseName("ux_flight_booking_segments_booking_sequence");
        });

        builder.Navigation(booking => booking.Segments)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureIssuedTickets(EntityTypeBuilder<FlightBooking> builder)
    {
        builder.OwnsMany(booking => booking.IssuedTickets, tickets =>
        {
            tickets.ToTable("flight_booking_tickets");

            tickets.WithOwner().HasForeignKey("flight_booking_id");

            tickets.HasKey(ticket => ticket.Id);

            tickets.Property(ticket => ticket.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");

            tickets.Property(ticket => ticket.PassengerId)
                .IsRequired()
                .HasColumnName("passenger_id");

            tickets.Property(ticket => ticket.TicketNumber)
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnName("ticket_number");

            tickets.Property(ticket => ticket.PassengerNameSnapshot)
                .IsRequired()
                .HasMaxLength(300)
                .HasColumnName("passenger_name_snapshot");

            tickets.Property(ticket => ticket.ProviderTicketId)
                .HasMaxLength(200)
                .HasColumnName("provider_ticket_id");

            tickets.Property(ticket => ticket.ProviderTraceId)
                .HasMaxLength(200)
                .HasColumnName("provider_trace_id");

            tickets.Property(ticket => ticket.SnapshotJson)
                .HasColumnType("jsonb")
                .HasColumnName("ticket_snapshot");

            tickets.Property(ticket => ticket.IssuedAtUtc)
                .IsRequired()
                .HasColumnName("issued_at_utc");

            tickets.HasIndex("flight_booking_id")
                .HasDatabaseName("ix_flight_booking_tickets_booking_id");

            tickets.HasIndex(ticket => ticket.TicketNumber)
                .HasDatabaseName("ix_flight_booking_tickets_ticket_number");
        });

        builder.Navigation(booking => booking.IssuedTickets)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureCancellationRequests(EntityTypeBuilder<FlightBooking> builder)
    {
        builder.OwnsMany(booking => booking.CancellationRequests, requests =>
        {
            requests.ToTable("flight_cancellation_requests");

            requests.WithOwner().HasForeignKey("flight_booking_id");

            requests.HasKey(request => request.Id);

            requests.Property(request => request.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");

            requests.Property(request => request.Reason)
                .IsRequired()
                .HasMaxLength(1000)
                .HasColumnName("reason");

            requests.Property(request => request.Status)
                .IsRequired()
                .HasConversion<short>()
                .HasColumnName("status");

            requests.Property(request => request.RequestedAtUtc)
                .IsRequired()
                .HasColumnName("requested_at_utc");

            requests.Property(request => request.CompletedAtUtc)
                .HasColumnName("completed_at_utc");

            requests.Property(request => request.FailureReason)
                .HasMaxLength(1000)
                .HasColumnName("failure_reason");

            requests.Property(request => request.ProviderCancellationId)
                .HasMaxLength(200)
                .HasColumnName("provider_cancellation_id");

            requests.OwnsOne(request => request.Quote, quote =>
            {
                ConfigureCancellationQuote(quote, "quote");
            });

            requests.HasIndex("flight_booking_id")
                .HasDatabaseName("ix_flight_cancellation_requests_booking_id");
        });

        builder.Navigation(booking => booking.CancellationRequests)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureMoney<TOwner>(
        OwnedNavigationBuilder<TOwner, Money> money,
        string columnPrefix)
        where TOwner : class
    {
        money.Property(item => item.Amount)
            .IsRequired()
            .HasColumnName($"{columnPrefix}_amount");

        money.Property(item => item.Currency)
            .IsRequired()
            .HasMaxLength(3)
            .HasColumnName($"{columnPrefix}_currency");
    }

    private static void ConfigureCancellationQuote<TOwner>(
        OwnedNavigationBuilder<TOwner, CancellationQuoteSnapshot> quote,
        string columnPrefix)
        where TOwner : class
    {
        ConfigureMoney(quote.OwnsOne(item => item.PenaltyAmount), $"{columnPrefix}_penalty");
        ConfigureMoney(quote.OwnsOne(item => item.RefundAmount), $"{columnPrefix}_refund");

        quote.Property(item => item.QuotedAtUtc)
            .HasColumnName($"{columnPrefix}_quoted_at_utc");

        quote.Property(item => item.ExpiresAtUtc)
            .HasColumnName($"{columnPrefix}_expires_at_utc");

        quote.Property(item => item.ProviderCancellationQuoteId)
            .HasMaxLength(200)
            .HasColumnName($"{columnPrefix}_provider_quote_id");

        quote.Property(item => item.ProviderTraceId)
            .HasMaxLength(200)
            .HasColumnName($"{columnPrefix}_provider_trace_id");

        quote.Property(item => item.SnapshotJson)
            .HasColumnType("jsonb")
            .HasColumnName($"{columnPrefix}_snapshot");
    }
}
