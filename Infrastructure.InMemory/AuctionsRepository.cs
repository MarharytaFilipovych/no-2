namespace Infrastructure.InMemory;

using System.Collections.Concurrent;
using Application.Api.Auctions;
using Domain.Auctions;

public class AuctionsRepository : IAuctionsRepository
{
    private readonly ConcurrentDictionary<int, Auction> _auctions = new();
    private int _nextId = 1;

    public Task<Auction?> GetAuction(int auctionId)
    {
        _auctions.TryGetValue(auctionId, out var auction);
        return Task.FromResult(auction);
    }

    public Task<Auction> CreateAuction(Auction auction)
    {
        var id = Interlocked.Increment(ref _nextId) - 1;
        var auctionWithId = new Auction
        {
            AuctionId = id,
            Title = auction.Title,
            Description = auction.Description,
            StartTime = auction.StartTime,
            EndTime = auction.EndTime,
            Type = auction.Type,
            MinimumIncrement = auction.MinimumIncrement,
            MinPrice = auction.MinPrice,
            SoftCloseWindow = auction.SoftCloseWindow,
            ShowMinPrice = auction.ShowMinPrice,
            TieBreakingPolicy = auction.TieBreakingPolicy
        };

        _auctions[id] = auctionWithId;
        return Task.FromResult(auctionWithId);
    }

    public Task UpdateAuction(Auction auction)
    {
        _auctions[auction.AuctionId] = auction;
        return Task.CompletedTask;
    }

    public Task<List<Auction>> GetAuctionsByState(AuctionState state) =>
        Task.FromResult(_auctions.Values.Where(a => a.State == state).ToList());
}