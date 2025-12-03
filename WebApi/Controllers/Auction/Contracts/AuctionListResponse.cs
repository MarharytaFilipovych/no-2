namespace WebApi.Controllers.Auctions.Contracts;

public record AuctionListResponse(
    Guid AuctionId,
    string Title,
    string? Description,
    DateTime EndTime,
    string Type,
    decimal? MinPrice,
    decimal? CurrentHighestBid
);