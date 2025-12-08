namespace Application.Commands.Auctions;

using Application.Api.Auctions;
using Api.Users;
using Application.Api.Utils;
using Configs;
using Utils;
using Domain.Auctions;
using Domain.Users;
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
    IAuctionCycleRepository cycleRepository,
    IParticipantBalanceRepository balanceRepository,
    IUsersRepository usersRepository,
    ITimeProvider timeProvider,
    IPaymentWindowConfig paymentConfig,
    WinnerSelectionService winnerSelectionService,
    NoRepeatWinnerPolicy noRepeatWinnerPolicy,
    PaymentProcessingService paymentProcessingService,
    BanPolicy banPolicy)
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

        var allBids = await bidsRepository.GetActiveBidsByAuction(auction.Id);

        var excludedUsers = await GetExcludedUsersForAuction(auction, currentTime);

        var result = paymentProcessingService.ProcessPaymentDeadline(
            auction,
            allBids,
            balance,
            excludedUsers ?? new HashSet<Guid>(),
            userId => IsUserBanned(userId, currentTime).Result);

        if (result.IsConfirmed)
        {
            await auctionsRepository.UpdateAuction(auction);
            return SuccessResponse(result.ConfirmedWinnerId, false);
        }

        await BanUserForPaymentFailure(result.RejectedUserId!.Value, currentTime);

        var newWinner = await winnerSelectionService.SelectWinner(
            auction, 
            result.EligibleBidsForPromotion, 
            null);

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

    private async Task<HashSet<Guid>?> GetExcludedUsersForAuction(Auction auction, DateTime currentTime)
    {
        if (string.IsNullOrEmpty(auction.Category))
            return null;

        var cycle = await cycleRepository.GetActiveCycle();
        if (cycle == null)
            return null;

        var finalizedInCategory = await auctionsRepository
            .GetFinalizedAuctionsByCategoryAndPeriod(
                auction.Category,
                cycle.StartDate,
                cycle.EndDate);

        return noRepeatWinnerPolicy.GetExcludedUsers(auction, finalizedInCategory);
    }

    private async Task<bool> IsUserBanned(Guid userId, DateTime currentTime)
    {
        var user = await usersRepository.GetUser(userId);
        return user?.IsBanned(currentTime) ?? false;
    }

    private async Task BanUserForPaymentFailure(Guid userId, DateTime currentTime)
    {
        var user = await usersRepository.GetUser(userId);
        if (user == null)
            return;

        banPolicy.BanForPaymentFailure(user, currentTime, paymentConfig.BanDurationDays);
        await usersRepository.UpdateUser(user);
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