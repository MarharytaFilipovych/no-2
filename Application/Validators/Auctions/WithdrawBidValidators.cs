namespace Application.Validators.Auctions;

using Commands.Auctions;
using Domain.Auctions;

public interface IWithdrawBidValidator
{
    Task<WithdrawBidError?> Validate(Bid bid, Auction auction, Guid requestingUserId, DateTime currentTime);
}

public class BidOwnershipValidator : IWithdrawBidValidator
{
    public Task<WithdrawBidError?> Validate(Bid bid, Auction auction, Guid requestingUserId, DateTime currentTime)
    {
        return Task.FromResult<WithdrawBidError?>(
            bid.UserId != requestingUserId
                ? WithdrawBidError.NotBidOwner
                : null);
    }
}

public class BidNotAlreadyWithdrawnValidator : IWithdrawBidValidator
{
    public Task<WithdrawBidError?> Validate(Bid bid, Auction auction, Guid requestingUserId, DateTime currentTime)
    {
        return Task.FromResult<WithdrawBidError?>(
            bid.IsWithdrawn
                ? WithdrawBidError.AlreadyWithdrawn
                : null);
    }
}

public class AuctionActiveForWithdrawalValidator : IWithdrawBidValidator
{
    public Task<WithdrawBidError?> Validate(Bid bid, Auction auction, Guid requestingUserId, DateTime currentTime)
    {
        return Task.FromResult<WithdrawBidError?>(
            !auction.IsActive(currentTime)
                ? WithdrawBidError.AuctionNotActive
                : null);
    }
}