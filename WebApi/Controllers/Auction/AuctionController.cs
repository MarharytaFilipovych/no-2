using WebApi.Controllers.Auctions.Contracts;

namespace WebApi.Controllers.Auctions;

using Application.Api.Auctions;
using Application.Commands.Auctions;
using Domain.Auctions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

[ApiController, Route("auctions")]
public class AuctionsController(IMediator mediator, IAuctionsRepository auctionsRepository,
    IBidsRepository bidsRepository) : ControllerBase
{
    [HttpPost]
    [SwaggerResponse(StatusCodes.Status200OK, "Auction created successfully")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Invalid auction data")]
    public async Task<IActionResult> CreateAuction([FromBody] CreateAuctionRequest request)
    {
        var command = new CreateAuctionCommand
        {
            Title = request.Title,
            Description = request.Description,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Type = request.Type,
            MinimumIncrement = request.MinimumIncrement,
            MinPrice = request.MinPrice,
            SoftCloseWindow = request.SoftCloseWindow,
            ShowMinPrice = request.ShowMinPrice,
            TieBreakingPolicy = request.TieBreakingPolicy
        };

        var result = await mediator.Send(command);

        if (result.Result.IsError) 
            return BadRequest(new { error = result.Result.GetError().ToString() });

        return Ok(new { auctionId = result.AuctionId });
    }

    [HttpGet("active")]
    [SwaggerResponse(StatusCodes.Status200OK, "Active auctions retrieved")]
    public async Task<IActionResult> GetActiveAuctions()
    {
        var auctions = await auctionsRepository.GetAuctionsByState(AuctionState.Active);

        var response = auctions.Select(a => new
        {
            auctionId = a.Id,
            title = a.Title,
            description = a.Description,
            endTime = a.EndTime,
            type = a.Type.ToString(),
            minPrice = a.ShowMinPrice ? a.MinPrice : (decimal?)null,
            currentHighestBid = a.Type == AuctionType.Open 
                ? bidsRepository.GetHighestBidForAuction(a.Id).Result?.Amount 
                : null
        });

        return Ok(response);
    }

    [HttpGet("{auctionId}")]
    [SwaggerResponse(StatusCodes.Status200OK, "Auction details retrieved")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Auction not found")]
    public async Task<IActionResult> GetAuctionDetails(Guid auctionId)
    {
        var auction = await auctionsRepository.GetAuction(auctionId);
        if (auction == null) return NotFound();

        var bids = await bidsRepository.GetActiveBidsByAuction(auctionId);
        var highestBid = await bidsRepository.GetHighestBidForAuction(auctionId);

        var response = new
        {
            auctionId = auction.Id,
            title = auction.Title,
            description = auction.Description,
            startTime = auction.StartTime,
            endTime = auction.EndTime,
            state = auction.State.ToString(),
            type = auction.Type.ToString(),
            minPrice = auction.ShowMinPrice || 
                       auction.State == AuctionState.Ended ||
                       auction.State == AuctionState.Finalized 
                ? auction.MinPrice 
                : (decimal?)null,
            currentHighestBid = auction.Type == AuctionType.Open ? highestBid?.Amount : null,
            bidCount = auction.Type == AuctionType.Blind ? bids.Count : (int?)null,
            winnerId = auction.State == AuctionState.Finalized ? auction.WinnerId : null,
            winningAmount = auction.State == AuctionState.Finalized ? auction.WinningBidAmount : null
        };

        return Ok(response);
    }

    [HttpPost("{auctionId}/bids"), Authorize]
    [SwaggerResponse(StatusCodes.Status200OK, "Bid placed successfully")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Invalid bid")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Auction not found")]
    public async Task<IActionResult> PlaceBid(Guid auctionId, [FromBody] PlaceBidRequest request)
    {
        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");

        var command = new PlaceBidCommand
        {
            AuctionId = auctionId,
            UserId = userId,
            Amount = request.Amount
        };

        var result = await mediator.Send(command);

        if (result.Result.IsError)
        {
            var error = result.Result.GetError();
            return error switch
            {
                PlaceBidError.AuctionNotFound => NotFound(),
                _ => BadRequest(new { error = error.ToString() })
            };
        }

        return Ok(new { bidId = result.BidId });
    }

    [HttpPost("{auctionId}/finalize")]
    [SwaggerResponse(StatusCodes.Status200OK, "Auction finalized")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Cannot finalize auction")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Auction not found")]
    public async Task<IActionResult> FinalizeAuction(Guid auctionId)
    {
        var auction = await auctionsRepository.GetAuction(auctionId);
        if (auction == null) return NotFound();
        if (!auction.CanFinalize()) 
            return BadRequest(new { error = "Auction must be in Ended state to finalize" });
        

        var highestBid = await bidsRepository.GetHighestBidForAuction(auctionId);

        if (highestBid != null && highestBid.Amount >= auction.MinPrice) 
            auction.Finalize(highestBid.UserId, highestBid.Amount);
        else auction.Finalize(null, null);

        await auctionsRepository.UpdateAuction(auction);

        return Ok(new
        {
            winnerId = auction.WinnerId,
            winningAmount = auction.WinningBidAmount
        });
    }
}