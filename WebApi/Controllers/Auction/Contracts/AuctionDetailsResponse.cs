namespace WebApi.Controllers.Auctions.Contracts;

public record AuctionDetailsResponse(
    Guid AuctionId,
    string Title,
    string? Description,
    DateTime? StartTime,
    DateTime EndTime,
    string State,
    string Type,
    decimal? MinPrice,
    decimal? CurrentHighestBid,
    int? BidCount,
    Guid? WinnerId,
    decimal? WinningAmount
);