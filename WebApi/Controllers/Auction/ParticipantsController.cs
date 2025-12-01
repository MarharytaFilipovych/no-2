using Application.Api.Auctions;
using Domain.Auctions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace WebApi.Controllers.Auctions;

[ApiController, Route("participants"), Authorize]
public class ParticipantsController(IBidsRepository bidsRepository, IAuctionsRepository auctionsRepository) : ControllerBase
{
    /*[HttpPost("deposit")]
    [SwaggerResponse(StatusCodes.Status200OK, "Funds deposited successfully")]
    public IActionResult Deposit([FromBody] DepositRequest request) =>
        Ok(new { message = "Deposit functionality not implemented in this scope" });
    

    [HttpGet("balance")]
    [SwaggerResponse(StatusCodes.Status200OK, "Balance retrieved")]
    public IActionResult GetBalance()
    {
        return Ok(new { balance = 0m, message = "Balance functionality not implemented in this scope" });
    }*/

    [HttpGet("bids")]
    [SwaggerResponse(StatusCodes.Status200OK, "Bid history retrieved")]
    public async Task<IActionResult> GetBidHistory()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var allBids = new List<Bid>();
        var auctions = await auctionsRepository.GetAuctionsByState(AuctionState.Active);
        auctions.AddRange(await auctionsRepository.GetAuctionsByState(AuctionState.Ended));
        auctions.AddRange(await auctionsRepository.GetAuctionsByState(AuctionState.Finalized));

        foreach (var auction in auctions)
        {
            var bids = await bidsRepository.GetBidsByAuction(auction.Id);
            allBids.AddRange(bids.Where(b => b.UserId == userId));
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
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId)) return Unauthorized();
        

        var activeAuctions = await auctionsRepository.GetAuctionsByState(AuctionState.Active);
        var endedAuctions = await auctionsRepository.GetAuctionsByState(AuctionState.Ended);
        
        var winningAuctions = new List<object>();

        foreach (var auction in activeAuctions.Concat(endedAuctions))
        {
            var highestBid = await bidsRepository.GetHighestBidForAuction(auction.Id);
            if (highestBid?.UserId == userId)
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
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId)) return Unauthorized();

        var finalizedAuctions = await auctionsRepository.GetAuctionsByState(AuctionState.Finalized);
        
        var wonAuctions = finalizedAuctions
            .Where(a => a.WinnerId == userId)
            .Select(a => new
            {
                auctionId = a.Id,
                title = a.Title,
                winningAmount = a.WinningBidAmount,
                endTime = a.EndTime
            });

        return Ok(wonAuctions);
    }
}
