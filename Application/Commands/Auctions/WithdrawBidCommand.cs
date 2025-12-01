using Application.Api.Auctions;
using Application.Api.Utils;
using Application.Utils;
using MediatR;

namespace Application.Commands.Auctions;

public class WithdrawBidCommand : IRequest<WithdrawBidCommand.Response>
{
    public Guid BidId { get; init; }
    public Guid UserId { get; init; }

    public class Response
    {
        public OkOrError<WithdrawBidError> Result { get; init; }
    }
}

public enum WithdrawBidError
{
    BidNotFound,
    NotBidOwner,
    AuctionNotActive,
    AlreadyWithdrawn
}


public class WithdrawBidCommandHandler(
    IBidsRepository bidsRepository,
    IAuctionsRepository auctionsRepository,
    ITimeProvider timeProvider)
    : IRequestHandler<WithdrawBidCommand, WithdrawBidCommand.Response>
{
    public async Task<WithdrawBidCommand.Response> Handle(WithdrawBidCommand request, CancellationToken cancellationToken)
    {
        var bid = await bidsRepository.GetBid(request.BidId);
        if (bid == null)
            return ErrorResponse(WithdrawBidError.BidNotFound);

        if (bid.UserId != request.UserId)
            return ErrorResponse(WithdrawBidError.NotBidOwner);

        if (bid.IsWithdrawn)
            return ErrorResponse(WithdrawBidError.AlreadyWithdrawn);

        var auction = await auctionsRepository.GetAuction(bid.AuctionId);
        if (auction == null || !auction.IsActive(timeProvider.Now()))
            return ErrorResponse(WithdrawBidError.AuctionNotActive);

        bid.Withdraw();
        await bidsRepository.UpdateBid(bid);

        return new WithdrawBidCommand.Response
        {
            Result = OkOrError<WithdrawBidError>.Ok()
        };
    }

    private WithdrawBidCommand.Response ErrorResponse(WithdrawBidError error) =>
        new() { Result = OkOrError<WithdrawBidError>.Error(error) };
}