using System;

namespace Refahi.Modules.PaymentGateway.Application.Contracts.Features.ProcessCallback;

public sealed record ProcessCallbackResponse(
    Guid SessionId,
    bool IsSuccess,
    /// <summary>The URL to HTTP 302 redirect the user's browser to.</summary>
    string BrowserRedirectUrl
);
