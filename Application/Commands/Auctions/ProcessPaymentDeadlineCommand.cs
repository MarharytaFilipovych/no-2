namespace Application.Commands.Auctions;

using Application.Api.Auctions;
using Api.Users;
using Application.Api.Utils;
using Configs;
using Utils;
using Domain.Auctions;
using MediatR;

public class ProcessPaymentDeadlineCommand : IRequest<ProcessPaymentDeadlineCommand.Response>
{
    public Guid AuctionId { get; init; }

    public class Response
    {
        public OkOrError<ProcessPaymentError> Result { get; init; }
        public Guid? NewWinnerId { get; init; }
        public bool AllBidsExhausted { get; init; }
    }
}

public enum ProcessPaymentError
{
    AuctionNotFound,
    NoProvisionalWinner,
    DeadlineNotPassed
}

public class ProcessPaymentDeadlineCommandHandler(
    IAuctionsRepository auctionsRepository,
    IBidsRepository bidsRepository,
    IParticipantBalanceRepository balanceRepository,
    IUsersRepository usersRepository,
    ITimeProvider timeProvider,
    IPaymentWindowConfig paymentConfig,
    WinnerSelectionService winnerSelectionService)
    : IRequestHandler<ProcessPaymentDeadlineCommand, ProcessPaymentDeadlineCommand.Response>
{
    public async Task<ProcessPaymentDeadlineCommand.Response> Handle(
        ProcessPaymentDeadlineCommand request,
        CancellationToken cancellationToken)
    {
        var auction = await auctionsRepository.GetAuction(request.AuctionId);
        if (auction == null)
            return ErrorResponse(ProcessPaymentError.AuctionNotFound);

        if (!auction.HasProvisionalWinner())
            return ErrorResponse(ProcessPaymentError.NoProvisionalWinner);

        var currentTime = timeProvider.Now();
        if (!auction.IsPaymentDeadlinePassed(currentTime))
            return ErrorResponse(ProcessPaymentError.DeadlineNotPassed);

        var balance = await balanceRepository.GetBalance(auction.ProvisionalWinnerId!.Value);
        if (balance >= auction.ProvisionalWinningAmount!.Value)
        {
            auction.ConfirmPayment();
            await auctionsRepository.UpdateAuction(auction);
            return SuccessResponse(auction.WinnerId, false);
        }

        var rejectedUserId = auction.ProvisionalWinnerId.Value;
        await BanUser(rejectedUserId, currentTime);
        auction.RejectProvisionalWinner();

        var newWinner = await PromoteNextEligibleBid(auction, rejectedUserId);

        if (newWinner != null)
        {
            var deadline = currentTime.Add(paymentConfig.PaymentDeadline);
            auction.SetProvisionalWinner(newWinner.UserId, newWinner.Amount, deadline);
            await auctionsRepository.UpdateAuction(auction);
            return SuccessResponse(newWinner.UserId, false);
        }

        await auctionsRepository.UpdateAuction(auction);
        return SuccessResponse(null, true);
    }

    private async Task BanUser(Guid userId, DateTime currentTime)
    {
        var user = await usersRepository.GetUser(userId);
        if (user != null)
        {
            var banUntil = currentTime.AddDays(paymentConfig.BanDurationDays);
            user.Ban(banUntil);
            await usersRepository.UpdateUser(user);
        }
    }

    private async Task<Bid?> PromoteNextEligibleBid(Domain.Auctions.Auction auction, Guid rejectedUserId)
    {
        var allBids = await bidsRepository.GetActiveBidsByAuction(auction.Id);

        var eligibleBids = allBids
            .Where(b => b.UserId != rejectedUserId)
            .Where(b => b.Amount >= auction.MinPrice)
            .ToList();

        if (eligibleBids.Count == 0)
            return null;

        return await winnerSelectionService.SelectWinner(auction, eligibleBids);
    }

    private static ProcessPaymentDeadlineCommand.Response ErrorResponse(ProcessPaymentError error) =>
        new()
        {
            Result = OkOrError<ProcessPaymentError>.Error(error),
            NewWinnerId = null,
            AllBidsExhausted = false
        };

    private static ProcessPaymentDeadlineCommand.Response SuccessResponse(Guid? winnerId, bool exhausted) =>
        new()
        {
            Result = OkOrError<ProcessPaymentError>.Ok(),
            NewWinnerId = winnerId,
            AllBidsExhausted = exhausted
        };
}