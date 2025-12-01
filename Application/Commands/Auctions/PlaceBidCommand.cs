namespace Application.Commands.Auctions;

using Api.Auctions;
using Domain.Auctions;
using MediatR;
using Utils;

public class PlaceBidCommand : IRequest<PlaceBidCommand.Response>
{
    public int AuctionId { get; init; }
    public int UserId { get; init; }
    public decimal Amount { get; init; }

    public class Response
    {
        public OkOrError<PlaceBidError> Result { get; init; }
        public int BidId { get; init; }
    }
}

public enum PlaceBidError
{
    AuctionNotFound,
    AuctionNotActive,
    BidTooLow,
    UserAlreadyBid
}

public class PlaceBidCommandHandler(IAuctionsRepository auctionsRepository,
    IBidsRepository bidsRepository, ITimeProvider timeProvider)
    : IRequestHandler<PlaceBidCommand, PlaceBidCommand.Response>
{
    public async Task<PlaceBidCommand.Response> Handle(
        PlaceBidCommand request, CancellationToken cancellationToken)
    {
        var auction = await auctionsRepository.GetAuction(request.AuctionId);
        if (auction == null)
            return ErrorResponse(PlaceBidError.AuctionNotFound);

        var currentTime = timeProvider.Now();
        if (!auction.IsActive(currentTime)) 
            return ErrorResponse(PlaceBidError.AuctionNotActive);

        var validationResult = await ValidateBid(auction, request);
        if (validationResult != null)
            return ErrorResponse(validationResult.Value);

        var bid = new Bid
        {
            AuctionId = request.AuctionId,
            UserId = request.UserId,
            Amount = request.Amount,
            PlacedAt = currentTime
        };

        if (auction.Type == AuctionType.Blind) 
            await RemovePreviousBidIfExists(request.AuctionId, request.UserId);

        var createdBid = await bidsRepository.CreateBid(bid);

        await ApplySoftCloseIfNeeded(auction, currentTime);

        return new PlaceBidCommand.Response
        {
            Result = OkOrError<PlaceBidError>.Ok(),
            BidId = createdBid.BidId
        };
    }

    private async Task<PlaceBidError?> ValidateBid(Auction auction, PlaceBidCommand request)
    {
        if (auction.Type == AuctionType.Open)
        {
            var highestBid = await bidsRepository.GetHighestBidForAuction(request.AuctionId);
            var minimumRequired = (highestBid?.Amount ?? auction.MinPrice) + (auction.MinimumIncrement ?? 0);

            if (request.Amount < minimumRequired)
                return PlaceBidError.BidTooLow;
        }
        else
        {
            var existingBid = await bidsRepository.GetUserBidForAuction(request.AuctionId, request.UserId);
            if (existingBid is { IsWithdrawn: false }) 
                return PlaceBidError.UserAlreadyBid;
        }

        return null;
    }

    private async Task RemovePreviousBidIfExists(int auctionId, int userId)
    {
        var existingBid = await bidsRepository.GetUserBidForAuction(auctionId, userId);
        if (existingBid != null && !existingBid.IsWithdrawn)
        {
            existingBid.Withdraw();
            await bidsRepository.UpdateBid(existingBid);
        }
    }

    private async Task ApplySoftCloseIfNeeded(Auction auction, DateTime currentTime)
    {
        if (auction.SoftCloseWindow.HasValue)
        {
            var timeUntilEnd = auction.EndTime - currentTime;
            if (timeUntilEnd <= auction.SoftCloseWindow.Value)
            {
                auction.ExtendEndTime(auction.SoftCloseWindow.Value);
                await auctionsRepository.UpdateAuction(auction);
            }
        }
    }

    private PlaceBidCommand.Response ErrorResponse(PlaceBidError error) =>
        new() { Result = OkOrError<PlaceBidError>.Error(error) };
}

