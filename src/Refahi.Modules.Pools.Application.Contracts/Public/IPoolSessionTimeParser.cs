namespace Refahi.Modules.Pools.Application.Contracts.Public;
public interface IPoolSessionTimeParser { (DateTimeOffset? Start, DateTimeOffset? End) Parse(string date, string time); }
