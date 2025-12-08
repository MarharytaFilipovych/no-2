namespace Infrastructure.InMemory;

using System.Collections.Concurrent;
using Application.Api.Auctions;
using Domain.Auctions;

public class AuctionsRepository : IAuctionsRepository
{
    private readonly ConcurrentDictionary<Guid, Auction> _auctions = new();

    public Task<Auction?> GetAuction(Guid auctionId)
    {
        _auctions.TryGetValue(auctionId, out var auction);
        return Task.FromResult(auction);
    }

    public Task<Auction> CreateAuction(Auction auction)
    {
        if (auction.Id == Guid.Empty)
            auction.Id = Guid.NewGuid();
    
        _auctions[auction.Id] = auction;
        return Task.FromResult(auction);
    }

    public Task UpdateAuction(Auction auction)
    {
        _auctions[auction.Id] = auction;
        return Task.CompletedTask;
    }

    public Task<List<Auction>> GetAuctionsByState(AuctionState state) =>
        Task.FromResult(_auctions.Values.Where(a => a.State == state).ToList());
    
    public Task<List<Auction>> GetFinalizedAuctionsByCategoryAndPeriod(
        string category, 
        DateTime startDate, 
        DateTime endDate)
    {
        var result = _auctions.Values
            .Where(a => a.State == AuctionState.Finalized)
            .Where(a => a.Category == category)
            .Where(a => a.EndTime >= startDate && a.EndTime <= endDate)
            .ToList();
        
        return Task.FromResult(result);
    }
}