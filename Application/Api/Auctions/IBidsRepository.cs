using Domain.Auctions;

namespace Application.Api.Auctions;

public interface IBidsRepository
{
    Task<Bid> CreateBid(Bid bid);
    Task<Bid?> GetBid(int bidId);
    Task<List<Bid>> GetBidsByAuction(int auctionId);
    Task<List<Bid>> GetActiveBidsByAuction(int auctionId);
    Task<Bid?> GetUserBidForAuction(int auctionId, int userId);
    Task<Bid?> GetHighestBidForAuction(int auctionId);
    Task UpdateBid(Bid bid);
}