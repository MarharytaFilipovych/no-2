namespace Infrastructure.InMemory;

using System.Collections.Concurrent;
using Application.Api.Auctions;
using Domain.Auctions;

public class BidsRepository : IBidsRepository
{
    private readonly ConcurrentDictionary<Guid, Bid> _bids = new();

    public Task<Bid> CreateBid(Bid bid)
    {
        if (bid.Id == Guid.Empty)
            bid.Id = Guid.NewGuid();
    
        _bids[bid.Id] = bid;
        return Task.FromResult(bid);
    }

    public Task<Bid?> GetBid(Guid bidId)
    {
        _bids.TryGetValue(bidId, out var bid);
        return Task.FromResult(bid);
    }

    public Task<List<Bid>> GetBidsByAuction(Guid auctionId) =>
        Task.FromResult(_bids.Values.Where(b => b.AuctionId == auctionId).ToList());

    public Task<List<Bid>> GetActiveBidsByAuction(Guid auctionId) => 
        Task.FromResult(_bids.Values
            .Where(b => b.AuctionId == auctionId && !b.IsWithdrawn)
            .ToList());

    public Task<Bid?> GetUserBidForAuction(Guid auctionId, Guid userId) =>
        Task.FromResult(_bids.Values
            .Where(b => b.AuctionId == auctionId && b.UserId == userId)
            .MaxBy(b => b.PlacedAt));

    public Task<Bid?> GetHighestBidForAuction(Guid auctionId) =>
        Task.FromResult(_bids.Values
            .Where(b => b.AuctionId == auctionId && !b.IsWithdrawn)
            .OrderByDescending(b => b.Amount)
            .ThenBy(b => b.PlacedAt)
            .FirstOrDefault());

    public Task UpdateBid(Bid bid)
    {
        _bids[bid.Id] = bid;
        return Task.CompletedTask;
    }
}