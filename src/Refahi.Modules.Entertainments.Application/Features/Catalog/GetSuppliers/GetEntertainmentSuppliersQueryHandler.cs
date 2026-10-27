using MediatR;
using Refahi.Modules.Entertainments.Application.Contracts.Providers;
using Refahi.Modules.Entertainments.Application.Contracts.Providers.Dtos;
namespace Refahi.Modules.Entertainments.Application.Features.Catalog.GetSuppliers;

public sealed class GetEntertainmentSuppliersQueryHandler(IEntertainmentProviderFactory providers) : IRequestHandler<GetEntertainmentSuppliersQuery, EntertainmentSupplierPage>
{
    public async Task<EntertainmentSupplierPage> Handle(GetEntertainmentSuppliersQuery request, CancellationToken ct)
    {
        var all = new List<EntertainmentSupplierDto>(); 
        var unavailable = new List<string>();

        var enabledProviders = providers.GetEnabledProviders()
                                        .Where(p => string.IsNullOrWhiteSpace(request.ProviderKey) || p.Key == request.ProviderKey);

        foreach (var provider in enabledProviders)
        {
            try
            {
                var suppliers = await provider.GetSuppliersAsync(ct);

                if (!string.IsNullOrWhiteSpace(request.LocationCode) || !string.IsNullOrWhiteSpace(request.CategoryCode))
                {
                    var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    for (var page = 1; page <= 1000; page++)
                    {
                        var programs = await provider.GetProgramsAsync(new(ProviderKey: provider.Key, PageNumber: page, PageSize: 100), ct);

                        var list = programs.Items.Where(p => 
                            (string.IsNullOrWhiteSpace(request.LocationCode) || 
                            p.LocationCode == request.LocationCode) &&
                            (string.IsNullOrWhiteSpace(request.CategoryCode) || 
                            p.ExternalCategoryCode == request.CategoryCode)
                        );

                        foreach (var p in list) 
                            matched.Add(p.SupplierKey);

                        if (page >= programs.TotalPages || programs.Items.Count == 0) 
                            break;
                    }

                    suppliers = suppliers.Where(x => matched.Contains(x.SupplierKey))
                                     .ToArray();
                }

                all.AddRange(suppliers);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) 
            { 
                throw; 
            }
            catch 
            { 
                unavailable.Add(provider.Key); 
            }
        }
        var sorted = all.DistinctBy(x => (x.ProviderKey, x.SupplierKey))
                        .OrderBy(x => x.Title).ThenBy(x => x.ProviderKey)
                        .ThenBy(x => x.SupplierKey)
                        .ToArray();

        var number = Math.Max(1, request.PageNumber); 
        var size = Math.Clamp(request.PageSize, 1, 100);

        return new(
            sorted.Skip((number - 1) * size).Take(size).ToArray(), 
            number, 
            size, 
            sorted.Length
        ) 
        { 
            UnavailableProviders = unavailable 
        };
    }
}
