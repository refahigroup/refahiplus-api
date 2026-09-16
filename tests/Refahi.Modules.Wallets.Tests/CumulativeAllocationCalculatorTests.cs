using Refahi.Modules.Wallets.Infrastructure.Persistence.Atomic;
using Xunit;

namespace Refahi.Modules.Wallets.Tests;

public sealed class CumulativeAllocationCalculatorTests
{
    [Fact]
    public void MultiplePartialRefunds_EndAtExactOriginalAllocation()
    {
        var first = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var second = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var previous = new Dictionary<Guid, long> { [first] = 0, [second] = 0 };

        foreach (var cumulative in new long[] { 2_000, 5_000, 10_000 })
        {
            var result = CumulativeAllocationCalculator.Calculate(
                10_000,
                cumulative,
                [
                    new(first, 6_000, previous[first]),
                    new(second, 4_000, previous[second]),
                ]
            );
            Assert.Equal(cumulative - previous.Values.Sum(), result.Sum(x => x.Increment));
            foreach (var item in result)
                previous[item.StableId] = item.TargetCumulative;
        }

        Assert.Equal(6_000, previous[first]);
        Assert.Equal(4_000, previous[second]);
    }

    [Fact]
    public void NonEvenAllocation_IsDeterministicAndHasNoRoundingDrift()
    {
        var first = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var second = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var previous = new Dictionary<Guid, long> { [first] = 0, [second] = 0 };

        for (long cumulative = 1; cumulative <= 10; cumulative++)
        {
            var result = CumulativeAllocationCalculator.Calculate(
                10,
                cumulative,
                [new(first, 3, previous[first]), new(second, 7, previous[second])]
            );
            Assert.Equal(1, result.Sum(x => x.Increment));
            foreach (var item in result)
            {
                Assert.True(item.TargetCumulative >= previous[item.StableId]);
                previous[item.StableId] = item.TargetCumulative;
            }
        }

        Assert.Equal(3, previous[first]);
        Assert.Equal(7, previous[second]);
    }

    [Fact]
    public void StableIdentifierBreaksEqualRemainderTie()
    {
        var first = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var second = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var result = CumulativeAllocationCalculator.Calculate(
            2,
            1,
            [new(second, 1, 0), new(first, 1, 0)]
        );

        Assert.Equal(1, result.Single(x => x.StableId == first).Increment);
        Assert.Equal(0, result.Single(x => x.StableId == second).Increment);
    }

    [Fact]
    public void SmallIntegerCompositions_AreMonotonicExactAndFullyReversible()
    {
        var ids = Enumerable.Range(1, 4)
            .Select(x => new Guid(x, 0, 0, new byte[8]))
            .ToArray();
        for (var total = 1; total <= 30; total++)
        for (var first = 0; first <= total; first++)
        for (var second = 0; second <= total - first; second++)
        {
            var third = total - first - second;
            var originals = new long[] { first, second, third }
                .Select((amount, index) => (Id: ids[index], Amount: amount))
                .Where(x => x.Amount > 0)
                .ToArray();
            var previous = originals.ToDictionary(x => x.Id, _ => 0L);
            for (var cumulative = 0; cumulative <= total; cumulative++)
            {
                var result = CumulativeAllocationCalculator.Calculate(
                    total,
                    cumulative,
                    originals.Select(x => new CumulativeAllocationCalculator.Item(
                        x.Id, x.Amount, previous[x.Id])).ToList());
                Assert.Equal(cumulative, result.Sum(x => x.TargetCumulative));
                Assert.All(result, x => Assert.True(x.Increment >= 0));
                foreach (var item in result)
                    previous[item.StableId] = item.TargetCumulative;
            }
            foreach (var original in originals)
                Assert.Equal(original.Amount, previous[original.Id]);
        }
    }
}
