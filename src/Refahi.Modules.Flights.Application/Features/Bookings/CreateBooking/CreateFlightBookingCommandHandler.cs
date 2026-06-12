using System.Text.Json;
using MediatR;
using Refahi.Modules.Flights.Application.Contracts.Providers;
using Refahi.Modules.Flights.Application.Contracts.Providers.DTOs;
using Refahi.Modules.Flights.Application.Features.Bookings;
using Refahi.Modules.Flights.Application.Features.Offers;
using Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg;
using Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg.Entities;
using Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg.Enums;
using Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg.ValueObjects;
using Refahi.Modules.Flights.Domain.Repositories;

namespace Refahi.Modules.Flights.Application.Features.Bookings.CreateBooking;

public sealed class CreateFlightBookingCommandHandler
    : IRequestHandler<CreateFlightBookingCommand, FlightBookingDetailDto>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IFlightOfferSnapshotRepository _offerSnapshotRepository;
    private readonly IFlightBookingRepository _bookingRepository;
    private readonly IFlightProviderFactory _providerFactory;

    public CreateFlightBookingCommandHandler(
        IFlightOfferSnapshotRepository offerSnapshotRepository,
        IFlightBookingRepository bookingRepository,
        IFlightProviderFactory providerFactory)
    {
        _offerSnapshotRepository = offerSnapshotRepository;
        _bookingRepository = bookingRepository;
        _providerFactory = providerFactory;
    }

    public async Task<FlightBookingDetailDto> Handle(
        CreateFlightBookingCommand request,
        CancellationToken cancellationToken)
    {
        var normalizedIdempotencyKey = request.IdempotencyKey.Trim();
        var existing = await _bookingRepository.GetByIdempotencyKeyAsync(
            normalizedIdempotencyKey,
            cancellationToken);

        if (existing is not null)
        {
            EnsureOwner(existing, request.UserId);
            return FlightBookingDtoMapper.ToDetailDto(existing);
        }

        var offerSnapshot = await _offerSnapshotRepository.GetByTokenAsync(
            request.OfferToken.Trim(),
            cancellationToken);

        if (offerSnapshot is null || offerSnapshot.IsExpired(DateTime.UtcNow))
            throw new InvalidOperationException("پیشنهاد پرواز یافت نشد یا منقضی شده است.");

        var publicOffer = JsonSerializer.Deserialize<FlightOfferDto>(
            offerSnapshot.PublicOfferSnapshotJson,
            JsonOptions) ?? throw new InvalidOperationException("اطلاعات پیشنهاد پرواز معتبر نیست.");

        var provider = ResolveProvider(offerSnapshot.ProviderName);
        var providerRequest = new FlightBookRequest(
            offerSnapshot.ProviderFareSourceCode,
            request.Contact.MobileNumber.Trim(),
            request.Contact.Email.Trim(),
            request.Passengers.Select(MapProviderPassenger).ToList());

        var providerResponse = await provider.BookAsync(providerRequest, cancellationToken);
        ValidateProviderBookingResponse(providerResponse, offerSnapshot.TotalFareAmount, offerSnapshot.Currency);

        var nowUtc = DateTime.UtcNow;
        var booking = FlightBooking.CreateDraft(
            FlightBookingId.New(),
            request.UserId,
            new ProviderSnapshot(
                offerSnapshot.ProviderName,
                offerSnapshot.ProviderName,
                offerSnapshot.ProviderName,
                providerResponse.ProviderTraceId ?? offerSnapshot.ProviderTraceId,
                offerSnapshot.ProviderSnapshotJson),
            new SelectedFareSnapshot(
                offerSnapshot.ProviderFareSourceCode,
                BuildFareCaption(publicOffer),
                publicOffer.CabinClassCaption ?? publicOffer.CabinClassCode ?? "Economy",
                publicOffer.Segments.FirstOrDefault()?.BookingClass,
                offerSnapshot.ProviderSnapshotJson,
                offerSnapshot.ProviderTraceId),
            new ContactInfo(request.Contact.MobileNumber, request.Contact.Email),
            request.Passengers.Select(MapDomainPassenger).ToList(),
            publicOffer.Segments.Select((segment, index) => MapDomainSegment(segment, index + 1)).ToList(),
            BuildFareBreakdown(publicOffer.TotalFare),
            normalizedIdempotencyKey,
            nowUtc,
            offerSnapshot.ExpiresAtUtc);

        booking.MarkProviderBooked(new ProviderBookingSnapshot(
            providerResponse.BookId!,
            providerResponse.TrackingCode ?? providerResponse.BookId!,
            nowUtc,
            providerResponse.TrackingCode,
            providerResponse.ProviderTraceId,
            JsonSerializer.Serialize(new
            {
                providerResponse.BookId,
                providerResponse.TrackingCode,
                providerResponse.CheckoutUrl,
                providerResponse.PaymentCurrency,
                providerResponse.PaymentAmount,
                providerResponse.ProviderTraceId,
                providerResponse.RawPayloadSnapshot
            }, JsonOptions)), nowUtc);

        var duplicateProviderBooking = await _bookingRepository.GetByProviderBookingIdAsync(
            offerSnapshot.ProviderName,
            providerResponse.BookId!,
            cancellationToken);

        if (duplicateProviderBooking is not null)
        {
            EnsureOwner(duplicateProviderBooking, request.UserId);
            return FlightBookingDtoMapper.ToDetailDto(duplicateProviderBooking);
        }

        await _bookingRepository.AddAsync(booking, cancellationToken);
        await _bookingRepository.SaveChangesAsync(cancellationToken);

        return FlightBookingDtoMapper.ToDetailDto(booking);
    }

    private IFlightProvider ResolveProvider(string providerName)
    {
        return Enum.TryParse<FlightProviderType>(providerName, ignoreCase: true, out var providerType)
            ? _providerFactory.GetProvider(providerType)
            : _providerFactory.GetDefaultProvider();
    }

    private static FlightBookPassenger MapProviderPassenger(FlightBookingPassengerInput passenger)
    {
        return new FlightBookPassenger(
            passenger.NationalityCode.Trim().ToUpperInvariant(),
            string.IsNullOrWhiteSpace(passenger.NationalCode) ? null : passenger.NationalCode.Trim(),
            passenger.FirstName.Trim(),
            passenger.LastName.Trim(),
            passenger.Gender.Trim(),
            passenger.BirthDate,
            NormalizePassengerType(passenger.PassengerType).ToString(),
            passenger.Passport is null
                ? null
                : new FlightPassportInfo(
                    passenger.Passport.CountryCode,
                    passenger.Passport.IssueDate,
                    passenger.Passport.ExpireDate,
                    passenger.Passport.Number));
    }

    private static Passenger MapDomainPassenger(FlightBookingPassengerInput passenger)
    {
        return new Passenger(
            passenger.FirstName,
            passenger.LastName,
            NormalizePassengerType(passenger.PassengerType),
            passenger.BirthDate,
            passenger.NationalCode,
            passenger.Passport?.Number,
            passenger.NationalityCode);
    }

    private static FlightSegment MapDomainSegment(FlightSegmentDto segment, int sequence)
    {
        if (!segment.DepartureDateTime.HasValue || !segment.ArrivalDateTime.HasValue)
            throw new InvalidOperationException("زمان پرواز در پاسخ تامین‌کننده معتبر نیست.");

        return new FlightSegment(
            sequence,
            $"{segment.DepartureAirportCode}-{segment.ArrivalAirportCode}-{sequence}",
            segment.FlightNumber ?? sequence.ToString(),
            segment.MarketingAirlineCode ?? segment.OperatingAirlineCode ?? "NA",
            segment.MarketingAirlineCaption ?? segment.OperatingAirlineCaption ?? "Unknown",
            segment.DepartureAirportCode,
            segment.DepartureAirportCaption ?? segment.DepartureAirportCode,
            segment.ArrivalAirportCode,
            segment.ArrivalAirportCaption ?? segment.ArrivalAirportCode,
            DateTime.SpecifyKind(segment.DepartureDateTime.Value, DateTimeKind.Utc),
            DateTime.SpecifyKind(segment.ArrivalDateTime.Value, DateTimeKind.Utc));
    }

    private static FareBreakdown BuildFareBreakdown(FlightMoneyDto money)
    {
        var baseFare = money.BaseFare > 0 && money.BaseFare <= money.TotalFare
            ? money.BaseFare
            : money.TotalFare;
        var taxes = money.TotalTax >= 0 && baseFare + money.TotalTax <= money.TotalFare
            ? money.TotalTax
            : 0;
        var fees = money.TotalFare - baseFare - taxes;

        return new FareBreakdown(
            new Money(baseFare, money.Currency),
            new Money(taxes, money.Currency),
            new Money(fees, money.Currency),
            Money.Zero(money.Currency),
            new Money(money.TotalFare, money.Currency));
    }

    private static void ValidateProviderBookingResponse(
        FlightBookResponse providerResponse,
        long expectedAmount,
        string expectedCurrency)
    {
        if (string.IsNullOrWhiteSpace(providerResponse.BookId))
            throw new InvalidOperationException("شناسه رزرو تامین‌کننده دریافت نشد.");

        if (!string.IsNullOrWhiteSpace(providerResponse.PaymentCurrency)
            && !string.Equals(providerResponse.PaymentCurrency, expectedCurrency, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("واحد پولی رزرو تامین‌کننده با پیشنهاد انتخاب‌شده همخوانی ندارد.");
        }

        if (providerResponse.PaymentAmount.HasValue && providerResponse.PaymentAmount.Value != expectedAmount)
            throw new InvalidOperationException("مبلغ رزرو تامین‌کننده با پیشنهاد انتخاب‌شده همخوانی ندارد.");
    }

    private static FlightPassengerType NormalizePassengerType(string value)
    {
        return Enum.TryParse<FlightPassengerType>(value, ignoreCase: true, out var type)
            ? type
            : throw new InvalidOperationException("نوع مسافر معتبر نیست.");
    }

    private static string BuildFareCaption(FlightOfferDto offer)
    {
        var airline = offer.AirlineCaption ?? offer.AirlineCode ?? "پرواز";
        return $"{airline} {offer.OriginAirportCode}-{offer.DestinationAirportCode}";
    }

    private static void EnsureOwner(FlightBooking booking, Guid userId)
    {
        if (booking.UserId != userId)
            throw new UnauthorizedAccessException("دسترسی به این رزرو مجاز نیست.");
    }
}
