using System;
using MediatR;
using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Responses;

namespace Refahi.Modules.Wallets.Application.Contracts.Queries;

/// <summary>
/// Query to detect drift between ledger and projection without modifying data.
/// </summary>
public record DetectDriftQuery(
    Guid WalletId
) : IRequest<CommandResponse<DriftReportResponse>>;
