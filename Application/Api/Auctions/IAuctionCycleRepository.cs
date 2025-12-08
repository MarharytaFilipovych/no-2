using Domain.Auctions;

namespace Application.Api.Auctions;

public interface IAuctionCycleRepository
{
    Task<AuctionCycle?> GetActiveCycle();
    Task<AuctionCycle?> GetCycleForDate(DateTime date);
    Task<AuctionCycle> CreateCycle(AuctionCycle cycle);
    Task UpdateCycle(AuctionCycle cycle);
}