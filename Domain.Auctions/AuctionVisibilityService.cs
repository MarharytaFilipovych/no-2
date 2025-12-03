namespace Domain.Auctions;

public class AuctionVisibilityService
{
    public bool AreBidAmountsVisible(Auction auction)
    {
        return auction.Type switch
        {
            AuctionType.Open => auction.State == AuctionState.Active || 
                               auction.State == AuctionState.Ended || 
                               auction.State == AuctionState.Finalized,
            AuctionType.Blind => auction.State == AuctionState.Ended || 
                                auction.State == AuctionState.Finalized,
            _ => false
        };
    }

    public bool ShouldShowHighestBid(Auction auction)
    {
        return auction.Type == AuctionType.Open && 
               (auction.State == AuctionState.Active || 
                auction.State == AuctionState.Ended || 
                auction.State == AuctionState.Finalized);
    }

    public bool ShouldShowMinPrice(Auction auction)
    {
        if (auction.Type == AuctionType.Open)
            return true;
        
        return auction.ShowMinPrice;
    }

    public bool CanViewBidDetails(Auction auction, Guid? requestingUserId, Guid bidOwnerId)
    {
        if (requestingUserId.HasValue && requestingUserId.Value == bidOwnerId)
            return true;

        return AreBidAmountsVisible(auction);
    }

    public bool ShouldShowBidCount(Auction auction)
    {
        return auction.State != AuctionState.Pending;
    }
}