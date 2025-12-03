using Application.Api.Auctions;
using Application.Api.Utils;
using Application.Validators.Auctions;
using Domain.Auctions;

namespace Application.Commands.Auctions;

using MediatR;
using Utils;

public class PlaceBidCommand : IRequest<PlaceBidCommand.Response>
{
    public Guid AuctionId { get; init; }
    public Guid UserId { get; init; }
    public decimal Amount { get; init; }

    public class Response
    {
        public OkOrError<PlaceBidError> Result { get; init; }
        public Guid BidId { get; init; }
    }
}

public enum PlaceBidError
{
    AuctionNotFound,
    AuctionNotActive,
    BidTooLow,
    UserAlreadyBid,
    ExceedsMaxBidAmount,
    ExceedsBalanceLimit
}

public class PlaceBidCommandHandler(IAuctionsRepository auctionsRepository,
    IBidsRepository bidsRepository, ITimeProvider timeProvider,
    IEnumerable<IBidValidator> validators)
    : IRequestHandler<PlaceBidCommand, PlaceBidCommand.Response>
{
    public async Task<PlaceBidCommand.Response> Handle(PlaceBidCommand request, CancellationToken cancellationToken)
    {
        var auction = await auctionsRepository.GetAuction(request.AuctionId);
        if (auction == null) return ErrorResponse(PlaceBidError.AuctionNotFound);

        var currentTime = timeProvider.Now();
        if (!auction.IsActive(currentTime)) return ErrorResponse(PlaceBidError.AuctionNotActive);
        
        if (auction.Type == AuctionType.Blind)
            await RemovePreviousBidIfExists(request.AuctionId, request.UserId);
        
        var validationError = await ValidateBid(auction, request.Amount, request.UserId);
        if (validationError != null) return ErrorResponse(validationError.Value);

        var bid = CreateBid(request, currentTime);

        var createdBid = await bidsRepository.CreateBid(bid);
        await ApplySoftCloseIfNeeded(auction, currentTime);
        return SuccessResponse(createdBid.Id);
    }

    private async Task<PlaceBidError?> ValidateBid(Auction auction, decimal amount, Guid userId)
    {
        foreach (var validator in validators)
        {
            var error = await validator.Validate(auction, amount, userId);
            if (error.HasValue) return error;
        }

        return null;
    }

    private static Bid CreateBid(PlaceBidCommand request, DateTime currentTime) => new()
    {
        AuctionId = request.AuctionId,
        UserId = request.UserId,
        Amount = request.Amount,
        PlacedAt = currentTime
    };

    private async Task RemovePreviousBidIfExists(Guid auctionId, Guid userId)
    {
        var existingBid = await bidsRepository.GetUserBidForAuction(auctionId, userId);
        if (existingBid is { IsWithdrawn: false })
        {
            existingBid.Withdraw();
            await bidsRepository.UpdateBid(existingBid);
        }
    }

    private async Task ApplySoftCloseIfNeeded(Auction auction, DateTime currentTime)
    {
        if (!auction.SoftCloseWindow.HasValue) return;

        var timeUntilEnd = auction.EndTime - currentTime;
        if (timeUntilEnd <= auction.SoftCloseWindow.Value)
        {
            auction.ExtendEndTime(auction.SoftCloseWindow.Value);
            await auctionsRepository.UpdateAuction(auction);
        }
    }

    private static PlaceBidCommand.Response ErrorResponse(PlaceBidError error) =>
        new() { Result = OkOrError<PlaceBidError>.Error(error) };

    private static PlaceBidCommand.Response SuccessResponse(Guid bidId) =>
        new() { Result = OkOrError<PlaceBidError>.Ok(), BidId = bidId };
}