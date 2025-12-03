namespace Application.Queries.Auctions;

using Domain.Auctions;

public class AuctionListItemDTO
{
    public Guid Id { get; init; }
    public required string Title { get; init; }
    public AuctionType Type { get; init; }
    public AuctionState State { get; init; }
    public DateTime EndTime { get; init; }
    public decimal? CurrentHighestBid { get; init; }
    public int? BidCount { get; init; }
    public decimal? MinPrice { get; init; }
}