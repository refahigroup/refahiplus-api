using MediatR;
using FluentValidation;
using Refahi.Modules.Hotels.Application.Contracts.Catalogue;
using Refahi.Modules.Hotels.Application.Contracts.Services.HotelRequests.CreateHotelRequest;
namespace Refahi.Modules.Hotels.Application.Features.Catalogue;

public sealed class HotelCatalogueHandlers(HotelCatalogueService service) :
    IRequestHandler<GetHotelCitiesQuery, IReadOnlyList<HotelCityDto>>,
    IRequestHandler<SearchHotelCatalogueQuery, HotelSearchResponse>,
    IRequestHandler<GetHotelCatalogueDetailsQuery, HotelDetailsResponse>,
    IRequestHandler<GetHotelOfferQuery, HotelOfferDto>,
    IRequestHandler<QuoteHotelOfferQuery, HotelOfferDto>,
    IRequestHandler<CreateHotelCatalogueRequestCommand, CreateHotelRequestResponse>,
    IRequestHandler<FinalizeHotelCatalogueCommand>, IRequestHandler<RecoverHotelCatalogueCommand>,
    IRequestHandler<GetHotelBookingStatusQuery, HotelBookingStatusDto>
{
    public Task<IReadOnlyList<HotelCityDto>> Handle(GetHotelCitiesQuery q, CancellationToken ct) => service.CitiesAsync(q.Name, ct);
    public Task<HotelSearchResponse> Handle(SearchHotelCatalogueQuery q, CancellationToken ct) => service.SearchAsync(q, ct);
    public Task<HotelDetailsResponse> Handle(GetHotelCatalogueDetailsQuery q, CancellationToken ct) => service.DetailsAsync(q.HotelKey, q.Travel, ct);
    public Task<HotelOfferDto> Handle(GetHotelOfferQuery q, CancellationToken ct) => service.OfferAsync(q.OfferToken, ct);
    public Task<HotelOfferDto> Handle(QuoteHotelOfferQuery q, CancellationToken ct) => service.QuoteAsync(q.OfferToken, q.Travel, ct);
    public Task<CreateHotelRequestResponse> Handle(CreateHotelCatalogueRequestCommand q, CancellationToken ct) => service.CreateAsync(q, ct);
    public async Task<Unit> Handle(FinalizeHotelCatalogueCommand q, CancellationToken ct) { await service.FinalizeAsync(q.OrderId, q.UserId, ct); return Unit.Value; }
    public async Task<Unit> Handle(RecoverHotelCatalogueCommand q, CancellationToken ct) { await service.RecoverAsync(ct, q.ReconciliationOnly); return Unit.Value; }
    public Task<HotelBookingStatusDto> Handle(GetHotelBookingStatusQuery q, CancellationToken ct) => service.StatusAsync(q, ct);
}
public sealed class CreateHotelCatalogueRequestValidator : AbstractValidator<CreateHotelCatalogueRequestCommand>
{
    public CreateHotelCatalogueRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("کاربر الزامی است.");
        RuleFor(x => x.OfferToken).NotEmpty().MaximumLength(64).WithMessage("پیشنهاد هتل معتبر نیست.");
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(200).WithMessage("کلید تکرارپذیری الزامی است.");
        RuleFor(x => x.Phone).Matches("^09[0-9]{9}$").WithMessage("تلفن همراه معتبر نیست.");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email)).WithMessage("ایمیل معتبر نیست.");
        RuleFor(x => x.ExpectedTotalPriceMinor).GreaterThan(0).WithMessage("مبلغ معتبر نیست.");
        RuleFor(x => x.Travel).NotNull().WithMessage("شرایط اقامت الزامی است."); RuleFor(x => x.Guests).NotNull().WithMessage("مسافران الزامی هستند.");
        RuleFor(x => x).Custom((command, context) =>
        {
            if (command.Travel is null || command.Guests is null) return;
            try { HotelCatalogueService.ValidateTravel(command.Travel); }
            catch (ValidationException) { context.AddFailure("Travel", "شرایط اقامت معتبر نیست."); return; }
            for (var index = 0; index < command.Travel.Rooms.Count; index++)
            {
                var party = command.Travel.Rooms[index]; var guests = command.Guests.Where(x => x is not null && x.RoomIndex == index).ToArray();
                if (guests.Count(x => x.Type == "Adult") != party.Adults
                    || !guests.Where(x => x.Type == "Child").Select(x => x.Age).Order().SequenceEqual(party.ChildrenAges.Order()))
                    context.AddFailure("Guests", "تخصیص مسافران با ترکیب اتاق‌ها مطابقت ندارد.");
            }
            foreach (var guest in command.Guests)
            {
                if (guest is null) { context.AddFailure("Guests", "مشخصات مسافر معتبر نیست."); continue; }
                if (string.IsNullOrWhiteSpace(guest.FirstName) || string.IsNullOrWhiteSpace(guest.LastName) || guest.FirstName.Length > 100 || guest.LastName.Length > 100
                    || (guest.Country is null || guest.Country.Length != 2) || (guest.Country is not null && !guest.Country.All(char.IsAsciiLetterUpper)) || guest.RoomIndex < 0 || guest.RoomIndex >= command.Travel.Rooms.Count
                    || guest.Type is not ("Adult" or "Child") || guest.Age < 0 || guest.Age > 120 || (guest.Type == "Adult" && guest.Age < 13) || (guest.Type == "Child" && guest.Age > 12))
                    context.AddFailure("Guests", "مشخصات مسافر معتبر نیست.");
                if (guest.Country == "IR" && !ValidNationalCode(guest.NationalCode)) context.AddFailure("Guests", "کد ملی معتبر نیست.");
            }
        });
    }
    internal static bool ValidNationalCode(string? value)
    {
        if (value is null || value.Length != 10 || !value.All(char.IsAsciiDigit) || value.Distinct().Count() == 1) return false;
        var sum = Enumerable.Range(0, 9).Sum(i => (value[i] - '0') * (10 - i)) % 11;
        return value[9] - '0' == (sum < 2 ? sum : 11 - sum);
    }
}
