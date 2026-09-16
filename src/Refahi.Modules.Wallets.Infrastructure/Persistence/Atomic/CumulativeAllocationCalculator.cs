using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Refahi.Modules.Wallets.Infrastructure.Persistence.Atomic;

internal static class CumulativeAllocationCalculator
{
    internal sealed record Item(Guid StableId, long OriginalAmount, long PreviouslyAllocated);
    internal sealed record Result(Guid StableId, long TargetCumulative, long Increment);

    public static IReadOnlyList<Result> Calculate(
        long originalTotal,
        long targetCumulativeTotal,
        IReadOnlyList<Item> items
    )
    {
        if (originalTotal <= 0 || targetCumulativeTotal < 0 || targetCumulativeTotal > originalTotal)
            throw new ArgumentOutOfRangeException(nameof(targetCumulativeTotal));
        if (items.Count == 0 || items.Any(x => x.OriginalAmount < 0 || x.PreviouslyAllocated < 0 || x.PreviouslyAllocated > x.OriginalAmount))
            throw new InvalidOperationException("طرح تخصیص تجمعی معتبر نیست.");
        if (items.Sum(x => x.OriginalAmount) != originalTotal)
            throw new InvalidOperationException("مجموع تخصیص‌های اصلی با مبلغ مبنا برابر نیست.");

        var previousTotal = items.Sum(x => x.PreviouslyAllocated);
        if (previousTotal > targetCumulativeTotal)
            throw new InvalidOperationException("مبلغ تجمعی هدف از تخصیص‌های قبلی کمتر است.");

        var candidates = items.Select(item =>
        {
            var product = (BigInteger)targetCumulativeTotal * item.OriginalAmount;
            var floor = (long)(product / originalTotal);
            var remainder = product % originalTotal;
            return new Candidate(item, Math.Max(item.PreviouslyAllocated, floor), remainder);
        }).ToList();

        var assigned = candidates.Sum(x => x.Target);
        if (assigned > targetCumulativeTotal)
        {
            // Hamilton apportionment can very rarely lose a unit as the cumulative
            // total grows. Preserve monotonicity and allocate only the new amount.
            foreach (var candidate in candidates)
                candidate.Target = candidate.Item.PreviouslyAllocated;
            assigned = previousTotal;
        }

        var remaining = targetCumulativeTotal - assigned;
        while (remaining > 0)
        {
            var eligible = candidates
                .Where(x => x.Target < x.Item.OriginalAmount)
                .OrderByDescending(x => x.Remainder)
                .ThenBy(x => x.Item.StableId)
                .ToList();
            if (eligible.Count == 0)
                throw new InvalidOperationException("ظرفیت کافی برای تخصیص مبلغ بازپرداخت وجود ندارد.");

            var take = (int)Math.Min(remaining, eligible.Count);
            for (var index = 0; index < take; index++)
                eligible[index].Target++;
            remaining -= take;
        }

        return candidates
            .OrderBy(x => x.Item.StableId)
            .Select(x => new Result(
                x.Item.StableId,
                x.Target,
                x.Target - x.Item.PreviouslyAllocated
            ))
            .ToList();
    }

    private sealed class Candidate(Item item, long target, BigInteger remainder)
    {
        public Item Item { get; } = item;
        public long Target { get; set; } = target;
        public BigInteger Remainder { get; } = remainder;
    }
}
