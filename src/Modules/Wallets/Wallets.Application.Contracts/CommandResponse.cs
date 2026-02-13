namespace Wallets.Application.Contracts;

public sealed record CommandResponse<T>(CommandStatus Status, T? Data);
