using Domain.Auctions;

namespace Application.Api.Auctions;

public interface IBidsRepository
{
    Task<Bid> CreateBid(Bid bid);
    Task<Bid?> GetBid(Guid bidId);
    Task<List<Bid>> GetBidsByAuction(Guid auctionId);
    Task<List<Bid>> GetActiveBidsByAuction(Guid auctionId);
    Task<Bid?> GetUserBidForAuction(Guid auctionId, Guid userId);
    Task<Bid?> GetHighestBidForAuction(Guid auctionId);
    Task UpdateBid(Bid bid);
}