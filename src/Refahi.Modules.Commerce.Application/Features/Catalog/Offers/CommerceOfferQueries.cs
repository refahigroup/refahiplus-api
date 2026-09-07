using FluentValidation;
using MediatR;
using Refahi.Modules.Commerce.Application.Contracts.Providers;
using Refahi.Modules.Commerce.Application.Contracts.Providers.Dtos;

namespace Refahi.Modules.Commerce.Application.Features.Catalog.Offers;

public sealed record GetCommerceOffersQuery(string ProviderKey, string ProductKey, DateOnly? Start, DateOnly? End) : IRequest<IReadOnlyList<CommerceOfferDto>>;
public sealed record GetCommerceFiltersQuery(string? ProviderKey) : IRequest<CommerceCatalogFilters>;
public sealed class CommerceOfferQueries(ICommerceProviderFactory providers) : IRequestHandler<GetCommerceOffersQuery, IReadOnlyList<CommerceOfferDto>>,
    IRequestHandler<GetCommerceFiltersQuery, CommerceCatalogFilters>
{
    public async Task<IReadOnlyList<CommerceOfferDto>> Handle(GetCommerceOffersQuery request, CancellationToken ct)
    {
        var provider = providers.GetRequired(request.ProviderKey);
        return provider is ICommerceOfferProvider offers ? await offers.GetOffersAsync(request.ProductKey, request.Start, request.End, ct)
            : (await provider.GetProductAsync(request.ProductKey, ct))?.Offers ?? [];
    }
    public async Task<CommerceCatalogFilters> Handle(GetCommerceFiltersQuery request, CancellationToken ct)
    {
        var selected = string.IsNullOrWhiteSpace(request.ProviderKey)
            ? providers.GetEnabledProviders()
            : [providers.GetRequired(request.ProviderKey)];
        var locations = new List<CommerceFilterOption>(); var categories = new List<CommerceFilterOption>();
        foreach (var provider in selected.OfType<ICommerceOfferProvider>())
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
public sealed class GetCommerceOffersValidator : AbstractValidator<GetCommerceOffersQuery>
{
    public GetCommerceOffersValidator()
    {
        RuleFor(x => x.ProviderKey).NotEmpty().MaximumLength(80).WithMessage("پرووایدر معتبر نیست");
        RuleFor(x => x.ProductKey).NotEmpty().MaximumLength(300).WithMessage("محصول معتبر نیست");
        RuleFor(x => x).Must(x => x.Start.HasValue == x.End.HasValue && (!x.Start.HasValue || x.End >= x.Start && x.End!.Value.DayNumber - x.Start.Value.DayNumber <= 6))
            .WithMessage("بازه انتخاب سانس باید حداکثر هفت روز باشد");
    }
}
