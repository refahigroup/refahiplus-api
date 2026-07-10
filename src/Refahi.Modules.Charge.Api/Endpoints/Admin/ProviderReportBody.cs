namespace Refahi.Modules.Charge.Api.Endpoints;

public sealed record ProviderReportBody(
    int PageNumber = 1,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null);
