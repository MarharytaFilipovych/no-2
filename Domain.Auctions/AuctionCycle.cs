namespace Domain.Auctions;

public class AuctionCycle
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string  Name { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public bool IsActive { get; init; }

    public bool IsDateInCycle(DateTime date)
    {
        return date >= StartDate && date <= EndDate;
    }
}