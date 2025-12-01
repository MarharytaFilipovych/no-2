namespace Application.Validators.Auctions;

using Commands.Auctions;
using Domain.Auctions;

public interface ICreateAuctionValidator
{
    Task<CreateAuctionError?> Validate(CreateAuctionCommand command, DateTime currentTime);
}

public class AuctionTitleValidator : ICreateAuctionValidator
{
    public Task<CreateAuctionError?> Validate(CreateAuctionCommand command, DateTime currentTime)
    {
        return Task.FromResult<CreateAuctionError?>(
            string.IsNullOrWhiteSpace(command.Title)
                ? CreateAuctionError.InvalidTitle
                : null);
    }
}

public class AuctionTimeRangeValidator : ICreateAuctionValidator
{
    public Task<CreateAuctionError?> Validate(CreateAuctionCommand command, DateTime currentTime)
    {
        if (command.StartTime.HasValue && command.StartTime.Value >= command.EndTime)
            return Task.FromResult<CreateAuctionError?>(CreateAuctionError.InvalidTimeRange);

        return Task.FromResult<CreateAuctionError?>(null);
    }
}

public class OpenAuctionRequiresIncrementValidator : ICreateAuctionValidator
{
    public Task<CreateAuctionError?> Validate(CreateAuctionCommand command, DateTime currentTime)
    {
        if (command.Type == AuctionType.Open && command.MinimumIncrement is not > 0)
            return Task.FromResult<CreateAuctionError?>(CreateAuctionError.InvalidIncrement);

        return Task.FromResult<CreateAuctionError?>(null);
    }
}