namespace Application.Queries.Auctions;

using Api.Auctions;
using Domain.Auctions;
using MediatR;

public class ListActiveAuctionsQuery : IRequest<ListActiveAuctionsQuery.Response>
{
    public Guid? RequestingUserId { get; init; }

    public class Response
    {
        public List<AuctionListItemDTO> Auctions { get; init; } = new();
    }
}

public class ListActiveAuctionsQueryHandler(
    IAuctionsRepository auctionsRepository,
    IBidsRepository bidsRepository,
    AuctionVisibilityService visibilityService)
    : IRequestHandler<ListActiveAuctionsQuery, ListActiveAuctionsQuery.Response>
{
    public async Task<ListActiveAuctionsQuery.Response> Handle(
        ListActiveAuctionsQuery request,
        CancellationToken cancellationToken)
    {
        var activeAuctions = await auctionsRepository.GetAuctionsByState(AuctionState.Active);
        
        var auctionDtos = new List<AuctionListItemDTO>();
        foreach (var auction in activeAuctions)
        {
            var dto = await BuildListItemDto(auction);
            auctionDtos.Add(dto);
        }

        return new ListActiveAuctionsQuery.Response { Auctions = auctionDtos };
    }

    private async Task<AuctionListItemDTO> BuildListItemDto(Auction auction)
    {
        var showHighestBid = visibilityService.ShouldShowHighestBid(auction);
        var showMinPrice = visibilityService.ShouldShowMinPrice(auction);
        var showBidCount = visibilityService.ShouldShowBidCount(auction);

        var highestBid = showHighestBid ? await bidsRepository.GetHighestBidForAuction(auction.Id) : null;
        var bidCount = showBidCount ? (await bidsRepository.GetActiveBidsByAuction(auction.Id)).Count : 0;

        return new AuctionListItemDTO
        {
            Id = auction.Id,
            Title = auction.Title,
            Type = auction.Type,
            State = auction.State,
            EndTime = auction.EndTime,
            CurrentHighestBid = showHighestBid ? highestBid?.Amount : null,
            BidCount = showBidCount ? bidCount : null,
            MinPrice = showMinPrice ? auction.MinPrice : null
        };
    }
}