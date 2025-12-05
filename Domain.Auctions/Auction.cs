namespace Domain.Auctions;

public class Auction
{
    public Guid Id { get; set; } = Guid.Empty;
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
    public Guid? ProvisionalWinnerId { get; private set; }
    public decimal? ProvisionalWinningAmount { get; private set; }
    public DateTime? PaymentDeadline { get; private set; }
    public bool IsPaymentConfirmed { get; private set; }

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

    public void SetProvisionalWinner(Guid userId, decimal amount, DateTime deadline)
    {
        if (State != AuctionState.Finalized)
            throw new InvalidOperationException("Can only set provisional winner on finalized auction!");

        ProvisionalWinnerId = userId;
        ProvisionalWinningAmount = amount;
        PaymentDeadline = deadline;
        IsPaymentConfirmed = false;
    }

    public void ConfirmPayment()
    {
        if (!ProvisionalWinnerId.HasValue)
            throw new InvalidOperationException("No provisional winner to confirm!");

        WinnerId = ProvisionalWinnerId;
        WinningBidAmount = ProvisionalWinningAmount;
        IsPaymentConfirmed = true;
        PaymentDeadline = null;
    }

    public void RejectProvisionalWinner()
    {
        if (!ProvisionalWinnerId.HasValue)
            throw new InvalidOperationException("No provisional winner to reject!");

        ProvisionalWinnerId = null;
        ProvisionalWinningAmount = null;
        PaymentDeadline = null;
        IsPaymentConfirmed = false;
    }

    public bool IsPaymentDeadlinePassed(DateTime currentTime)
    {
        return PaymentDeadline.HasValue && currentTime > PaymentDeadline.Value;
    }

    public bool HasProvisionalWinner()
    {
        return ProvisionalWinnerId.HasValue && !IsPaymentConfirmed;
    }
}