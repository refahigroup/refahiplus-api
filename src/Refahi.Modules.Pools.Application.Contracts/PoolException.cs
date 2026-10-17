namespace Refahi.Modules.Pools.Application.Contracts;

public sealed class PoolException(string message, int status = 409, string code = "POOLS_CONFLICT", object? details = null) : Exception(message)
{
    public string Code { get; } = code;
    public object? Details { get; } = details;
    public int Status { get; } = status;
}
