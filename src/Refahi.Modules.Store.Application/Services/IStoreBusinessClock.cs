namespace Refahi.Modules.Store.Application.Services;

public interface IStoreBusinessClock
{
    StoreBusinessMoment Current { get; }
}

public readonly record struct StoreBusinessMoment(DateOnly Date, TimeOnly Time);
