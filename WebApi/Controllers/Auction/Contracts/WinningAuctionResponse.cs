namespace WebApi.Controllers.Auctions.Contracts;

public record WinningAuctionResponse(
    Guid AuctionId,
    string Title,
    string State,
    decimal YourBid,
    DateTime EndTime
);