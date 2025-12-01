using Application.Api.Auctions;
using Application.Commands.Auctions;
using Application.Validators.Auctions;
using Domain.Auctions;
using Infrastructure.InMemory;
using Tests.Utils;

namespace Tests.Validation;

[TestFixture]
public class BidWithdrawalCommandTests
{
    private IAuctionsRepository _auctionsRepository = null!;
    private IBidsRepository _bidsRepository = null!;
    private TestTimeProvider _timeProvider = null!;

    [SetUp]
    public void SetUp()
    {
        _auctionsRepository = new AuctionsRepository();
        _bidsRepository = new BidsRepository();
        _timeProvider = new TestTimeProvider();
    }

    [Test]
    public async Task WithdrawBid_FromActiveAuction_ShouldSucceed()
    {
        // Arrange
        var auction = await CreateActiveAuction();
        var userId = Guid.NewGuid();
        var bid = await PlaceBid(auction.Id, userId, 150);
        var handler = CreateHandler();
        var command = new WithdrawBidCommand
        {
            BidId = bid.Id,
            UserId = userId
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsOk, Is.True);
        var updatedBid = await _bidsRepository.GetBid(bid.Id);
        Assert.That(updatedBid!.IsWithdrawn, Is.True);
    }

    [Test]
    public async Task WithdrawBid_FromEndedAuction_ShouldFail()
    {
        // Arrange
        var auction = await CreateEndedAuction();
        var userId = Guid.NewGuid();
        var bid = await PlaceBid(auction.Id, userId, 150);
        var handler = CreateHandler();
        var command = new WithdrawBidCommand
        {
            BidId = bid.Id,
            UserId = userId
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsError, Is.True);
        Assert.That(result.Result.GetError(), Is.EqualTo(WithdrawBidError.AuctionNotActive));
    }

    [Test]
    public async Task WithdrawBid_ByNonOwner_ShouldFail()
    {
        // Arrange
        var auction = await CreateActiveAuction();
        var userId = Guid.NewGuid();
        var bid = await PlaceBid(auction.Id, userId, 150);
        var handler = CreateHandler();
        var command = new WithdrawBidCommand
        {
            BidId = bid.Id,
            UserId = Guid.NewGuid()
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsError, Is.True);
        Assert.That(result.Result.GetError(), Is.EqualTo(WithdrawBidError.NotBidOwner));
    }

    [Test]
    public async Task WithdrawBid_AlreadyWithdrawn_ShouldFail()
    {
        // Arrange
        var auction = await CreateActiveAuction();
        var userId = Guid.NewGuid();
        var bid = await PlaceBid(auction.Id, userId, 150);
        bid.Withdraw();
        await _bidsRepository.UpdateBid(bid);
        var handler = CreateHandler();
        var command = new WithdrawBidCommand
        {
            BidId = bid.Id,
            UserId = userId
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsError, Is.True);
        Assert.That(result.Result.GetError(), Is.EqualTo(WithdrawBidError.AlreadyWithdrawn));
    }

    [Test]
    public async Task WithdrawBid_NonExistentBid_ShouldFail()
    {
        // Arrange
        var handler = CreateHandler();
        var command = new WithdrawBidCommand
        {
            BidId = Guid.NewGuid(),
            UserId = Guid.NewGuid()
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsError, Is.True);
        Assert.That(result.Result.GetError(), Is.EqualTo(WithdrawBidError.BidNotFound));
    }

    [Test]
    public void CanBeWithdrawn_ActiveAuction_ShouldReturnTrue()
    {
        // Arrange
        var bid = new Bid
        {
            AuctionId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Amount = 100,
            PlacedAt = DateTime.UtcNow
        };

        // Act
        var canWithdraw = bid.CanBeWithdrawn(AuctionState.Active);

        // Assert
        Assert.That(canWithdraw, Is.True);
    }

    [Test]
    public void CanBeWithdrawn_EndedAuction_ShouldReturnFalse()
    {
        // Arrange
        var bid = new Bid
        {
            AuctionId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Amount = 100,
            PlacedAt = DateTime.UtcNow
        };

        // Act
        var canWithdraw = bid.CanBeWithdrawn(AuctionState.Ended);

        // Assert
        Assert.That(canWithdraw, Is.False);
    }

    [Test]
    public void CanBeWithdrawn_AlreadyWithdrawn_ShouldReturnFalse()
    {
        // Arrange
        var bid = new Bid
        {
            AuctionId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Amount = 100,
            PlacedAt = DateTime.UtcNow
        };
        bid.Withdraw();

        // Act
        var canWithdraw = bid.CanBeWithdrawn(AuctionState.Active);

        // Assert
        Assert.That(canWithdraw, Is.False);
    }

    private WithdrawBidCommandHandler CreateHandler()
    {
        var validators = new List<IWithdrawBidValidator>
        {
            new BidOwnershipValidator(),
            new BidNotAlreadyWithdrawnValidator(),
            new AuctionActiveForWithdrawalValidator()
        };

        return new WithdrawBidCommandHandler(_bidsRepository, _auctionsRepository, _timeProvider, validators);
    }

    private async Task<Auction> CreateActiveAuction()
    {
        var auction = await _auctionsRepository.CreateAuction(new Auction
        {
            Title = "Test Auction",
            EndTime = _timeProvider.Now().AddHours(1),
            Type = AuctionType.Open,
            MinimumIncrement = 10,
            MinPrice = 100,
            TieBreakingPolicy = TieBreakingPolicy.Earliest
        });
        auction.TransitionToActive();
        await _auctionsRepository.UpdateAuction(auction);
        return auction;
    }

    private async Task<Auction> CreateEndedAuction()
    {
        var auction = await _auctionsRepository.CreateAuction(new Auction
        {
            Title = "Test Auction",
            EndTime = _timeProvider.Now().AddMinutes(-5),
            Type = AuctionType.Open,
            MinimumIncrement = 10,
            MinPrice = 100,
            TieBreakingPolicy = TieBreakingPolicy.Earliest
        });
        auction.TransitionToActive();
        auction.TransitionToEnded();
        await _auctionsRepository.UpdateAuction(auction);
        return auction;
    }

    private async Task<Bid> PlaceBid(Guid auctionId, Guid userId, decimal amount)
    {
        return await _bidsRepository.CreateBid(new Bid
        {
            AuctionId = auctionId,
            UserId = userId,
            Amount = amount,
            PlacedAt = _timeProvider.Now()
        });
    }
}