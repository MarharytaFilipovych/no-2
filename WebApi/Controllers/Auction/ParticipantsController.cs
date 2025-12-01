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
    [SwaggerResponse(StatusCodes.Status200OK, "Funds deposited successfully🤌🏿")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Invalid deposit amount👺")]
    public async Task<IActionResult> Deposit([FromBody] DepositRequest request)
    {
        if (request.Amount <= 0)
            return BadRequest(new { error = "Deposit amount must be positive!" });

        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        await balanceRepository.DepositFunds(userId.Value, request.Amount);

        var newBalance = await balanceRepository.GetBalance(userId.Value);
        return Ok(new { balance = newBalance, message = "Funds deposited successfully💚" });
    }

    [HttpGet("balance")]
    [SwaggerResponse(StatusCodes.Status200OK, "Balance retrieved🌈")]
    public async Task<IActionResult> GetBalance()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var balance = await balanceRepository.GetBalance(userId.Value);
        return Ok(new { balance });
    }

    [HttpGet("bids")]
    [SwaggerResponse(StatusCodes.Status200OK, "Bid history retrieved💌")]
    public async Task<IActionResult> GetBidHistory()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var allBids = new List<Bid>();
        var auctions = await auctionsRepository.GetAuctionsByState(AuctionState.Active);
        auctions.AddRange(await auctionsRepository.GetAuctionsByState(AuctionState.Ended));
        auctions.AddRange(await auctionsRepository.GetAuctionsByState(AuctionState.Finalized));

        foreach (var auction in auctions)
        {
            var bids = await bidsRepository.GetBidsByAuction(auction.Id);
            allBids.AddRange(bids.Where(b => b.UserId == userId.Value));
        }

        var response = allBids.OrderByDescending(b => b.PlacedAt).Select(b => new
        {
            bidId = b.Id,
            auctionId = b.AuctionId,
            amount = b.Amount,
            placedAt = b.PlacedAt,
            isWithdrawn = b.IsWithdrawn
        });

        return Ok(response);
    }

    [HttpGet("auctions/winning")]
    [SwaggerResponse(StatusCodes.Status200OK, "Winning auctions retrieved")]
    public async Task<IActionResult> GetWinningAuctions()
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized();

        var activeAuctions = await auctionsRepository.GetAuctionsByState(AuctionState.Active);
        var endedAuctions = await auctionsRepository.GetAuctionsByState(AuctionState.Ended);

        var winningAuctions = new List<object>();

        foreach (var auction in activeAuctions.Concat(endedAuctions))
        {
            var highestBid = await bidsRepository.GetHighestBidForAuction(auction.Id);
            if (highestBid?.UserId == userId.Value)
            {
                winningAuctions.Add(new
                {
                    auctionId = auction.Id,
                    title = auction.Title,
                    state = auction.State.ToString(),
                    yourBid = highestBid.Amount,
                    endTime = auction.EndTime
                });
            }
        }

        return Ok(winningAuctions);
    }

    [HttpGet("auctions/won")]
    [SwaggerResponse(StatusCodes.Status200OK, "Won auctions retrieved")]
    public async Task<IActionResult> GetWonAuctions()
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized();

        var finalizedAuctions = await auctionsRepository.GetAuctionsByState(AuctionState.Finalized);

        var wonAuctions = finalizedAuctions
            .Where(a => a.WinnerId == userId.Value)
            .Select(a => new
            {
                auctionId = a.Id,
                title = a.Title,
                winningAmount = a.WinningBidAmount,
                endTime = a.EndTime
            });

        return Ok(wonAuctions);
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}