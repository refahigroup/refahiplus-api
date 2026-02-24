namespace Refahi.Shared.Presentation;

/// <summary>
/// Error detail for validation errors
/// </summary>
public sealed record ValidationError(
    string Field,
    string[] Messages
);

