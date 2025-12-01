namespace Application.Api.Auctions;

using Domain.Auctions;

public interface IAuctionsRepository
{
    Task<Auction?> GetAuction(Guid auctionId);
    Task<Auction> CreateAuction(Auction auction);
    Task UpdateAuction(Auction auction);
    Task<List<Auction>> GetAuctionsByState(AuctionState state);
}