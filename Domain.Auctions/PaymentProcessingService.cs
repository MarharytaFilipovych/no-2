namespace Domain.Auctions;

public class PaymentProcessingService
{
    public PaymentProcessingResult ProcessPaymentDeadline(
        Auction auction,
        List<Bid> allBids,
        decimal provisionalWinnerBalance,
        HashSet<Guid> excludedUsers,
        Func<Guid, bool> isUserBanned)
    {
        if (!auction.HasProvisionalWinner())
        {
            throw new InvalidOperationException("Cannot process payment deadline: no provisional winner");
        }

        if (provisionalWinnerBalance >= auction.ProvisionalWinningAmount!.Value)
        {
            auction.ConfirmPayment();
            return PaymentProcessingResult.Confirmed(auction.WinnerId!.Value);
        }

        var rejectedUserId = auction.ProvisionalWinnerId!.Value;
        auction.RejectProvisionalWinner();

        var eligibleBids = GetEligibleBidsForPromotion(
            allBids,
            excludedUsers,
            rejectedUserId,
            isUserBanned,
            auction.MinPrice);

        return PaymentProcessingResult.Rejected(rejectedUserId, eligibleBids);
    }

    private List<Bid> GetEligibleBidsForPromotion(
        List<Bid> allBids,
        HashSet<Guid> excludedUsers,
        Guid rejectedUserId,
        Func<Guid, bool> isUserBanned,
        decimal minPrice)
    {
        var exclusionSet = new HashSet<Guid>(excludedUsers) { rejectedUserId };

        return allBids
            .Where(bid => !exclusionSet.Contains(bid.UserId))
            .Where(bid => !isUserBanned(bid.UserId))
            .Where(bid => bid.Amount >= minPrice)
            .ToList();
    }
}

public class PaymentProcessingResult
{
    public bool IsConfirmed { get; private init; }
    public Guid? ConfirmedWinnerId { get; private init; }
    public Guid? RejectedUserId { get; private init; }
    public List<Bid> EligibleBidsForPromotion { get; private init; } = new();

    public static PaymentProcessingResult Confirmed(Guid winnerId)
    {
        return new PaymentProcessingResult
        {
            IsConfirmed = true,
            ConfirmedWinnerId = winnerId
        };
    }

    public static PaymentProcessingResult Rejected(Guid rejectedUserId, List<Bid> eligibleBids)
    {
        return new PaymentProcessingResult
        {
            IsConfirmed = false,
            RejectedUserId = rejectedUserId,
            EligibleBidsForPromotion = eligibleBids
        };
    }
}