namespace Application.Queries.Auctions;

using Api.Auctions;
using Domain.Auctions;
using MediatR;

public class GetAuctionDetailsQuery : IRequest<GetAuctionDetailsQuery.Response>
{
    public Guid AuctionId { get; init; }
    public Guid? RequestingUserId { get; init; }

    public class Response
    {
        public AuctionDetailsDTO? Auction { get; init; }
        public bool NotFound { get; init; }
    }
}

public class GetAuctionDetailsQueryHandler(
    IAuctionsRepository auctionsRepository,
    IBidsRepository bidsRepository,
    AuctionVisibilityService visibilityService)
    : IRequestHandler<GetAuctionDetailsQuery, GetAuctionDetailsQuery.Response>
{
    public async Task<GetAuctionDetailsQuery.Response> Handle(
        GetAuctionDetailsQuery request,
        CancellationToken cancellationToken)
    {
        var auction = await auctionsRepository.GetAuction(request.AuctionId);
        if (auction == null)
            return new GetAuctionDetailsQuery.Response { NotFound = true };

        var dto = await BuildAuctionDetailsDto(auction);
        return new GetAuctionDetailsQuery.Response { Auction = dto };
    }

    private async Task<AuctionDetailsDTO> BuildAuctionDetailsDto(Auction auction)
    {
        var bids = await bidsRepository.GetActiveBidsByAuction(auction.Id);
        var highestBid = await bidsRepository.GetHighestBidForAuction(auction.Id);
        var showHighestBid = visibilityService.ShouldShowHighestBid(auction);
        var showMinPrice = visibilityService.ShouldShowMinPrice(auction);
        var showBidCount = visibilityService.ShouldShowBidCount(auction);

        return new AuctionDetailsDTO
        {
            Id = auction.Id,
            Title = auction.Title,
            Description = auction.Description,
            StartTime = auction.StartTime,
            EndTime = auction.EndTime,
            Type = auction.Type,
            State = auction.State,
            MinPrice = showMinPrice ? auction.MinPrice : null,
            MinimumIncrement = auction.Type == AuctionType.Open ? auction.MinimumIncrement : null,
            CurrentHighestBid = showHighestBid ? highestBid?.Amount : null,
            BidCount = showBidCount ? bids.Count : null,
            SoftCloseWindow = auction.SoftCloseWindow,
            TieBreakingPolicy = auction.TieBreakingPolicy,
            WinnerId = auction.State == AuctionState.Finalized ? auction.WinnerId : null,
            WinningBidAmount = auction.State == AuctionState.Finalized ? auction.WinningBidAmount : null
        };
    }
}