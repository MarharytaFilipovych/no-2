namespace WebApi.Controllers.Auctions.Contracts;

public record WonAuctionResponse(
    Guid AuctionId,
    string Title,
    decimal WinningAmount,
    DateTime EndTime
);