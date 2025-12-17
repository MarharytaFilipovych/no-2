using WebApi.Controllers.Auctions.Contracts;
using System.Security.Claims;

namespace WebApi.Controllers.Auctions;

using Application.Api.Auctions;
using Application.Commands.Auctions;
using Domain.Auctions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

[ApiController, Route("auctions")]
public class AuctionsController(
    IMediator mediator,
    IAuctionsRepository auctionsRepository,
    IBidsRepository bidsRepository) : ControllerBase
{
    [HttpPost, Authorize]
    [SwaggerResponse(StatusCodes.Status200OK, "Auction created successfully", typeof(AuctionCreatedResponse))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Invalid auction data")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Insufficient permissions")]
    public async Task<IActionResult> CreateAuction(
        [FromBody] CreateAuctionRequest request,
        [FromHeader(Name = "X-Role")] string? roleHeader)
    {
        var role = ParseRole(roleHeader);
        
        var command = MapToCommand(request, role);
        var result = await mediator.Send(command);

        if (result.Result.IsError)
        {
            return result.Result.GetError() switch
            {
                CreateAuctionError.InsufficientPermissions => 
                    StatusCode(403, new { error = "Forbidden: Admin role required" }),
                _ => BadRequest(new { error = result.Result.GetError().ToString() })
            };
        }

        return Ok(new AuctionCreatedResponse(result.AuctionId));
    }

    [HttpGet("active")]
    [SwaggerResponse(StatusCodes.Status200OK, "Active auctions retrieved", typeof(IEnumerable<AuctionListResponse>))]
    public async Task<IActionResult> GetActiveAuctions()
    {
        var auctions = await auctionsRepository.GetAuctionsByState(AuctionState.Active);
        var response = await MapToListResponse(auctions);
        return Ok(response);
    }

    [HttpGet("{auctionId}")]
    [SwaggerResponse(StatusCodes.Status200OK, "Auction details retrieved", typeof(AuctionDetailsResponse))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Auction not found")]
    public async Task<IActionResult> GetAuctionDetails(Guid auctionId)
    {
        var auction = await auctionsRepository.GetAuction(auctionId);
        if (auction == null) return NotFound();

        var response = await MapToDetailsResponse(auction);
        return Ok(response);
    }

    [HttpPost("{auctionId}/bids"), Authorize]
    [SwaggerResponse(StatusCodes.Status200OK, "Bid placed successfully", typeof(BidPlacedResponse))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Invalid bid")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Auction not found")]
    public async Task<IActionResult> PlaceBid(Guid auctionId, [FromBody] PlaceBidRequest request)
    {
        var userId = GetUserId();
        var command = new PlaceBidCommand
        {
            AuctionId = auctionId,
            UserId = userId,
            Amount = request.Amount
        };

        var result = await mediator.Send(command);

        if (result.Result.IsError)
        {
            return result.Result.GetError() switch
            {
                PlaceBidError.AuctionNotFound => NotFound(),
                _ => BadRequest(new { error = result.Result.GetError().ToString() })
            };
        }

        return Ok(new BidPlacedResponse(result.BidId));
    }

    [HttpPost("{auctionId}/finalize"), Authorize]
    [SwaggerResponse(StatusCodes.Status200OK, "Auction finalized", typeof(AuctionFinalizedResponse))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Cannot finalize auction")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Insufficient permissions")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Auction not found")]
    public async Task<IActionResult> FinalizeAuction(
        Guid auctionId,
        [FromHeader(Name = "X-Role")] string? roleHeader)
    {
        var role = ParseRole(roleHeader);
        
        var command = new FinalizeAuctionCommand 
        { 
            AuctionId = auctionId,
            ActorRole = role
        };
        var result = await mediator.Send(command);

        if (result.Result.IsError)
        {
            return result.Result.GetError() switch
            {
                FinalizeAuctionError.AuctionNotFound => 
                    NotFound(new { error = "Auction not found!" }),
                FinalizeAuctionError.InsufficientPermissions => 
                    StatusCode(403, new { error = "Forbidden: Admin role required" }),
                FinalizeAuctionError.AlreadyFinalized => 
                    BadRequest(new { error = "Auction already finalized!" }),
                FinalizeAuctionError.AuctionNotEnded => 
                    BadRequest(new { error = "Auction not ended yet!" }),
                _ => BadRequest(new { error = result.Result.GetError().ToString() })
            };
        }

        return Ok(new AuctionFinalizedResponse(result.WinnerId, result.WinningAmount));
    }

    [HttpPost("{auctionId}/confirm-payment"), Authorize]
    [SwaggerResponse(StatusCodes.Status200OK, "Payment confirmed")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Cannot confirm payment")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Insufficient permissions")]
    public async Task<IActionResult> ConfirmPayment(
        Guid auctionId,
        [FromHeader(Name = "X-Role")] string? roleHeader)
    {
        var role = ParseRole(roleHeader);
        
        var command = new ConfirmPaymentCommand 
        { 
            AuctionId = auctionId,
            ActorRole = role
        };
        var result = await mediator.Send(command);

        if (result.Result.IsError)
        {
            return result.Result.GetError() switch
            {
                ConfirmPaymentError.AuctionNotFound => NotFound(),
                ConfirmPaymentError.InsufficientPermissions => 
                    StatusCode(403, new { error = "Forbidden: Admin role required" }),
                _ => BadRequest(new { error = result.Result.GetError().ToString() })
            };
        }

        return Ok(new { paymentConfirmed = result.PaymentConfirmed });
    }

    [HttpPost("{auctionId}/process-deadline"), Authorize]
    [SwaggerResponse(StatusCodes.Status200OK, "Deadline processed")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Cannot process deadline")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Insufficient permissions")]
    public async Task<IActionResult> ProcessPaymentDeadline(
        Guid auctionId,
        [FromHeader(Name = "X-Role")] string? roleHeader)
    {
        var role = ParseRole(roleHeader);
        
        var command = new ProcessPaymentDeadlineCommand 
        { 
            AuctionId = auctionId,
            ActorRole = role
        };
        var result = await mediator.Send(command);

        if (result.Result.IsError)
        {
            return result.Result.GetError() switch
            {
                ProcessPaymentError.AuctionNotFound => NotFound(),
                ProcessPaymentError.InsufficientPermissions => 
                    StatusCode(403, new { error = "Forbidden: Admin role required" }),
                _ => BadRequest(new { error = result.Result.GetError().ToString() })
            };
        }

        return Ok(new
        {
            newWinnerId = result.NewWinnerId,
            allBidsExhausted = result.AllBidsExhausted
        });
    }

    private static AuctionRole ParseRole(string? roleHeader)
    {
        if (string.IsNullOrWhiteSpace(roleHeader))
            return AuctionRole.Participant;

        return Enum.TryParse<AuctionRole>(roleHeader, ignoreCase: true, out var role) 
            ? role : AuctionRole.Participant;
    }

    private static CreateAuctionCommand MapToCommand(CreateAuctionRequest request, AuctionRole role) => new()
    {
        Title = request.Title,
        Description = request.Description,
        Category = request.Category,
        StartTime = request.StartTime,
        EndTime = request.EndTime,
        Type = request.Type,
        MinimumIncrement = request.MinimumIncrement,
        MinPrice = request.MinPrice,
        SoftCloseWindow = request.SoftCloseWindow,
        ShowMinPrice = request.ShowMinPrice,
        TieBreakingPolicy = request.TieBreakingPolicy,
        ActorRole = role
    };

    private async Task<IEnumerable<AuctionListResponse>> MapToListResponse(List<Auction> auctions)
    {
        var responseTasks = auctions.Select(async a => new AuctionListResponse(
            a.Id, a.Title, a.Description,
            a.EndTime, a.Type.ToString(),
            a.ShowMinPrice ? a.MinPrice : null, a.Type == AuctionType.Open
                ? (await bidsRepository.GetHighestBidForAuction(a.Id))?.Amount : null
        ));

        return await Task.WhenAll(responseTasks);
    }

    private async Task<AuctionDetailsResponse> MapToDetailsResponse(Auction auction)
    {
        var bids = await bidsRepository.GetActiveBidsByAuction(auction.Id);
        var highestBid = await bidsRepository.GetHighestBidForAuction(auction.Id);

        return new AuctionDetailsResponse(
            auction.Id, auction.Title,
            auction.Description, auction.StartTime,
            auction.EndTime, auction.State.ToString(),
            auction.Type.ToString(),
            ShouldShowMinPrice(auction) ? auction.MinPrice : null,
            auction.Type == AuctionType.Open ? highestBid?.Amount : null,
            auction.Type == AuctionType.Blind ? bids.Count : null,
            auction.State == AuctionState.Finalized ? auction.WinnerId : null,
            auction.State == AuctionState.Finalized ? auction.WinningBidAmount : null
        );
    }

    private static bool ShouldShowMinPrice(Auction auction) =>
        auction.ShowMinPrice ||
        auction.State == AuctionState.Ended ||
        auction.State == AuctionState.Finalized;

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
}