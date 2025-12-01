namespace Domain.Auctions;

public class Auction
{
    public Guid Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public DateTime? StartTime { get; init; }
    public DateTime EndTime { get; set; }
    public AuctionType Type { get; init; } = AuctionType.Open;
    public decimal? MinimumIncrement { get; init; }
    public decimal MinPrice { get; init; }
    public TimeSpan? SoftCloseWindow { get; init; }
    public bool ShowMinPrice { get; init; }
    public TieBreakingPolicy TieBreakingPolicy { get; init; } = TieBreakingPolicy.Earliest;
    public AuctionState State { get; private set; } = AuctionState.Pending;
    public Guid? WinnerId { get; private set; }
    public decimal? WinningBidAmount { get; private set; }

    public bool IsActive(DateTime currentTime) => 
        State == AuctionState.Active && currentTime < EndTime;

    public bool CanTransitionToActive(DateTime currentTime)
    {
        if (State != AuctionState.Pending) return false;
        if (StartTime == null) return true;
        return currentTime >= StartTime.Value;
    }

    public void TransitionToActive()
    {
        if (State != AuctionState.Pending)
            throw new InvalidOperationException("Can only transition to Active from Pending state!");
        
        State = AuctionState.Active;
    }

    public bool CanTransitionToEnded(DateTime currentTime) =>
        State == AuctionState.Active && currentTime >= EndTime;

    public void TransitionToEnded()
    {
        if (State != AuctionState.Active)
            throw new InvalidOperationException("Can only transition to Ended from Active state!");
        
        State = AuctionState.Ended;
    }

    public bool CanFinalize() => State == AuctionState.Ended;

    public void Finalize(Guid? winnerId, decimal? winningAmount)
    {
        if (!CanFinalize())
            throw new InvalidOperationException("Can only finalize after auction has ended!");
        
        WinnerId = winnerId;
        WinningBidAmount = winningAmount;
        State = AuctionState.Finalized;
    }

    public void ExtendEndTime(TimeSpan extension)
    {
        if (State != AuctionState.Active)
            throw new InvalidOperationException("Can only extend active auctions!");
        
        EndTime = EndTime.Add(extension);
    }
}