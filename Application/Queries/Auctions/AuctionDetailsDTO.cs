namespace Application.Queries.Auctions;

using Domain.Auctions;

public class AuctionDetailsDTO
{
    public Guid Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public DateTime? StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public AuctionType Type { get; init; }
    public AuctionState State { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MinimumIncrement { get; init; }
    public decimal? CurrentHighestBid { get; init; }
    public int? BidCount { get; init; }
    public TimeSpan? SoftCloseWindow { get; init; }
    public TieBreakingPolicy TieBreakingPolicy { get; init; }
    public Guid? WinnerId { get; init; }
    public decimal? WinningBidAmount { get; init; }
}