namespace Application.Commands.Auctions;

using Application.Api.Auctions;
using Application.Api.Utils;
using Utils;
using MediatR;

public class ConfirmPaymentCommand : IRequest<ConfirmPaymentCommand.Response>
{
    public Guid AuctionId { get; init; }

    public class Response
    {
        public OkOrError<ConfirmPaymentError> Result { get; init; }
        public bool PaymentConfirmed { get; init; }
    }
}

public enum ConfirmPaymentError
{
    AuctionNotFound,
    NoProvisionalWinner,
    InsufficientBalance,
    DeadlineNotPassed
}

public class ConfirmPaymentCommandHandler(
    IAuctionsRepository auctionsRepository,
    IParticipantBalanceRepository balanceRepository,
    ITimeProvider timeProvider)
    : IRequestHandler<ConfirmPaymentCommand, ConfirmPaymentCommand.Response>
{
    public async Task<ConfirmPaymentCommand.Response> Handle(
        ConfirmPaymentCommand request,
        CancellationToken cancellationToken)
    {
        var auction = await auctionsRepository.GetAuction(request.AuctionId);
        if (auction == null)
            return ErrorResponse(ConfirmPaymentError.AuctionNotFound);

        if (!auction.HasProvisionalWinner())
            return ErrorResponse(ConfirmPaymentError.NoProvisionalWinner);

        var currentTime = timeProvider.Now();

        var balance = await balanceRepository.GetBalance(auction.ProvisionalWinnerId!.Value);
        if (balance >= auction.ProvisionalWinningAmount!.Value)
        {
            auction.ConfirmPayment();
            await auctionsRepository.UpdateAuction(auction);
            return SuccessResponse(true);
        }

        if (!auction.IsPaymentDeadlinePassed(currentTime))
            return ErrorResponse(ConfirmPaymentError.InsufficientBalance);

        return ErrorResponse(ConfirmPaymentError.InsufficientBalance);
    }

    private static ConfirmPaymentCommand.Response ErrorResponse(ConfirmPaymentError error) =>
        new() { Result = OkOrError<ConfirmPaymentError>.Error(error), PaymentConfirmed = false };

    private static ConfirmPaymentCommand.Response SuccessResponse(bool confirmed) =>
        new() { Result = OkOrError<ConfirmPaymentError>.Ok(), PaymentConfirmed = confirmed };
}