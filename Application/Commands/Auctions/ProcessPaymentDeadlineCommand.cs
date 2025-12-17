namespace Application.Commands.Auctions;

using Application.Api.Auctions;
using Api.Users;
using Application.Api.Utils;
using Configs;
using Utils;
using Validators.Auctions;
using Domain.Auctions;
using Domain.Users;
using MediatR;

public class ProcessPaymentDeadlineCommand : IRequest<ProcessPaymentDeadlineCommand.Response>
{
    public Guid AuctionId { get; init; }
    public AuctionRole ActorRole { get; init; }

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
    DeadlineNotPassed,
    InsufficientPermissions
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
    BanPolicy banPolicy,
    IEnumerable<IProcessPaymentDeadlineValidator> validators)
    : IRequestHandler<ProcessPaymentDeadlineCommand, ProcessPaymentDeadlineCommand.Response>
{
    public async Task<ProcessPaymentDeadlineCommand.Response> Handle(ProcessPaymentDeadlineCommand request,
        CancellationToken cancellationToken)
    {
        if (!AuctionPermissions.CanProcessPaymentDeadline(request.ActorRole))
            return ErrorResponse(ProcessPaymentError.InsufficientPermissions);

        var auction = await auctionsRepository.GetAuction(request.AuctionId);
        if (auction == null)
            return ErrorResponse(ProcessPaymentError.AuctionNotFound);

        var currentTime = timeProvider.Now();
        
        if (auction.CanTransitionToEnded(currentTime))
        {
            auction.TransitionToEnded();
            await auctionsRepository.UpdateAuction(auction);
        }

        var validationError = await ValidateDeadlineProcessing(auction, currentTime);
        if (validationError != null)
            return ErrorResponse(validationError.Value);

        var balance = await balanceRepository.GetBalance(auction!.ProvisionalWinnerId!.Value);
        var allBids = await bidsRepository.GetActiveBidsByAuction(auction.Id);
        var excludedUsers = await GetExcludedUsersForAuction(auction);

        var result = paymentProcessingService.ProcessPaymentDeadline(auction, allBids, balance,
            excludedUsers ?? [], userId => IsUserBanned(userId, currentTime).Result);

        if (result.IsConfirmed)
        {
            await auctionsRepository.UpdateAuction(auction);
            return SuccessResponse(result.ConfirmedWinnerId, false);
        }

        await BanUserForPaymentFailure(result.RejectedUserId!.Value, currentTime);

        var newWinner = await winnerSelectionService.SelectWinner(auction, result.EligibleBidsForPromotion);

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

    private async Task<ProcessPaymentError?> ValidateDeadlineProcessing(Auction? auction, DateTime currentTime)
    {
        foreach (var validator in validators)
        {
            var error = await validator.Validate(auction, currentTime);
            if (error.HasValue) return error;
        }

        return null;
    }

    private async Task<HashSet<Guid>?> GetExcludedUsersForAuction(Auction auction)
    {
        if (string.IsNullOrEmpty(auction.Category))
            return null;

        var cycle = await cycleRepository.GetActiveCycle();
        if (cycle == null)
            return null;

        var finalizedInCategory = await auctionsRepository
            .GetFinalizedAuctionsByCategoryAndPeriod(auction.Category, cycle.StartDate, cycle.EndDate);

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