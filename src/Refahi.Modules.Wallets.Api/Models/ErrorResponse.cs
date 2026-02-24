namespace Refahi.Modules.Wallets.Api.Models;

/// <summary>
/// Standardized error response for all Payment Intent operations.
/// </summary>
public sealed record ErrorResponse(string Code, string Message);
