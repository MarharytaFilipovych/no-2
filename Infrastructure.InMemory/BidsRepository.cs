namespace Infrastructure.InMemory;

using System.Collections.Concurrent;
using Application.Api.Auctions;
using Domain.Auctions;

public class BidsRepository : IBidsRepository
{
    private readonly ConcurrentDictionary<int, Bid> _bids = new();
    private int _nextId = 1;

    public Task<Bid> CreateBid(Bid bid)
    {
        var id = Interlocked.Increment(ref _nextId) - 1;
        var bidWithId = new Bid
        {
            BidId = id,
            AuctionId = bid.AuctionId,
            UserId = bid.UserId,
            Amount = bid.Amount,
            PlacedAt = bid.PlacedAt
        };

        _bids[id] = bidWithId;
        return Task.FromResult(bidWithId);
    }

    public Task<Bid?> GetBid(int bidId)
    {
        _bids.TryGetValue(bidId, out var bid);
        return Task.FromResult(bid);
    }

    public Task<List<Bid>> GetBidsByAuction(int auctionId) =>
        Task.FromResult(_bids.Values.Where(b => b.AuctionId == auctionId).ToList());

    public Task<List<Bid>> GetActiveBidsByAuction(int auctionId) => 
        Task.FromResult(_bids.Values
            .Where(b => b.AuctionId == auctionId && !b.IsWithdrawn)
            .ToList());

    public Task<Bid?> GetUserBidForAuction(int auctionId, int userId) =>
        Task.FromResult(_bids.Values
            .Where(b => b.AuctionId == auctionId && b.UserId == userId)
            .MaxBy(b => b.PlacedAt));

    public Task<Bid?> GetHighestBidForAuction(int auctionId) =>
        Task.FromResult(_bids.Values
            .Where(b => b.AuctionId == auctionId && !b.IsWithdrawn)
            .OrderByDescending(b => b.Amount)
            .ThenBy(b => b.PlacedAt)
            .FirstOrDefault());

    public Task UpdateBid(Bid bid)
    {
        _bids[bid.BidId] = bid;
        return Task.CompletedTask;
    }
}