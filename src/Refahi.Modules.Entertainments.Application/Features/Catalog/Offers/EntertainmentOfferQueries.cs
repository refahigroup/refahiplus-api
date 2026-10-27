using FluentValidation;
using MediatR;
using Refahi.Modules.Entertainments.Application.Contracts.Providers;
using Refahi.Modules.Entertainments.Application.Contracts.Providers.Dtos;

namespace Refahi.Modules.Entertainments.Application.Features.Catalog.Offers;

public sealed record GetEntertainmentRequirementsQuery(string ProgramKey) : IRequest<EntertainmentPurchaseRequirements>;
public sealed class GetEntertainmentRequirementsHandler(IEntertainmentProviderFactory providers) : IRequestHandler<GetEntertainmentRequirementsQuery, EntertainmentPurchaseRequirements>
{
    public Task<EntertainmentPurchaseRequirements> Handle(GetEntertainmentRequirementsQuery request, CancellationToken ct) =>
        ((IEntertainmentOfferProvider)providers.GetRequired("touristpanel")).GetRequirementsAsync(request.ProgramKey, ct);
}
public sealed class GetEntertainmentRequirementsValidator : AbstractValidator<GetEntertainmentRequirementsQuery>
{
    public GetEntertainmentRequirementsValidator() => RuleFor(x => x.ProgramKey).Must(x => Guid.TryParse(x, out var id) && id != Guid.Empty).WithMessage("شناسه خدمت معتبر نیست");
}
public sealed record GetEntertainmentOffersQuery(string ProviderKey, string ProgramKey, DateOnly? Start, DateOnly? End) : IRequest<IReadOnlyList<EntertainmentOfferDto>>;
public sealed record GetEntertainmentFiltersQuery(string? ProviderKey) : IRequest<EntertainmentCatalogFilters>;
public sealed class EntertainmentOfferQueries(IEntertainmentProviderFactory providers) : IRequestHandler<GetEntertainmentOffersQuery, IReadOnlyList<EntertainmentOfferDto>>,
    IRequestHandler<GetEntertainmentFiltersQuery, EntertainmentCatalogFilters>
{
    public async Task<IReadOnlyList<EntertainmentOfferDto>> Handle(GetEntertainmentOffersQuery request, CancellationToken ct)
    {
        var provider = providers.GetRequired(request.ProviderKey);
        return provider is IEntertainmentOfferProvider offers ? await offers.GetOffersAsync(request.ProgramKey, request.Start, request.End, ct)
            : (await provider.GetProgramAsync(request.ProgramKey, ct))?.Offers ?? [];
    }
    public async Task<EntertainmentCatalogFilters> Handle(GetEntertainmentFiltersQuery request, CancellationToken ct)
    {
        var selected = string.IsNullOrWhiteSpace(request.ProviderKey)
            ? providers.GetEnabledProviders()
            : [providers.GetRequired(request.ProviderKey)];
        var locations = new List<EntertainmentFilterOption>(); var categories = new List<EntertainmentFilterOption>();
        foreach (var provider in selected.OfType<IEntertainmentOfferProvider>())
        {
            try
            {
                var value = await provider.GetFiltersAsync(ct);
                locations.AddRange(value.Locations); categories.AddRange(value.Categories);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch { /* Catalog endpoints report provider availability on their primary page response. */ }
        }
        return new(locations.DistinctBy(x => x.Key).OrderBy(x => x.Title).ToArray(),
            categories.DistinctBy(x => x.Key).OrderBy(x => x.Title).ToArray());
    }
}
public sealed class GetEntertainmentOffersValidator : AbstractValidator<GetEntertainmentOffersQuery>
{
    public GetEntertainmentOffersValidator()
    {
        RuleFor(x => x.ProviderKey).NotEmpty().MaximumLength(80).WithMessage("پرووایدر معتبر نیست");
        RuleFor(x => x.ProgramKey).NotEmpty().MaximumLength(300).WithMessage("محصول معتبر نیست");
        RuleFor(x => x).Must(x => x.Start.HasValue == x.End.HasValue && (!x.Start.HasValue || x.End >= x.Start && x.End!.Value.DayNumber - x.Start.Value.DayNumber <= 6))
            .WithMessage("بازه انتخاب سانس باید حداکثر هفت روز باشد");
    }
}
