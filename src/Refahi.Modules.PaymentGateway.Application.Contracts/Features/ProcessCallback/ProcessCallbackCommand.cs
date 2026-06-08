using MediatR;
using Refahi.Modules.PaymentGateway.Domain.Enums;
using System;

namespace Refahi.Modules.PaymentGateway.Application.Contracts.Features.ProcessCallback;

/// <summary>
/// Processes the POST callback from the payment provider.
/// Verifies the transaction, tops up the wallet on success,
/// and returns the URL to redirect the user's browser.
/// </summary>
public sealed record ProcessCallbackCommand(
    PaymentGatewayProviderType Provider,
    /// <summary>Provider's transaction state (e.g. "OK" for SEP).</summary>
    string State,
    /// <summary>Provider's reference number (used for verification).</summary>
    string? RefNum,
    /// <summary>Session ID (sent to provider as ResNum).</summary>
    string ResNum,
    string? TraceNo,
    string? SecurePan,
    /// <summary>Full serialized form data for audit logging.</summary>
    string? RawCallbackJson,
    string? Status = null,
    string? TerminalId = null,
    string? MID = null,
    string? Rrn = null,
    long? AmountMinor = null,
    bool AmountParseFailed = false,
    long? WageMinor = null,
    long? AffectiveAmountMinor = null,
    string? HashedCardNumber = null,
    string? Token = null
) : IRequest<ProcessCallbackResponse>;
