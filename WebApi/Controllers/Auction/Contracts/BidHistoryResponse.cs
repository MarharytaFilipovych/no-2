namespace WebApi.Controllers.Auctions.Contracts;

public record BidHistoryResponse(
    Guid BidId,
    Guid AuctionId,
    decimal Amount,
    DateTime PlacedAt,
    bool IsWithdrawn
);