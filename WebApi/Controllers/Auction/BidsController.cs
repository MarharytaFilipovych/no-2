using System.Security.Claims;
using Application.Commands.Auctions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace WebApi.Controllers.Auctions;

[ApiController, Route("bids"), Authorize]
public class BidsController(IMediator mediator) : ControllerBase
{
    [HttpDelete("{bidId}")]
    [SwaggerResponse(StatusCodes.Status200OK, "Bid withdrawn successfully🌈🌈🌈")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Cannot withdraw bid👺")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Bid not found🐌")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Not your bid🥺")]
    [HttpPost("{bidId}/withdraw"), Authorize]
    public async Task<IActionResult> WithdrawBid(Guid bidId)
    {
        var userId = GetCurrentUserId();
        var command = new WithdrawBidCommand { BidId = bidId, UserId = userId };
        var result = await mediator.Send(command);

        return result.Result.IsError 
            ? MapErrorToResponse(result.Result.GetError())
            : Ok(new { message = "Bid withdrawn successfully" });
    }

    private IActionResult MapErrorToResponse(WithdrawBidError error) => error switch
    {
        WithdrawBidError.BidNotFound => NotFound(new { error = "Bid not found" }),
        WithdrawBidError.NotBidOwner => Forbid(),
        WithdrawBidError.AuctionNotActive => BadRequest(new { error = "Can only withdraw bids from active auctions" }),
        WithdrawBidError.AlreadyWithdrawn => BadRequest(new { error = "Bid already withdrawn" }),
        _ => BadRequest(new { error = error.ToString() })
    };

    private Guid GetCurrentUserId() => 
        Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
}
