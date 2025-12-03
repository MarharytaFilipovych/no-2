namespace Application.Queries.Auctions;

using Api.Auctions;
using Domain.Auctions;
using MediatR;

public class GetAuctionBidsQuery : IRequest<GetAuctionBidsQuery.Response>
{
    public Guid AuctionId { get; init; }
    public Guid? RequestingUserId { get; init; }

    public class Response
    {
        public List<BidDetailsDTO> Bids { get; init; } = new();
        public bool AuctionNotFound { get; init; }
    }
}

public class GetAuctionBidsQueryHandler(
    IAuctionsRepository auctionsRepository,
    IBidsRepository bidsRepository,
    AuctionVisibilityService visibilityService)
    : IRequestHandler<GetAuctionBidsQuery, GetAuctionBidsQuery.Response>
{
    public async Task<GetAuctionBidsQuery.Response> Handle(
        GetAuctionBidsQuery request,
        CancellationToken cancellationToken)
    {
        var auction = await auctionsRepository.GetAuction(request.AuctionId);
        if (auction == null)
            return new GetAuctionBidsQuery.Response { AuctionNotFound = true };

        var bids = await bidsRepository.GetBidsByAuction(request.AuctionId);
        var bidDtos = bids.Select(bid => BuildBidDto(bid, auction, request.RequestingUserId)).ToList();

        return new GetAuctionBidsQuery.Response { Bids = bidDtos };
    }

    private BidDetailsDTO BuildBidDto(Bid bid, Auction auction, Guid? requestingUserId)
    {
        var canViewDetails = visibilityService.CanViewBidDetails(auction, requestingUserId, bid.UserId);

        return new BidDetailsDTO
        {
            Id = bid.Id,
            AuctionId = bid.AuctionId,
            UserId = bid.UserId,
            Amount = canViewDetails ? bid.Amount : null,
            PlacedAt = bid.PlacedAt,
            IsWithdrawn = bid.IsWithdrawn,
            AmountHidden = !canViewDetails
        };
    }
}

public class GetUserBidsQuery : IRequest<GetUserBidsQuery.Response>
{
    public Guid UserId { get; init; }

    public class Response
    {
        public List<UserBidDTO> Bids { get; init; } = new();
    }
}

public class UserBidDTO
{
    public Guid BidId { get; init; }
    public Guid AuctionId { get; init; }
    public string AuctionTitle { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public DateTime PlacedAt { get; init; }
    public bool IsWithdrawn { get; init; }
    public AuctionState AuctionState { get; init; }
    public bool IsCurrentlyWinning { get; init; }
}

public class GetUserBidsQueryHandler(
    IAuctionsRepository auctionsRepository,
    IBidsRepository bidsRepository)
    : IRequestHandler<GetUserBidsQuery, GetUserBidsQuery.Response>
{
    public async Task<GetUserBidsQuery.Response> Handle(
        GetUserBidsQuery request,
        CancellationToken cancellationToken)
    {
        var allAuctions = await auctionsRepository.GetAuctionsByState(AuctionState.Active);
        allAuctions.AddRange(await auctionsRepository.GetAuctionsByState(AuctionState.Ended));
        allAuctions.AddRange(await auctionsRepository.GetAuctionsByState(AuctionState.Finalized));

        var userBids = new List<UserBidDTO>();

        foreach (var auction in allAuctions)
        {
            var bids = await bidsRepository.GetBidsByAuction(auction.Id);
            var userAuctionBids = bids.Where(b => b.UserId == request.UserId);

            foreach (var bid in userAuctionBids)
            {
                var highestBid = await bidsRepository.GetHighestBidForAuction(auction.Id);
                var isWinning = highestBid?.Id == bid.Id && !bid.IsWithdrawn;

                userBids.Add(new UserBidDTO
                {
                    BidId = bid.Id,
                    AuctionId = auction.Id,
                    AuctionTitle = auction.Title,
                    Amount = bid.Amount,
                    PlacedAt = bid.PlacedAt,
                    IsWithdrawn = bid.IsWithdrawn,
                    AuctionState = auction.State,
                    IsCurrentlyWinning = isWinning
                });
            }
        }

        return new GetUserBidsQuery.Response { Bids = userBids };
    }
}