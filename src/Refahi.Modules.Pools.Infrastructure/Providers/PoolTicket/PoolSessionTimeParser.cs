using Refahi.Modules.Pools.Application.Contracts.Public;
namespace Refahi.Modules.Pools.Infrastructure.Providers.PoolTicket;
public sealed class PoolSessionTimeParser : IPoolSessionTimeParser
{
    public (DateTimeOffset? Start, DateTimeOffset? End) Parse(string date, string time) => PoolSessionTime.Parse(date, time);
}
