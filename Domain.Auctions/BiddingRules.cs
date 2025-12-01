namespace Domain.Auctions;

public class BiddingRules
{
    private decimal MaxBidAmount { get; }
    private decimal BalanceRatioLimit { get; }

    public BiddingRules(decimal maxBidAmount, decimal balanceRatioLimit)
    {
        if (maxBidAmount <= 0)
            throw new ArgumentException("Max bid amount must be positive!", nameof(maxBidAmount));
        
        if (balanceRatioLimit <= 0)
            throw new ArgumentException("Balance ratio limit must be positive!", nameof(balanceRatioLimit));

        MaxBidAmount = maxBidAmount;
        BalanceRatioLimit = balanceRatioLimit;
    }

    public bool IsWithinMaxLimit(decimal bidAmount) => bidAmount <= MaxBidAmount;

    public bool IsWithinBalanceLimit(decimal bidAmount, decimal accountBalance) => 
        bidAmount <= accountBalance * BalanceRatioLimit;

    public decimal CalculateMaxAllowedBid(decimal accountBalance)
    {
        var balanceLimit = accountBalance * BalanceRatioLimit;
        return Math.Min(balanceLimit, MaxBidAmount);
    }
}