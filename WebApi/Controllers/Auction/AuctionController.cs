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
public class AuctionsController(
    IMediator mediator,
    IAuctionsRepository auctionsRepository,
    IBidsRepository bidsRepository) : ControllerBase
{
    [HttpPost]
    [SwaggerResponse(StatusCodes.Status200OK, "Auction created successfully", typeof(AuctionCreatedResponse))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Invalid auction data")]
    public async Task<IActionResult> CreateAuction([FromBody] CreateAuctionRequest request)
    {
        var command = MapToCommand(request);
        var result = await mediator.Send(command);

        return result.Result.IsError
            ? BadRequest(new { error = result.Result.GetError().ToString() })
            : Ok(new AuctionCreatedResponse(result.AuctionId));
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

    [HttpPost("{auctionId}/finalize")]
    [SwaggerResponse(StatusCodes.Status200OK, "Auction finalized", typeof(AuctionFinalizedResponse))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Cannot finalize auction")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Auction not found")]
    public async Task<IActionResult> FinalizeAuction(Guid auctionId)
    {
        var command = new FinalizeAuctionCommand { AuctionId = auctionId };
        var result = await mediator.Send(command);

        if (result.Result.IsError)
        {
            return result.Result.GetError() switch
            {
                FinalizeAuctionError.AuctionNotFound => NotFound(new { error = "Auction not found!" }),
                FinalizeAuctionError.AlreadyFinalized => BadRequest(new { error = "Auction already finalized!" }),
                FinalizeAuctionError.AuctionNotEnded => BadRequest(new { error = "Auction not ended yet!" }),
                _ => BadRequest(new { error = result.Result.GetError().ToString() })
            };
        }

        return Ok(new AuctionFinalizedResponse(result.WinnerId, result.WinningAmount));
    }

    [HttpPost("{auctionId}/confirm-payment")]
    [SwaggerResponse(StatusCodes.Status200OK, "Payment confirmed")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Cannot confirm payment")]
    public async Task<IActionResult> ConfirmPayment(Guid auctionId)
    {
        var command = new ConfirmPaymentCommand { AuctionId = auctionId };
        var result = await mediator.Send(command);

        if (result.Result.IsError)
        {
            return result.Result.GetError() switch
            {
                ConfirmPaymentError.AuctionNotFound => NotFound(),
                _ => BadRequest(new { error = result.Result.GetError().ToString() })
            };
        }

        return Ok(new { paymentConfirmed = result.PaymentConfirmed });
    }

    [HttpPost("{auctionId}/process-deadline")]
    [SwaggerResponse(StatusCodes.Status200OK, "Deadline processed")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Cannot process deadline")]
    public async Task<IActionResult> ProcessPaymentDeadline(Guid auctionId)
    {
        var command = new ProcessPaymentDeadlineCommand { AuctionId = auctionId };
        var result = await mediator.Send(command);

        if (result.Result.IsError)
        {
            return result.Result.GetError() switch
            {
                ProcessPaymentError.AuctionNotFound => NotFound(),
                _ => BadRequest(new { error = result.Result.GetError().ToString() })
            };
        }

        return Ok(new
        {
            newWinnerId = result.NewWinnerId,
            allBidsExhausted = result.AllBidsExhausted
        });
    }

    private static CreateAuctionCommand MapToCommand(CreateAuctionRequest request) => new()
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

    private async Task<IEnumerable<AuctionListResponse>> MapToListResponse(List<Auction> auctions)
    {
        var responseTasks = auctions.Select(async a => new AuctionListResponse(
            a.Id, a.Title, a.Description,
            a.EndTime, a.Type.ToString(),
            a.ShowMinPrice ? a.MinPrice : null, a.Type == AuctionType.Open
                ? (await bidsRepository.GetHighestBidForAuction(a.Id))?.Amount
                : null
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
        Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
}