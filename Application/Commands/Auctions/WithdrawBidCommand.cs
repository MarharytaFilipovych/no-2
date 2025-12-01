using Application.Api.Auctions;
using Application.Api.Utils;
using Application.Utils;
using Application.Validators.Auctions;
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
    ITimeProvider timeProvider,
    IEnumerable<IWithdrawBidValidator> validators)
    : IRequestHandler<WithdrawBidCommand, WithdrawBidCommand.Response>
{
    public async Task<WithdrawBidCommand.Response> Handle(
        WithdrawBidCommand request,
        CancellationToken cancellationToken)
    {
        var bid = await bidsRepository.GetBid(request.BidId);
        if (bid == null) return ErrorResponse(WithdrawBidError.BidNotFound);

        var auction = await auctionsRepository.GetAuction(bid.AuctionId);
        if (auction == null) return ErrorResponse(WithdrawBidError.AuctionNotActive);

        var currentTime = timeProvider.Now();
        var validationError = await ValidateWithdrawal(bid, auction, request.UserId, currentTime);
        if (validationError != null) return ErrorResponse(validationError.Value);

        bid.Withdraw();
        await bidsRepository.UpdateBid(bid);

        return SuccessResponse();
    }

    private async Task<WithdrawBidError?> ValidateWithdrawal(
        Domain.Auctions.Bid bid,
        Domain.Auctions.Auction auction,
        Guid userId,
        DateTime currentTime)
    {
        foreach (var validator in validators)
        {
            var error = await validator.Validate(bid, auction, userId, currentTime);
            if (error.HasValue) return error;
        }

        return null;
    }

    private static WithdrawBidCommand.Response ErrorResponse(WithdrawBidError error) =>
        new() { Result = OkOrError<WithdrawBidError>.Error(error) };

    private static WithdrawBidCommand.Response SuccessResponse() =>
        new() { Result = OkOrError<WithdrawBidError>.Ok() };
}