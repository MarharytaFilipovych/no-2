namespace Application.Validators.Auctions;

using Commands.Auctions;
using Api.Utils;

public interface IProcessPaymentDeadlineValidator
{
    Task<ProcessPaymentError?> Validate(
        Domain.Auctions.Auction? auction,
        DateTime currentTime);
}

public class AuctionExistsForDeadlineValidator : IProcessPaymentDeadlineValidator
{
    public Task<ProcessPaymentError?> Validate(Domain.Auctions.Auction? auction, DateTime currentTime)
    {
        return Task.FromResult<ProcessPaymentError?>(
            auction == null ? ProcessPaymentError.AuctionNotFound : null);
    }
}

public class HasProvisionalWinnerForDeadlineValidator : IProcessPaymentDeadlineValidator
{
    public Task<ProcessPaymentError?> Validate(Domain.Auctions.Auction? auction, DateTime currentTime)
    {
        if (auction == null) return Task.FromResult<ProcessPaymentError?>(null);

        return Task.FromResult<ProcessPaymentError?>(
            !auction.HasProvisionalWinner()
                ? ProcessPaymentError.NoProvisionalWinner
                : null);
    }
}

public class DeadlineHasPassedValidator : IProcessPaymentDeadlineValidator
{
    public Task<ProcessPaymentError?> Validate(Domain.Auctions.Auction? auction, DateTime currentTime)
    {
        if (auction == null) return Task.FromResult<ProcessPaymentError?>(null);

        return Task.FromResult<ProcessPaymentError?>(
            !auction.IsPaymentDeadlinePassed(currentTime)
                ? ProcessPaymentError.DeadlineNotPassed
                : null);
    }
}