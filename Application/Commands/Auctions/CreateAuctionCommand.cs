using Application.Api.Auctions;
using Application.Api.Utils;
using Application.Utils;
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

public class CreateAuctionCommandHandler(IAuctionsRepository auctionsRepository,
    ITimeProvider timeProvider) : IRequestHandler<CreateAuctionCommand, CreateAuctionCommand.Response>
{
    public async Task<CreateAuctionCommand.Response> Handle(
        CreateAuctionCommand request,
        CancellationToken cancellationToken)
    {
        var validationResult = ValidateRequest(request);
        if (validationResult != null)
        {
            return new CreateAuctionCommand.Response
            {
                Result = OkOrError<CreateAuctionError>.Error(validationResult.Value)
            };
        }

        var auction = new Auction
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

        var created = await auctionsRepository.CreateAuction(auction);

        if (auction.CanTransitionToActive(timeProvider.Now()))
        {
            created.TransitionToActive();
            await auctionsRepository.UpdateAuction(created);
        }

        return new CreateAuctionCommand.Response
        {
            Result = OkOrError<CreateAuctionError>.Ok(),
            AuctionId = created.Id
        };
    }

    private static CreateAuctionError? ValidateRequest(CreateAuctionCommand request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return CreateAuctionError.InvalidTitle;

        if (request.StartTime.HasValue && request.StartTime.Value >= request.EndTime)
            return CreateAuctionError.InvalidTimeRange;

        if (request is { Type: AuctionType.Open, MinimumIncrement: not > 0 })
            return CreateAuctionError.InvalidIncrement;

        return null;
    }
}

