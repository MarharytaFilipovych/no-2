namespace Domain.Auctions;

public class Bid
{
    public required int BidId { get; init; }
    public required int AuctionId { get; init; }
    public required int UserId { get; init; }
    public decimal Amount { get; init; }
    public DateTime PlacedAt { get; init; }
    public bool IsWithdrawn { get; private set; }

    public void Withdraw()
    {
        if (IsWithdrawn)
            throw new InvalidOperationException("Bid is already withdrawn🐌!!!");
        
        IsWithdrawn = true;
    }
}