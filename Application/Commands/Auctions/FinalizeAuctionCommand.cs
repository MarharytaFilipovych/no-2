using Application.Api.Auctions;
using Application.Api.Utils;
using Application.Configs;
using Application.Utils;
using Application.Validators.Auctions;
using Domain.Auctions;
using MediatR;

namespace Application.Commands.Auctions;

public class FinalizeAuctionCommand : IRequest<FinalizeAuctionCommand.Response>
{
    public Guid AuctionId { get; init; }
    public AuctionRole ActorRole { get; init; }

    public class Response
    {
        public OkOrError<FinalizeAuctionError> Result { get; init; }
        public Guid? WinnerId { get; init; }
        public decimal? WinningAmount { get; init; }
    }
}

public enum FinalizeAuctionError
{
    AuctionNotFound,
    AuctionNotEnded,
    AlreadyFinalized,
    InsufficientPermissions
}

public class FinalizeAuctionCommandHandler(
    IAuctionsRepository auctionsRepository,
    IBidsRepository bidsRepository,
    IAuctionCycleRepository cycleRepository,
    ITimeProvider timeProvider,
    IPaymentWindowConfig paymentWindowConfig,
    WinnerSelectionService winnerSelectionService,
    NoRepeatWinnerPolicy noRepeatWinnerPolicy,
    IEnumerable<IFinalizeAuctionValidator> validators)
    : IRequestHandler<FinalizeAuctionCommand, FinalizeAuctionCommand.Response>
{
    public async Task<FinalizeAuctionCommand.Response> Handle(FinalizeAuctionCommand request,
        CancellationToken cancellationToken)
    {
        if (!AuctionPermissions.CanFinalizeAuction(request.ActorRole))
            return ErrorResponse(FinalizeAuctionError.InsufficientPermissions);

        var auction = await auctionsRepository.GetAuction(request.AuctionId);
        if (auction == null)
            return ErrorResponse(FinalizeAuctionError.AuctionNotFound);

        var now = timeProvider.Now();
        
        if (auction.CanTransitionToEnded(now))
        {
            auction.TransitionToEnded();
            await auctionsRepository.UpdateAuction(auction);
        }

        var validationError = await ValidateFinalization(auction);
        if (validationError != null)
            return ErrorResponse(validationError.Value);

        var bids = await bidsRepository.GetActiveBidsByAuction(auction!.Id);
        var excludedUsers = await GetExcludedUsersForAuction(auction);
        var winner = await winnerSelectionService.SelectWinner(auction, bids, excludedUsers);

        if (winner != null)
        {
            var deadline = now.Add(paymentWindowConfig.PaymentDeadline);
            auction.FinalizeWithProvisionalWinner(winner.UserId, winner.Amount, deadline);
        }
        else auction.FinalizeWithNoWinner();

        await auctionsRepository.UpdateAuction(auction);

        return SuccessResponse(winner?.UserId, winner?.Amount);
    }

    private async Task<FinalizeAuctionError?> ValidateFinalization(Auction? auction)
    {
        foreach (var validator in validators)
        {
            var error = await validator.Validate(auction);
            if (error.HasValue) return error;
        }

        return null;
    }

    private async Task<HashSet<Guid>?> GetExcludedUsersForAuction(Auction auction)
    {
        if (string.IsNullOrEmpty(auction.Category)) return null;

        var cycle = await cycleRepository.GetActiveCycle();
        if (cycle == null) return null;

        var finalizedInCategory = await auctionsRepository
            .GetFinalizedAuctionsByCategoryAndPeriod(auction.Category, cycle.StartDate, cycle.EndDate);

        return noRepeatWinnerPolicy.GetExcludedUsers(auction, finalizedInCategory);
    }

    private static FinalizeAuctionCommand.Response ErrorResponse(FinalizeAuctionError error) =>
        new() { Result = OkOrError<FinalizeAuctionError>.Error(error) };

    private static FinalizeAuctionCommand.Response SuccessResponse(Guid? winnerId, decimal? winningAmount) =>
        new()
        {
            Result = OkOrError<FinalizeAuctionError>.Ok(),
            WinnerId = winnerId,
            WinningAmount = winningAmount
        };
}