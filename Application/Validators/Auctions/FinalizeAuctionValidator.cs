namespace Application.Validators.Auctions;

using Commands.Auctions;

public interface IFinalizeAuctionValidator
{
    Task<FinalizeAuctionError?> Validate(Domain.Auctions.Auction? auction);
}

public class AuctionExistsForFinalizationValidator : IFinalizeAuctionValidator
{
    public Task<FinalizeAuctionError?> Validate(Domain.Auctions.Auction? auction)
    {
        return Task.FromResult<FinalizeAuctionError?>(
            auction == null ? FinalizeAuctionError.AuctionNotFound : null);
    }
}

public class AuctionNotAlreadyFinalizedValidator : IFinalizeAuctionValidator
{
    public Task<FinalizeAuctionError?> Validate(Domain.Auctions.Auction? auction)
    {
        if (auction == null) return Task.FromResult<FinalizeAuctionError?>(null);
        
        return Task.FromResult<FinalizeAuctionError?>(
            auction.State == Domain.Auctions.AuctionState.Finalized 
                ? FinalizeAuctionError.AlreadyFinalized 
                : null);
    }
}

public class AuctionCanBeFinalizedValidator : IFinalizeAuctionValidator
{
    public Task<FinalizeAuctionError?> Validate(Domain.Auctions.Auction? auction)
    {
        if (auction == null) return Task.FromResult<FinalizeAuctionError?>(null);
        
        return Task.FromResult<FinalizeAuctionError?>(
            !auction.CanFinalize() 
                ? FinalizeAuctionError.AuctionNotEnded 
                : null);
    }
}