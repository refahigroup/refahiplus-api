using FluentValidation;
using MediatR;
using Refahi.Modules.Entertainments.Application.Contracts.Discovery;
using Refahi.Modules.Entertainments.Application.Contracts.Providers;
using Refahi.Modules.Entertainments.Application.Contracts.Providers.Dtos;
using Refahi.Modules.Entertainments.Domain;
namespace Refahi.Modules.Entertainments.Application.Features.Catalog.Offers;

public sealed record GetEntertainmentRequirementsQuery(string ProgramKey) : IRequest<EntertainmentPurchaseRequirements>;
public sealed record GetEntertainmentOffersQuery(string ProgramKey, DateOnly? Start, DateOnly? End) : IRequest<IReadOnlyList<EntertainmentOfferDto>>;
public sealed record GetEntertainmentFiltersQuery() : IRequest<EntertainmentCatalogFilters>;
public sealed class EntertainmentOfferQueries(IEntertainmentProviderFactory providers, IEntertainmentProgramIdentity identities, IEntertainmentContentReader content)
    : IRequestHandler<GetEntertainmentOffersQuery, IReadOnlyList<EntertainmentOfferDto>>, IRequestHandler<GetEntertainmentRequirementsQuery, EntertainmentPurchaseRequirements>,
      IRequestHandler<GetEntertainmentFiltersQuery, EntertainmentCatalogFilters>
{
    private async Task<IEntertainmentOfferProvider> Provider(string key, CancellationToken ct)
    {
        var identity = await identities.ResolveAsync(Guid.Parse(key), ct) ?? throw new EntertainmentDomainException("خدمت در دسترس نیست", "PROGRAM_UNAVAILABLE");
        return providers.GetRequired(identity.ProviderKey) as IEntertainmentOfferProvider
            ?? throw new EntertainmentDomainException("فروش این خدمت در دسترس نیست", "OFFERS_UNAVAILABLE");
    }
    public async Task<IReadOnlyList<EntertainmentOfferDto>> Handle(GetEntertainmentOffersQuery request, CancellationToken ct) =>
        (await (await Provider(request.ProgramKey, ct)).GetOffersAsync(request.ProgramKey, request.Start, request.End, ct))
            .Select(x => x with { ProgramKey = Guid.Parse(request.ProgramKey).ToString("N") }).ToArray();
    public async Task<EntertainmentPurchaseRequirements> Handle(GetEntertainmentRequirementsQuery request, CancellationToken ct) =>
        await (await Provider(request.ProgramKey, ct)).GetRequirementsAsync(request.ProgramKey, ct);
    public Task<EntertainmentCatalogFilters> Handle(GetEntertainmentFiltersQuery request, CancellationToken ct) => content.FiltersAsync(ct);
}
public sealed class GetEntertainmentRequirementsValidator : AbstractValidator<GetEntertainmentRequirementsQuery>
{
    public GetEntertainmentRequirementsValidator() => RuleFor(x => x.ProgramKey).Must(x => Guid.TryParse(x, out var id) && id != Guid.Empty).WithMessage("شناسه خدمت معتبر نیست");
}
public sealed class GetEntertainmentOffersValidator : AbstractValidator<GetEntertainmentOffersQuery>
{
    public GetEntertainmentOffersValidator()
    {
        RuleFor(x => x.ProgramKey).Must(x => Guid.TryParse(x, out var id) && id != Guid.Empty).WithMessage("شناسه خدمت معتبر نیست");
        RuleFor(x => x).Must(x => x.Start.HasValue == x.End.HasValue && (!x.Start.HasValue || x.End >= x.Start && x.End!.Value.DayNumber - x.Start.Value.DayNumber <= 6))
            .WithMessage("بازه انتخاب سانس باید حداکثر هفت روز باشد");
    }
}
