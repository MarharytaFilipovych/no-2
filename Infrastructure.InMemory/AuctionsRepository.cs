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
        var auctionWithId = new Auction
        {
            Id = Guid.NewGuid(),
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

        _auctions[auctionWithId.Id] = auctionWithId;
        return Task.FromResult(auctionWithId);
    }

    public Task UpdateAuction(Auction auction)
    {
        _auctions[auction.Id] = auction;
        return Task.CompletedTask;
    }

    public Task<List<Auction>> GetAuctionsByState(AuctionState state) =>
        Task.FromResult(_auctions.Values.Where(a => a.State == state).ToList());
}