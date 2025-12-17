using Domain.Auctions;

namespace Application.Validators.Auctions;

using Commands.Auctions;

public interface IConfirmPaymentValidator
{
    Task<ConfirmPaymentError?> Validate(Auction? auction, decimal balance, DateTime currentTime);
}

public class AuctionExistsForPaymentValidator : IConfirmPaymentValidator
{
    public Task<ConfirmPaymentError?> Validate(Auction? auction, decimal balance, DateTime currentTime)
    {
        return Task.FromResult<ConfirmPaymentError?>(
            auction == null ? ConfirmPaymentError.AuctionNotFound : null);
    }
}

public class HasProvisionalWinnerValidator : IConfirmPaymentValidator
{
    public Task<ConfirmPaymentError?> Validate(Auction? auction, decimal balance, DateTime currentTime)
    {
        if (auction == null) return Task.FromResult<ConfirmPaymentError?>(null);
        
        return Task.FromResult<ConfirmPaymentError?>(!auction.HasProvisionalWinner()
            ? ConfirmPaymentError.NoProvisionalWinner
            : null);
    }
}

public class PaymentDeadlinePassedValidator : IConfirmPaymentValidator
{
    public Task<ConfirmPaymentError?> Validate(Auction? auction, decimal balance, DateTime currentTime)
    {
        if (auction == null) return Task.FromResult<ConfirmPaymentError?>(null);
        
        return Task.FromResult<ConfirmPaymentError?>(
            auction.IsPaymentDeadlinePassed(currentTime)
                ? ConfirmPaymentError.DeadlineAlreadyPassed
                : null);
    }
}

public class SufficientBalanceValidator : IConfirmPaymentValidator
{
    public Task<ConfirmPaymentError?> Validate(Auction? auction, decimal balance, DateTime currentTime)
    {
        if (auction == null) return Task.FromResult<ConfirmPaymentError?>(null);
        
        return Task.FromResult<ConfirmPaymentError?>(
            balance < auction.ProvisionalWinningAmount!.Value
                ? ConfirmPaymentError.InsufficientBalance
                : null);
    }
}