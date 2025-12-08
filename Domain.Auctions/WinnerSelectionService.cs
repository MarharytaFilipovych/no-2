namespace Domain.Auctions;

public class WinnerSelectionService
{
    public Task<Bid?> SelectWinner(Auction auction, List<Bid> activeBids, HashSet<Guid>? excludedUserIds = null)
    {
        if (activeBids.Count == 0)
            return Task.FromResult<Bid?>(null);

        var eligibleBids = activeBids
            .Where(b => b.Amount >= auction.MinPrice)
            .Where(b => excludedUserIds == null || !excludedUserIds.Contains(b.UserId))
            .ToList();

        if (eligibleBids.Count == 0)
            return Task.FromResult<Bid?>(null);

        var maxAmount = eligibleBids.Max(b => b.Amount);
        var topBids = eligibleBids
            .Where(b => b.Amount == maxAmount)
            .ToList();

        if (topBids.Count == 1)
            return Task.FromResult<Bid?>(topBids[0]);

        return Task.FromResult<Bid?>(ApplyTieBreaking(auction.TieBreakingPolicy, topBids));
    }

    private Bid ApplyTieBreaking(TieBreakingPolicy policy, List<Bid> tiedBids)
    {
        return policy switch
        {
            TieBreakingPolicy.Earliest => tiedBids.OrderBy(b => b.PlacedAt).First(),
            TieBreakingPolicy.RandomAmongEquals => SelectRandomBid(tiedBids),
            _ => throw new ArgumentException($"Unknown tie-breaking policy: {policy}")
        };
    }

    private Bid SelectRandomBid(List<Bid> bids)
    {
        var random = new Random();
        var index = random.Next(bids.Count);
        return bids[index];
    }
}