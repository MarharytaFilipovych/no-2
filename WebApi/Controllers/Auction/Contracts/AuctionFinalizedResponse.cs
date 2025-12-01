namespace WebApi.Controllers.Auctions.Contracts;

public record AuctionFinalizedResponse(Guid? WinnerId, decimal? WinningAmount);
