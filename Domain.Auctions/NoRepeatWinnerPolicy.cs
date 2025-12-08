namespace Domain.Auctions;

public class NoRepeatWinnerPolicy
{
    public HashSet<Guid> GetExcludedUsers(
        Auction currentAuction, 
        List<Auction> finalizedAuctionsInCategory)
    {
        if (string.IsNullOrEmpty(currentAuction.Category))
            return new HashSet<Guid>();

        var excludedUsers = new HashSet<Guid>();

        foreach (var auction in finalizedAuctionsInCategory)
        {
            if (auction.Id == currentAuction.Id)
                continue;

            if (auction.WinnerId.HasValue)
            {
                excludedUsers.Add(auction.WinnerId.Value);
            }

            if (auction.ProvisionalWinnerId.HasValue && !auction.IsPaymentConfirmed)
            {
                excludedUsers.Add(auction.ProvisionalWinnerId.Value);
            }
        }

        return excludedUsers;
    }
}