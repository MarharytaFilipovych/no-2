namespace Application.Queries.Auctions;

public class BidDetailsDTO
{
    public Guid Id { get; init; }
    public Guid AuctionId { get; init; }
    public Guid UserId { get; init; }
    public decimal? Amount { get; init; }
    public DateTime PlacedAt { get; init; }
    public bool IsWithdrawn { get; init; }
    public bool AmountHidden { get; init; }
}