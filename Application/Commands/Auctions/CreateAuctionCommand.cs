using Application.Api.Auctions;
using Application.Api.Utils;
using Application.Utils;
using Application.Validators.Auctions;
using Domain.Auctions;
using MediatR;

namespace Application.Commands.Auctions;

public class CreateAuctionCommand : IRequest<CreateAuctionCommand.Response>
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public DateTime? StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public AuctionType Type { get; init; }
    public decimal? MinimumIncrement { get; init; }
    public decimal MinPrice { get; init; }
    public TimeSpan? SoftCloseWindow { get; init; }
    public bool ShowMinPrice { get; init; }
    public TieBreakingPolicy TieBreakingPolicy { get; init; }
    public string? Category { get; init; }

    public class Response
    {
        public OkOrError<CreateAuctionError> Result { get; init; }
        public Guid AuctionId { get; init; }
    }
}

public enum CreateAuctionError
{
    InvalidTimeRange,
    InvalidIncrement,
    InvalidTitle
}

public class CreateAuctionCommandHandler(
    IAuctionsRepository auctionsRepository,
    ITimeProvider timeProvider,
    IEnumerable<ICreateAuctionValidator> validators)
    : IRequestHandler<CreateAuctionCommand, CreateAuctionCommand.Response>
{
    public async Task<CreateAuctionCommand.Response> Handle(
        CreateAuctionCommand request,
        CancellationToken cancellationToken)
    {
        var currentTime = timeProvider.Now();
        var validationError = await ValidateCommand(request, currentTime);
        if (validationError != null)
            return ErrorResponse(validationError.Value);

        var auction = CreateAuction(request);
        var created = await auctionsRepository.CreateAuction(auction);

        if (created.CanTransitionToActive(currentTime))
        {
            created.TransitionToActive();
            await auctionsRepository.UpdateAuction(created);
        }

        return SuccessResponse(created.Id);
    }

    private async Task<CreateAuctionError?> ValidateCommand(CreateAuctionCommand command, DateTime currentTime)
    {
        foreach (var validator in validators)
        {
            var error = await validator.Validate(command, currentTime);
            if (error.HasValue) return error;
        }

        return null;
    }

    private static Auction CreateAuction(CreateAuctionCommand request) => new()
    {
        Title = request.Title,
        Description = request.Description,
        StartTime = request.StartTime,
        EndTime = request.EndTime,
        Type = request.Type,
        MinimumIncrement = request.MinimumIncrement,
        MinPrice = request.MinPrice,
        SoftCloseWindow = request.SoftCloseWindow,
        ShowMinPrice = request.ShowMinPrice,
        TieBreakingPolicy = request.TieBreakingPolicy
    };

    private static CreateAuctionCommand.Response ErrorResponse(CreateAuctionError error) =>
        new() { Result = OkOrError<CreateAuctionError>.Error(error) };

    private static CreateAuctionCommand.Response SuccessResponse(Guid auctionId) =>
        new() { Result = OkOrError<CreateAuctionError>.Ok(), AuctionId = auctionId };
}