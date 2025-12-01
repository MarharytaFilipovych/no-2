using Application.Api.Auctions;
using Domain.Auctions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using WebApi.Controllers.Auctions.Contracts;

namespace WebApi.Controllers.Auctions;

[ApiController, Route("participants"), Authorize]
public class ParticipantsController(IBidsRepository bidsRepository,
    IAuctionsRepository auctionsRepository,
    IParticipantBalanceRepository balanceRepository) : ControllerBase
{
    [HttpPost("deposit")]
    [SwaggerResponse(StatusCodes.Status200OK, "Funds deposited successfully", typeof(DepositResponse))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Invalid deposit amount")]
    public async Task<IActionResult> Deposit([FromBody] DepositRequest request)
    {
        if (request.Amount <= 0)
            return BadRequest(new { error = "Deposit amount must be positive" });

        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        await balanceRepository.DepositFunds(userId.Value, request.Amount);
        var newBalance = await balanceRepository.GetBalance(userId.Value);

        return Ok(new DepositResponse(newBalance, "Funds deposited successfully"));
    }

    [HttpGet("balance")]
    [SwaggerResponse(StatusCodes.Status200OK, "Balance retrieved", typeof(BalanceResponse))]
    public async Task<IActionResult> GetBalance()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var balance = await balanceRepository.GetBalance(userId.Value);
        return Ok(new BalanceResponse(balance));
    }

    [HttpGet("bids")]
    [SwaggerResponse(StatusCodes.Status200OK, "Bid history retrieved", typeof(IEnumerable<BidHistoryResponse>))]
    public async Task<IActionResult> GetBidHistory()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var userBids = await GetAllUserBids(userId.Value);
        var response = userBids
            .OrderByDescending(b => b.PlacedAt)
            .Select(b => new BidHistoryResponse(b.Id, b.AuctionId, b.Amount, b.PlacedAt, b.IsWithdrawn));

        return Ok(response);
    }

    [HttpGet("auctions/winning")]
    [SwaggerResponse(StatusCodes.Status200OK, "Winning auctions retrieved", typeof(IEnumerable<WinningAuctionResponse>))]
    public async Task<IActionResult> GetWinningAuctions()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var winningAuctions = await GetWinningAuctionsForUser(userId.Value);
        return Ok(winningAuctions);
    }

    [HttpGet("auctions/won")]
    [SwaggerResponse(StatusCodes.Status200OK, "Won auctions retrieved", typeof(IEnumerable<WonAuctionResponse>))]
    public async Task<IActionResult> GetWonAuctions()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var finalizedAuctions = await auctionsRepository.GetAuctionsByState(AuctionState.Finalized);

        var wonAuctions = finalizedAuctions
            .Where(a => a.WinnerId == userId.Value)
            .Select(a => new WonAuctionResponse(a.Id, a.Title, a.WinningBidAmount!.Value, a.EndTime));

        return Ok(wonAuctions);
    }

    private async Task<List<Bid>> GetAllUserBids(Guid userId)
    {
        var allAuctions = await GetAllRelevantAuctions();
        var allBids = new List<Bid>();

        foreach (var auction in allAuctions)
        {
            var bids = await bidsRepository.GetBidsByAuction(auction.Id);
            allBids.AddRange(bids.Where(b => b.UserId == userId));
        }

        return allBids;
    }

    private async Task<List<Auction>> GetAllRelevantAuctions()
    {
        var auctions = new List<Auction>();
        auctions.AddRange(await auctionsRepository.GetAuctionsByState(AuctionState.Active));
        auctions.AddRange(await auctionsRepository.GetAuctionsByState(AuctionState.Ended));
        auctions.AddRange(await auctionsRepository.GetAuctionsByState(AuctionState.Finalized));
        return auctions;
    }

    private async Task<List<WinningAuctionResponse>> GetWinningAuctionsForUser(Guid userId)
    {
        var activeAuctions = await auctionsRepository.GetAuctionsByState(AuctionState.Active);
        var endedAuctions = await auctionsRepository.GetAuctionsByState(AuctionState.Ended);
        var winningAuctions = new List<WinningAuctionResponse>();

        foreach (var auction in activeAuctions.Concat(endedAuctions))
        {
            var highestBid = await bidsRepository.GetHighestBidForAuction(auction.Id);
            if (highestBid?.UserId == userId)
            {
                winningAuctions.Add(new WinningAuctionResponse(
                    auction.Id, auction.Title, auction.State.ToString(),
                    highestBid.Amount, auction.EndTime
                ));
            }
        }

        return winningAuctions;
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}