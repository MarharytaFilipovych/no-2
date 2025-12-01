using Domain.Auctions;

namespace WebApi.Controllers.Auctions.Contracts;

public class CreateAuctionRequest
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public DateTime? StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public AuctionType Type { get; init; }
    public decimal? MinimumIncrement { get; init; }
    public decimal MinPrice { get; init; }
    public TimeSpan? SoftCloseWindow { get; init; }
    public bool ShowMinPrice { get; init; }
    public TieBreakingPolicy TieBreakingPolicy { get; init; }
}