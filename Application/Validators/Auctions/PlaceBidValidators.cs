namespace Application.Validators.Auctions;

using Api.Auctions;
using Commands.Auctions;
using Configs;
using Domain.Auctions;

public interface IBidValidator
{
    Task<PlaceBidError?> Validate(Auction auction, decimal bidAmount, Guid userId);
}

public class MaxBidAmountValidator(IBiddingConfig config) : IBidValidator
{
    public Task<PlaceBidError?> Validate(Auction auction, decimal bidAmount, Guid userId)
    {
        var rules = new BiddingRules(config.MaxBidAmount, config.BalanceRatioLimit);
        return Task.FromResult<PlaceBidError?>(
            !rules.IsWithinMaxLimit(bidAmount)
                ? PlaceBidError.ExceedsMaxBidAmount
                : null);
    }
}

public class BalanceRatioValidator(IBiddingConfig config, IParticipantBalanceRepository balanceRepo)
    : IBidValidator
{
    public async Task<PlaceBidError?> Validate(Auction auction, decimal bidAmount, Guid userId)
    {
        var rules = new BiddingRules(config.MaxBidAmount, config.BalanceRatioLimit);
        var balance = await balanceRepo.GetBalance(userId);

        return !rules.IsWithinBalanceLimit(bidAmount, balance)
            ? PlaceBidError.ExceedsBalanceLimit
            : null;
    }
}

public class OpenAuctionIncrementValidator(IBidsRepository bidsRepo) : IBidValidator
{
    public async Task<PlaceBidError?> Validate(Auction auction, decimal bidAmount, Guid userId)
    {
        if (auction.Type != AuctionType.Open)
            return null;

        var highestBid = await bidsRepo.GetHighestBidForAuction(auction.Id);
        var minimumRequired = (highestBid?.Amount ?? auction.MinPrice) + (auction.MinimumIncrement ?? 0);

        return bidAmount < minimumRequired ? PlaceBidError.BidTooLow : null;
    }
}

public class BlindAuctionSingleBidValidator(IBidsRepository bidsRepo) : IBidValidator
{
    public async Task<PlaceBidError?> Validate(Auction auction, decimal bidAmount, Guid userId)
    {
        if (auction.Type != AuctionType.Blind)
            return null;

        var existingBid = await bidsRepo.GetUserBidForAuction(auction.Id, userId);
        return existingBid is { IsWithdrawn: false }
            ? PlaceBidError.UserAlreadyBid
            : null;
    }
}