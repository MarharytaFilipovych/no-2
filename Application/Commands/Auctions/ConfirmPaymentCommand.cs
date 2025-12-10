namespace Application.Commands.Auctions;

using Application.Api.Auctions;
using Application.Api.Utils;
using Application.Validators.Auctions;
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
    DeadlineAlreadyPassed
}

public class ConfirmPaymentCommandHandler(
    IAuctionsRepository auctionsRepository,
    IParticipantBalanceRepository balanceRepository,
    ITimeProvider timeProvider,
    IEnumerable<IConfirmPaymentValidator> validators)
    : IRequestHandler<ConfirmPaymentCommand, ConfirmPaymentCommand.Response>
{
    public async Task<ConfirmPaymentCommand.Response> Handle(ConfirmPaymentCommand request,
        CancellationToken cancellationToken)
    {
        var auction = await auctionsRepository.GetAuction(request.AuctionId);
        var currentTime = timeProvider.Now();

        var balance = auction?.ProvisionalWinnerId != null
            ? await balanceRepository.GetBalance(auction.ProvisionalWinnerId.Value)
            : 0m;

        var validationError = await ValidatePaymentConfirmation(auction, balance, currentTime);
        if (validationError != null)
            return ErrorResponse(validationError.Value);

        auction!.ConfirmPayment();
        await auctionsRepository.UpdateAuction(auction);

        return SuccessResponse(true);
    }

    private async Task<ConfirmPaymentError?> ValidatePaymentConfirmation(Domain.Auctions.Auction? auction,
        decimal balance, DateTime currentTime)
    {
        foreach (var validator in validators)
        {
            var error = await validator.Validate(auction, balance, currentTime);
            if (error.HasValue) return error;
        }

        return null;
    }

    private static ConfirmPaymentCommand.Response ErrorResponse(ConfirmPaymentError error) =>
        new() { Result = OkOrError<ConfirmPaymentError>.Error(error), PaymentConfirmed = false };

    private static ConfirmPaymentCommand.Response SuccessResponse(bool confirmed) =>
        new() { Result = OkOrError<ConfirmPaymentError>.Ok(), PaymentConfirmed = confirmed };
}