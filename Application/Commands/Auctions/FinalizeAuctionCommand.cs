using Application.Api.Auctions;
using Application.Api.Utils;
using Application.Utils;
using Domain.Auctions;
using MediatR;

namespace Application.Commands.Auctions;

public class FinalizeAuctionCommand : IRequest<FinalizeAuctionCommand.Response>
{
    public Guid AuctionId { get; init; }

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
    AlreadyFinalized
}

public class FinalizeAuctionCommandHandler(
    IAuctionsRepository auctionsRepository,
    IBidsRepository bidsRepository,
    ITimeProvider timeProvider,
    WinnerSelectionService winnerSelectionService)
    : IRequestHandler<FinalizeAuctionCommand, FinalizeAuctionCommand.Response>
{
    public async Task<FinalizeAuctionCommand.Response> Handle(
        FinalizeAuctionCommand request,
        CancellationToken cancellationToken)
    {
        var auction = await auctionsRepository.GetAuction(request.AuctionId);
        if (auction == null)
            return ErrorResponse(FinalizeAuctionError.AuctionNotFound);

        var currentTime = timeProvider.Now();
        
        if (auction.State == AuctionState.Finalized)
            return ErrorResponse(FinalizeAuctionError.AlreadyFinalized);

        if (!auction.CanFinalize())
            return ErrorResponse(FinalizeAuctionError.AuctionNotEnded);

        var bids = await bidsRepository.GetActiveBidsByAuction(auction.Id);
        var winner = await winnerSelectionService.SelectWinner(auction, bids);

        auction.Finalize(winner?.UserId, winner?.Amount);
        await auctionsRepository.UpdateAuction(auction);

        return SuccessResponse(winner?.UserId, winner?.Amount);
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