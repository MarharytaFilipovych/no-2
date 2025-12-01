namespace Domain.Auctions;

public class Bid
{
    public Guid Id { get; init; }
    public required Guid AuctionId { get; init; }
    public required Guid UserId { get; init; }
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