using Refahi.Modules.Media.Application.Contracts.External;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.Hotels.Application.Contracts.Catalogue;
using Refahi.Modules.Hotels.Application.Contracts.Services.HotelRequests.CreateHotelRequest;
using Refahi.Modules.Hotels.Domain.Abstraction.Repositories;
using Refahi.Modules.Hotels.Domain.Aggregates.Catalogue;
using Refahi.Modules.Hotels.Domain.Aggregates.HotelBookingSagaAgg;
using Refahi.Modules.Hotels.Domain.Aggregates.HotelRequestAgg;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Contracts.Queries;
namespace Refahi.Modules.Hotels.Application.Features.Catalogue;

public sealed partial class HotelCatalogueService(IHotelCatalogueRepository repository, IHotelCatalogueProviderRegistry providers,
    IHotelSalePriceCalculator pricing, IHotelRequestRepository requests, IHotelBookingSagaRepository sagas,
    IMediator mediator, ILogger<HotelCatalogueService> logger, IOptions<HotelLifecycleOptions>? lifecycle = null, IExternalMediaService? media = null)
{
    private HotelLifecycleOptions Policy => lifecycle?.Value ?? new();
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    internal static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Json);
    internal static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, Json) ?? throw new InvalidOperationException("اطلاعات رزرو معتبر نیست.");
    public async Task<IReadOnlyList<HotelCityDto>> CitiesAsync(string? name, CancellationToken ct)
    {
        var response = await mediator.Send(new Refahi.Modules.References.Application.Contracts.Queries.GetCitiesQuery(ActiveOnly: true), ct);
        var term = NormalizeCityName(name);
        return response.Cities.Where(x => x.IsActive && (term.Length == 0
                || NormalizeCityName(x.Name).Contains(term, StringComparison.OrdinalIgnoreCase)
                || NormalizeCityName(x.NameEn).Contains(term, StringComparison.OrdinalIgnoreCase)))
            .Select(x => new HotelCityDto(x.Id, x.Name, x.NameEn, x.ProvinceId, x.ProvinceName)).ToArray();
    }
    private static string NormalizeCityName(string? name) => string.Join(" ", (name ?? "").Replace('ي', 'ی').Replace('ك', 'ک')
        .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public async Task<HotelSearchResponse> SearchAsync(SearchHotelCatalogueQuery query, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ValidateTravel(query.Travel);
        var city = await mediator.Send(new Refahi.Modules.References.Application.Contracts.Queries.GetCityByIdQuery(query.ReferenceCityId), ct);
        if (city is null || !city.IsActive) throw new FluentValidation.ValidationException("شهر مقصد معتبر نیست.");
        if (providers.Active.Count == 0) throw new HotelCatalogueUnavailableException("جست‌وجوی هتل در دسترس نیست.");
        var tasks = providers.Active.Select(async provider =>
        {
            var offers = new List<ProviderHotelOffer>();
            var succeeded = 0;
            var failed = 0;
            var started = System.Diagnostics.Stopwatch.StartNew();
            IReadOnlyList<string> nativeIds;
            try { nativeIds = await provider.ResolveCityIdsAsync(query.ReferenceCityId, ct); }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning("Hotel city mapping failed ReferenceCityId={ReferenceCityId} Provider={Provider} ErrorType={ErrorType}", query.ReferenceCityId, provider.Name, ex.GetType().Name);
                return (provider, offers: (IReadOnlyList<ProviderHotelOffer>)offers, succeeded, failed: 1);
            }
            foreach (var nativeId in nativeIds.Distinct(StringComparer.Ordinal))
            {
                ct.ThrowIfCancellationRequested();
                try { offers.AddRange(await provider.SearchAsync(nativeId, query.Travel, ct)); succeeded++; }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    failed++;
                    logger.LogWarning("Hotel search failed ReferenceCityId={ReferenceCityId} Provider={Provider} NativeCityId={NativeCityId} ErrorType={ErrorType}",
                        query.ReferenceCityId, provider.Name, nativeId, ex.GetType().Name);
                }
            }
            logger.LogInformation("Hotel search ReferenceCityId={ReferenceCityId} Provider={Provider} MappingCount={MappingCount} Succeeded={Succeeded} Failed={Failed} DurationMs={DurationMs}",
                query.ReferenceCityId, provider.Name, nativeIds.Count, succeeded, failed, started.ElapsedMilliseconds);
            return (provider, offers: (IReadOnlyList<ProviderHotelOffer>)offers, succeeded, failed);
        });
        var results = await Task.WhenAll(tasks);
        ct.ThrowIfCancellationRequested();
        if (results.Sum(x => x.failed) > 0 && results.Sum(x => x.succeeded) == 0)
            throw new HotelCatalogueUnavailableException("جست‌وجوی هتل در دسترس نیست. دوباره تلاش کنید.");
        var partial = results.Any(x => x.failed > 0);
        if (results.All(x => x.offers.Count == 0))
            return new([], partial, partial ? "بخشی از نتایج در دسترس نیست." : null);
        if (media is not null)
            foreach (var result in results) await media.GetPublicUrlsAsync(result.offers.SelectMany(x => x.Hotel.Images), new("Hotels", result.provider.Name), ct);
        var offers = new List<HotelOfferDto>();
        await using var held = await repository.LockAsync("catalogue", ct);
        foreach (var result in results)
        {
            foreach (var offer in result.offers.DistinctBy(x => (x.Hotel.Id, x.RoomId, x.RatePlanId, x.AgencyProviderId)))
            {
                var price = pricing.CalculateFinalPrice(offer);
                if (query.MinPrice is not null && price < query.MinPrice || query.MaxPrice is not null && price > query.MaxPrice
                    || query.Stars is { Length: > 0 } && !query.Stars.Contains(offer.Hotel.Stars)
                    || query.Accommodations is { Length: > 0 } && !query.Accommodations.Contains(offer.Hotel.AccommodationType, StringComparer.OrdinalIgnoreCase)) continue;
                var listing = await repository.ListingAsync(query.ReferenceCityId, result.provider.Name, offer.Hotel.Id, offer.Hotel.CityId, ct);
                var snapshot = HotelOfferSnapshot.Create(Convert.ToHexString(RandomNumberGenerator.GetBytes(24)), listing.HotelKey,
                    result.provider.Name, Serialize(offer), Serialize(query.Travel), DateTime.UtcNow.AddMinutes(20));
                repository.AddOffer(snapshot); offers.Add(await PublicOfferAsync(snapshot, offer, ct));
            }
        }
        await repository.SaveAsync(ct);
        var sorted = query.Sort switch { "price-desc" => offers.OrderByDescending(x => x.PriceMinor), "stars" => offers.OrderByDescending(x => x.Stars), _ => offers.OrderBy(x => x.PriceMinor) };
        return new(sorted.ToArray(), partial, partial ? "بخشی از نتایج در دسترس نیست." : null);
    }
    public async Task<HotelDetailsResponse> DetailsAsync(Guid key, HotelTravel travel, CancellationToken ct)
    {
        var listing = await repository.FindListingAsync(key, ct) ?? throw new InvalidOperationException("هتل یافت نشد.");
        var provider = providers.Get(listing.Provider);
        var hotel = await provider.DetailsAsync(listing.NativeId, listing.NativeCityId, ct);
        // Details remains bound to the selected provider. No fallback to a different hotel/provider.
        ValidateTravel(travel);
        var raw = await provider.SearchAsync(listing.NativeCityId, travel, ct);
        var offers = new List<HotelOfferDto>();
        foreach (var rawOffer in raw.Where(x => x.Hotel.Id == listing.NativeId))
        {
            ProviderHotelOffer offer;
            try { offer = await provider.ValidateOfferAsync(rawOffer, travel, ct); }
            catch (HotelCatalogueConflictException) { continue; }
            var snapshot = HotelOfferSnapshot.Create(Convert.ToHexString(RandomNumberGenerator.GetBytes(24)), key, provider.Name,
                Serialize(offer), Serialize(travel), DateTime.UtcNow.AddMinutes(20));
            repository.AddOffer(snapshot); offers.Add(await PublicOfferAsync(snapshot, offer, ct));
        }
        await repository.SaveAsync(ct);
        return new(key, hotel.Name, hotel.Address, hotel.Description, hotel.Stars, await PublishImagesAsync(provider.Name, hotel.Images, ct), hotel.Facilities ?? [], hotel.Policies,
            new(offers, false, null), hotel.CheckInTime, hotel.CheckOutTime);
    }
    public async Task<HotelOfferDto> OfferAsync(string token, CancellationToken ct)
    {
        var snapshot = await ValidOfferAsync(token, ct); return await PublicOfferAsync(snapshot, Read<ProviderHotelOffer>(snapshot.OfferJson), ct);
    }
    private readonly Dictionary<string, IReadOnlyList<string>> publishedImages = new(StringComparer.Ordinal);
    private async Task<IReadOnlyList<string>> PublishImagesAsync(string provider, IEnumerable<string> images, CancellationToken ct)
    {
        if (media is null) return [];
        var list = images.ToArray(); var key = provider + "\n" + string.Join("\n", list);
        if (!publishedImages.TryGetValue(key, out var result)) publishedImages[key] = result = await media.GetPublicUrlsAsync(list, new("Hotels", provider), ct);
        return result;
    }
    private async Task<HotelOfferDto> PublicOfferAsync(HotelOfferSnapshot snapshot, ProviderHotelOffer offer, CancellationToken ct) => new(snapshot.HotelKey, snapshot.Token,
        offer.Hotel.Name, offer.Hotel.Address, offer.Hotel.Stars, await PublishImagesAsync(snapshot.Provider, offer.Hotel.Images, ct), offer.RoomName, offer.BoardType, offer.Capacity,
        pricing.CalculateFinalPrice(offer), snapshot.ExpiresAtUtc, providers.Get(snapshot.Provider).Capabilities.Purchase
            && providers.Get(snapshot.Provider).IsOfferEligible(offer, Read<HotelTravel>(snapshot.TravelJson))
            && (snapshot.Provider != "Lamasoo" || offer.SupplyPriceMinor <= pricing.CalculateFinalPrice(offer)));
    public async Task<HotelOfferDto> QuoteAsync(string token, HotelTravel travel, CancellationToken ct)
    {
        ValidateTravel(travel);
        var original = await ValidOfferAsync(token, ct);
        var selected = Read<ProviderHotelOffer>(original.OfferJson);
        var offer = (await providers.Get(original.Provider).SearchFreshAsync(selected.Hotel.CityId, travel, ct))
            .FirstOrDefault(x => x.Hotel.Id == selected.Hotel.Id && x.RoomId == selected.RoomId && x.RatePlanId == selected.RatePlanId && x.AgencyProviderId == selected.AgencyProviderId)
            ?? throw new HotelCatalogueConflictException("اتاق انتخابی دیگر در دسترس نیست.");
        offer = await providers.Get(original.Provider).ValidateOfferAsync(offer, travel, ct);
        var snapshot = HotelOfferSnapshot.Create(Convert.ToHexString(RandomNumberGenerator.GetBytes(24)), original.HotelKey, original.Provider,
            Serialize(offer), Serialize(travel), DateTime.UtcNow.AddMinutes(20));
        repository.AddOffer(snapshot); await repository.SaveAsync(ct); return await PublicOfferAsync(snapshot, offer, ct);
    }
    private async Task<HotelOfferSnapshot> ValidOfferAsync(string token, CancellationToken ct)
    {
        var snapshot = await repository.OfferAsync(token, ct) ?? throw new InvalidOperationException("پیشنهاد هتل یافت نشد.");
        if (snapshot.ExpiresAtUtc <= DateTime.UtcNow) throw new HotelCatalogueConflictException("مهلت پیشنهاد هتل تمام شده است. دوباره جست‌وجو کنید.");
        return snapshot;
    }
    public async Task<CreateHotelRequestResponse> CreateAsync(CreateHotelCatalogueRequestCommand command, CancellationToken ct)
    {
        command = command with { IdempotencyKey = command.IdempotencyKey.Trim() };
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Serialize(command))));
        await using var held = await repository.LockAsync($"request:{command.UserId}:{command.IdempotencyKey}", ct);
        var existing = await requests.GetByIdempotencyKeyAsync(command.UserId, command.IdempotencyKey, ct);
        if (existing is not null)
        {
            if (existing.RequestFingerprint != hash) throw new HotelCatalogueConflictException("کلید تکرارپذیری با اطلاعات دیگری استفاده شده است.");
            var known = await repository.ReservationAsync(existing.Id, ct);
            var order = existing.OrderId is Guid orderId ? await mediator.Send(new GetOrderByIdQuery(orderId, existing.UserId, "User"), ct) : null;
            return Response(existing, known, order?.PaymentState ?? "Unpaid", order?.CanPay ?? true);
        }
        var snapshot = await ValidOfferAsync(command.OfferToken, ct);
        var provider = providers.Get(snapshot.Provider);
        if (!provider.Capabilities.Purchase) throw new InvalidOperationException("خرید این پیشنهاد هنوز فعال نیست.");
        var selected = Read<ProviderHotelOffer>(snapshot.OfferJson);
        var originalTravel = Read<HotelTravel>(snapshot.TravelJson);
        if (command.Travel.CheckIn != originalTravel.CheckIn || command.Travel.CheckOut != originalTravel.CheckOut || command.Travel.Rooms.Count != originalTravel.Rooms.Count
            || !command.Travel.Rooms.Zip(originalTravel.Rooms).All(x => x.First.Adults == x.Second.Adults && x.First.ChildrenAges.SequenceEqual(x.Second.ChildrenAges)))
            throw new HotelCatalogueConflictException("شرایط اقامت تغییر کرده است. دوباره جست‌وجو کنید.");
        var current = (await provider.SearchFreshAsync(selected.Hotel.CityId, command.Travel, ct)).FirstOrDefault(x =>
            x.Hotel.Id == selected.Hotel.Id && x.RoomId == selected.RoomId && x.RatePlanId == selected.RatePlanId && x.AgencyProviderId == selected.AgencyProviderId)
            ?? throw new HotelCatalogueConflictException("اتاق انتخابی دیگر در دسترس نیست.");
        current = await provider.ValidateOfferAsync(current, command.Travel, ct);
        var price = pricing.CalculateFinalPrice(current);
        if (price != command.ExpectedTotalPriceMinor) throw new HotelPriceChangedException(price);
        if (!provider.IsOfferEligible(current, command.Travel, command.Guests))
            throw new HotelCatalogueConflictException("این پیشنهاد با مشخصات مسافران قابل رزرو نیست.");
        if (snapshot.Provider == "Lamasoo" && current.SupplyPriceMinor > price)
            throw new HotelCatalogueConflictException("این پیشنهاد دیگر قابل خرید نیست. دوباره جست‌وجو کنید.");
        var travelJson = Serialize(new
        {
            cityId = int.TryParse(current.Hotel.CityId, out var cityId) ? cityId : 0,
            checkIn = command.Travel.CheckIn,
            checkOut = command.Travel.CheckOut,
            adults = command.Travel.Rooms.Sum(x => x.Adults),
            children = command.Travel.Rooms.Sum(x => x.ChildrenAges.Count),
            rooms = command.Travel.Rooms.Count,
            parties = command.Travel.Rooms
        });
        var guestJson = Serialize(new
        {
            phone = command.Phone,
            email = command.Email,
            guests = command.Guests.Select(x => new { fullName = $"{x.FirstName} {x.LastName}", age = x.Age, type = x.Type, nationality = x.Country, x.RoomIndex, x.FirstName, x.LastName, x.NationalCode })
        });
        var now = DateTime.UtcNow;
        var hotelRequest = HotelRequest.Create(command.UserId, snapshot.Provider,
            snapshot.Provider == "SnappTrip" ? long.Parse(current.Hotel.Id) : 0,
            snapshot.Provider == "SnappTrip" ? long.Parse(current.RoomId) : 0, travelJson,
            Serialize(new { name = current.Hotel.Name, address = current.Hotel.Address, stars = current.Hotel.Stars, images = current.Hotel.Images, nativeHotelId = current.Hotel.Id, snapshot.HotelKey }), Serialize(new { name = current.RoomName, boardType = current.BoardType, adultCapacity = current.AdultCapacity, current.RatePlanId, current.AgencyProviderId }),
            price, "IRR", Serialize(new
            {
                finalAmountMinor = price,
                pricingVersion = HotelRequest.CurrentPricingVersion,
                salePricingPolicyVersion = pricing.Version,
                estimatedSupplyPriceMinor = current.SupplyPriceMinor,
                feeMinor = current.FeeMinor,
                marginMinor = price - current.SupplyPriceMinor
            }), null, guestJson,
            now, now.AddMinutes(snapshot.Provider == "SnappTrip" ? 20 : 14), command.IdempotencyKey, current.Hotel.Id, current.RoomId);
        hotelRequest.AttachCatalogue(command.OfferToken, hash, current.Hotel.Id, current.RoomId);
        await requests.AddAsync(hotelRequest, ct);
        await sagas.AddAsync(HotelBookingSagaState.Start(command.UserId, hotelRequest.Id, now), ct);
        if (snapshot.Provider == "SnappTrip")
        {
            await repository.SaveAsync(ct);
            return Response(hotelRequest);
        }
        var reservation = HotelExternalReservation.Create(hotelRequest.Id, snapshot.Provider, hash,
            Serialize(new HotelSubmitRequest(hotelRequest.Id.ToString("N"), current, command.Travel, command.Guests, command.Phone, command.Email)));
        await using var reservationLease = await repository.LockAsync("reservation:" + hotelRequest.Id, ct);
        repository.AddReservation(reservation); reservation.StartSubmit();
        var operation = HotelExternalOperation.Start(reservation, HotelExternalOperationKind.Submit);
        repository.AddOperation(operation); await repository.SaveAsync(ct);
        using var submitScope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["Provider"] = provider.Name, ["HotelRequestId"] = hotelRequest.Id, ["ReferenceId"] = reservation.ReferenceId
        });
        logger.LogInformation("Hotel Submit started Provider={Provider} HotelRequestId={RequestId} ReferenceId={ReferenceId}", provider.Name, hotelRequest.Id, reservation.ReferenceId);
        var receivedSubmitResponse = false;
        try
        {
            var submitted = await provider.SubmitAsync(Read<HotelSubmitRequest>(reservation.SubmitJson), ct);
            receivedSubmitResponse = true;
            operation.Finish(HotelExternalOperationState.Succeeded, submitted.Snapshot);
            reservation.Reserve(submitted.SupplyPriceMinor, submitted.FeeMinor, submitted.LockedUntilUtc, submitted.Snapshot);
            if (submitted.SupplyPriceMinor > price) reservation.BlockPayment("SUPPLY_EXCEEDS_SALE");
            if (submitted.LockedUntilUtc <= DateTime.UtcNow.AddSeconds(Policy.PaymentSafetySeconds))
                reservation.MarkUnknown("LOCK_TOO_SHORT");
            else hotelRequest.SetPayableUntil(submitted.LockedUntilUtc.AddSeconds(-Policy.PaymentSafetySeconds));
            await repository.SaveAsync(ct);
            logger.LogInformation("Hotel Submit reserved Provider={Provider} HotelRequestId={RequestId} ReferenceId={ReferenceId} PayableUntil={PayableUntil}", provider.Name, hotelRequest.Id, reservation.ReferenceId, hotelRequest.ExpireAt);
        }
        catch (Exception ex)
        {
            if (!receivedSubmitResponse && ex is HotelProviderRejectedException rejected)
            {
                operation.Finish(HotelExternalOperationState.Rejected, rejected.Snapshot, errorCode: rejected.Code);
                reservation.Fail(rejected.Code); hotelRequest.MarkFailed(DateTime.UtcNow);
            }
            else
            {
                operation.Finish(HotelExternalOperationState.Unknown, errorCode: "SUBMIT_UNKNOWN");
                reservation.MarkUnknown("SUBMIT_UNKNOWN");
            }
            await repository.SaveAsync(CancellationToken.None);
            logger.LogWarning("Hotel Submit outcome requires follow-up RequestId={RequestId} State={State}", hotelRequest.Id, reservation.State);
        }
        return Response(hotelRequest, reservation);
    }
    public async Task FinalizeAsync(Guid orderId, Guid userId, CancellationToken ct)
    {
        var request = await requests.GetByOrderIdAsync(orderId, ct) ?? throw new InvalidOperationException("درخواست هتل یافت نشد.");
        await using var held = await repository.LockAsync("reservation:" + request.Id, ct);
        request = await requests.GetByOrderIdAsync(orderId, ct) ?? request;
        var reservation = await repository.ReservationAsync(request.Id, ct) ?? throw new InvalidOperationException("رزرو خارجی یافت نشد.");
        var order = await mediator.Send(new GetOrderByIdQuery(orderId, userId, "User"), ct);
        if (request.UserId != userId || order?.PaymentState != "Paid" || order.SourceModule != "Hotel" || order.SourceReferenceId != request.Id || order.ReferenceType != "HotelRequest"
            || order.FinalAmountMinor != request.TotalPrice) throw new InvalidOperationException("پرداخت رزرو تأیید نشده است.");
        if (reservation.State == HotelExternalState.Confirmed) return;
        if (reservation.State != HotelExternalState.Reserved || reservation.PaymentBlocked) throw new HotelExternalOutcomeUnknownException("رزرو در حال بررسی است.");
        if (reservation.LockedUntilUtc <= DateTime.UtcNow)
        {
            reservation.MarkUnknown(); await repository.SaveAsync(ct);
            throw new HotelExternalOutcomeUnknownException("مهلت رزرو خارجی تمام شده و نیاز به بررسی دارد.");
        }
        var saga = await sagas.GetByHotelRequestIdAsync(request.Id, ct);
        if (saga is not null) { saga.MarkPaid(orderId, DateTime.UtcNow); saga.MarkProviderBookingStarted(DateTime.UtcNow); }
        using var confirmScope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["Provider"] = reservation.Provider, ["HotelRequestId"] = request.Id, ["ReferenceId"] = reservation.ReferenceId,
            ["OrderId"] = orderId, ["SagaId"] = saga?.SagaId
        });
        reservation.StartConfirm();
        var operation = HotelExternalOperation.Start(reservation, HotelExternalOperationKind.Confirm);
        repository.AddOperation(operation); await repository.SaveAsync(ct);
        logger.LogInformation("Hotel Confirm started Provider={Provider} HotelRequestId={RequestId} OrderId={OrderId} ReferenceId={ReferenceId}", reservation.Provider, request.Id, orderId, reservation.ReferenceId);
        try
        {
            var confirmed = await providers.Get(reservation.Provider).ConfirmAsync(reservation.ReferenceId, ct);
            if (confirmed.Status != "Confirmed" || string.IsNullOrWhiteSpace(confirmed.ReservationId))
                throw new HotelExternalOutcomeUnknownException("پاسخ تأیید رزرو معتبر نیست.");
            operation.Finish(HotelExternalOperationState.Succeeded, confirmed.Snapshot ?? Serialize(confirmed));
            reservation.Confirm(confirmed.ReservationId); request.MarkProviderConfirmed(confirmed.ReservationId, DateTime.UtcNow); request.Complete(DateTime.UtcNow);
            if (saga is not null) { saga.MarkProviderBookingConfirmed(DateTime.UtcNow); saga.Complete(DateTime.UtcNow); }
            await repository.SaveAsync(ct);
        }
        catch (Exception ex)
        {
            operation.Finish(ex is HotelProviderRejectedException ? HotelExternalOperationState.Rejected : HotelExternalOperationState.Unknown,
                ex is HotelProviderRejectedException rejection ? rejection.Snapshot : null,
                errorCode: ex is HotelProviderRejectedException rejected ? rejected.Code : "CONFIRM_UNKNOWN");
            reservation.MarkUnknown(ex is HotelProviderRejectedException e ? e.Code : "CONFIRM_UNKNOWN"); await repository.SaveAsync(CancellationToken.None);
            logger.LogError("Hotel confirmation requires reconciliation Provider={Provider} HotelRequestId={RequestId} OrderId={OrderId}", reservation.Provider, request.Id, orderId);
            throw new HotelExternalOutcomeUnknownException("وضعیت رزرو نیاز به پیگیری دارد.");
        }
    }
    private CreateHotelRequestResponse Response(HotelRequest request, HotelExternalReservation? reservation = null, string payment = "Unpaid", bool orderCanPay = true)
    {
        var status = PublicStatus(request, reservation, payment);
        return new(request.Id, request.Status.ToString(), request.ExpireAt, request.TotalPrice, request.Currency,
            status, orderCanPay && payment is not ("Paid" or "Refunded") && CanProceed(request, reservation));
    }
    internal static void ValidateTravel(HotelTravel travel)
    {
        if (travel is null || travel.Rooms is null || travel.Rooms.Count is < 1 or > 8 || travel.CheckIn < DateOnly.FromDateTime(DateTime.Now)
            || travel.CheckOut <= travel.CheckIn || travel.CheckOut.DayNumber - travel.CheckIn.DayNumber > 30
            || travel.Rooms.Any(x => x is null || x.Adults is < 1 or > 10 || x.ChildrenAges is null || x.ChildrenAges.Count > 6 || x.ChildrenAges.Any(a => a is < 0 or > 12)))
            throw new FluentValidation.ValidationException("شرایط اقامت و مسافران معتبر نیست.");
    }
}
