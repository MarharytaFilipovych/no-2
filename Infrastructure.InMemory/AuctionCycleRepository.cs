using System.Collections.Concurrent;
using Application.Api.Auctions;
using Domain.Auctions;

namespace Infrastructure.InMemory;

public class AuctionCycleRepository : IAuctionCycleRepository
{
    private readonly ConcurrentDictionary<Guid, AuctionCycle> _cycles = new();

    public Task<AuctionCycle?> GetActiveCycle()
    {
        var now = DateTime.UtcNow;
        var activeCycle = _cycles.Values
            .FirstOrDefault(c => c.IsActive && c.IsDateInCycle(now));
        return Task.FromResult(activeCycle);
    }

    public Task<AuctionCycle?> GetCycleForDate(DateTime date)
    {
        var cycle = _cycles.Values
            .FirstOrDefault(c => c.IsDateInCycle(date));
        return Task.FromResult(cycle);
    }

    public Task<AuctionCycle> CreateCycle(AuctionCycle cycle)
    {
        _cycles[cycle.Id] = cycle;
        return Task.FromResult(cycle);
    }

    public Task UpdateCycle(AuctionCycle cycle)
    {
        _cycles[cycle.Id] = cycle;
        return Task.CompletedTask;
    }
}