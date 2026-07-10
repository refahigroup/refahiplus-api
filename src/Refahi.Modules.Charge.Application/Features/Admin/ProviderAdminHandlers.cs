using MediatR;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Modules.Charge.Application.Contracts.Providers;

namespace Refahi.Modules.Charge.Application.Features.Admin;

public sealed class ProviderAdminHandlers :
    IRequestHandler<GetProviderBalanceQuery, ProviderBalanceDto>,
    IRequestHandler<GetProviderErrorsQuery, IReadOnlyList<ProviderErrorDto>>,
    IRequestHandler<GetProviderChannelsQuery, IReadOnlyList<ProviderChannelDto>>,
    IRequestHandler<GetProviderTransactionReportQuery, ProviderReportDto>,
    IRequestHandler<GetProviderWalletChargeReportQuery, ProviderReportDto>
{

    private readonly IChargeProviderResolver _providers; public ProviderAdminHandlers(IChargeProviderResolver providers) => 
        _providers = providers;

    public Task<ProviderBalanceDto> Handle(GetProviderBalanceQuery q, CancellationToken ct) => 
        _providers.GetDefault()
                  .GetBalanceAsync(ct);

    public Task<IReadOnlyList<ProviderErrorDto>> Handle(GetProviderErrorsQuery q, CancellationToken ct) => 
        _providers.GetDefault()
                  .GetErrorsAsync(ct);

    public Task<IReadOnlyList<ProviderChannelDto>> Handle(GetProviderChannelsQuery q, CancellationToken ct) => 
        _providers.GetDefault()
                  .GetChannelsAsync(ct);

    public Task<ProviderReportDto> Handle(GetProviderTransactionReportQuery q, CancellationToken ct) => 
        _providers.GetDefault()
                  .GetTransactionReportAsync(new(q.PageNumber, q.FromDate, q.ToDate), ct);

    public Task<ProviderReportDto> Handle(GetProviderWalletChargeReportQuery q, CancellationToken ct) => 
        _providers.GetDefault()
                  .GetWalletChargeReportAsync(new(q.PageNumber, q.FromDate, q.ToDate), ct);
}
