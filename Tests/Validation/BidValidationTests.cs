using Application.Api.Auctions;
using Application.Commands.Auctions;
using Domain.Auctions;
using Infrastructure.InMemory;
using Tests.Utils;

namespace Tests.Validation;

[TestFixture]
public class BidValidationTests
{
    private IAuctionsRepository _auctionsRepository= null!;
    private IBidsRepository _bidsRepository= null!;
    private TestTimeProvider _timeProvider = null!;

    [SetUp]
    public void SetUp()
    {
        _auctionsRepository = new AuctionsRepository();
        _bidsRepository = new BidsRepository();
        _timeProvider = new TestTimeProvider();
    }

    [Test]
    public async Task PlaceBid_OnOpenAuction_WhenBidTooLow_ShouldFail()
    {
        // Arrange
        var auction = await CreateActiveOpenAuction(minPrice: 100, increment: 10);
        var userId2 = Guid.NewGuid();
        await PlaceInitialBid(auction.Id, userId: Guid.NewGuid(), amount: 120);
        var handler = new PlaceBidCommandHandler(_auctionsRepository, _bidsRepository, _timeProvider);
        var command = new PlaceBidCommand
        {
            AuctionId = auction.Id,
            UserId = userId2,
            Amount = 125
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsError, Is.True);
        Assert.That(result.Result.GetError(), Is.EqualTo(PlaceBidError.BidTooLow));
    }

    [Test]
    public async Task PlaceBid_OnOpenAuction_WhenBidMeetsIncrement_ShouldSucceed()
    {
        // Arrange
        var auction = await CreateActiveOpenAuction(minPrice: 100, increment: 10);
        var userId2 = Guid.NewGuid();
        await PlaceInitialBid(auction.Id, userId: Guid.NewGuid(), amount: 120);
        var handler = new PlaceBidCommandHandler(_auctionsRepository, _bidsRepository, _timeProvider);
        var command = new PlaceBidCommand
        {
            AuctionId = auction.Id,
            UserId = userId2,
            Amount = 130
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsOk, Is.True);
    }

    [Test]
    public async Task PlaceBid_OnOpenAuction_FirstBid_MustMeetMinPrice()
    {
        // Arrange
        var auction = await CreateActiveOpenAuction(minPrice: 100, increment: 10);
        var handler = new PlaceBidCommandHandler(_auctionsRepository, _bidsRepository, _timeProvider);
        var command = new PlaceBidCommand
        {
            AuctionId = auction.Id,
            UserId = Guid.NewGuid(),
            Amount = 105
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsError, Is.True);
        Assert.That(result.Result.GetError(), Is.EqualTo(PlaceBidError.BidTooLow));
    }

    [Test]
    public async Task PlaceBid_OnOpenAuction_MultipleBidsFromSameUser_ShouldSucceed()
    {
        // Arrange
        var auction = await CreateActiveOpenAuction(minPrice: 100, increment: 10);
        var handler = new PlaceBidCommandHandler(_auctionsRepository, _bidsRepository, _timeProvider);
        var userId = Guid.NewGuid();
        var firstBid = new PlaceBidCommand
        {
            AuctionId = auction.Id,
            UserId = userId,
            Amount = 110
        };
        await handler.Handle(firstBid, CancellationToken.None);
        var secondBid = new PlaceBidCommand
        {
            AuctionId = auction.Id,
            UserId = userId,
            Amount = 130
        };

        // Act
        var result = await handler.Handle(secondBid, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsOk, Is.True);
    }

    [Test]
    public async Task PlaceBid_OnBlindAuction_AnyAmountAccepted()
    {
        // Arrange
        var auction = await CreateActiveBlindAuction(minPrice: 100);
        var handler = new PlaceBidCommandHandler(_auctionsRepository, _bidsRepository, _timeProvider);
        var command = new PlaceBidCommand
        {
            AuctionId = auction.Id,
            UserId = Guid.NewGuid(),
            Amount = 50
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsOk, Is.True);
    }

    [Test]
    public async Task PlaceBid_OnBlindAuction_SecondBidFromSameUser_ShouldFail()
    {
        // Arrange
        var auction = await CreateActiveBlindAuction(minPrice: 100);
        var userId = Guid.NewGuid();
        await PlaceInitialBid(auction.Id, userId: userId, amount: 120);
        var handler = new PlaceBidCommandHandler(_auctionsRepository, _bidsRepository, _timeProvider);
        var command = new PlaceBidCommand
        {
            AuctionId = auction.Id,
            UserId = userId,
            Amount = 150
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsError, Is.True);
        Assert.That(result.Result.GetError(), Is.EqualTo(PlaceBidError.UserAlreadyBid));
    }

    [Test]
    public async Task PlaceBid_OnBlindAuction_AfterWithdrawingPreviousBid_ShouldSucceed()
    {
        // Arrange
        var auction = await CreateActiveBlindAuction(minPrice: 100);
        var userId = Guid.NewGuid();
        var firstBid = await PlaceInitialBid(auction.Id, userId: userId, amount: 120);
        firstBid.Withdraw();
        await _bidsRepository.UpdateBid(firstBid);
        var handler = new PlaceBidCommandHandler(_auctionsRepository, _bidsRepository, _timeProvider);
        var command = new PlaceBidCommand
        {
            AuctionId = auction.Id,
            UserId = userId,
            Amount = 150
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsOk, Is.True);
    }

    [Test]
    public async Task PlaceBid_WhenAuctionNotActive_ShouldFail()
    {
        // Arrange
        var auction = await _auctionsRepository.CreateAuction(new Auction
        {
            Title = "Test Auction",
            EndTime = _timeProvider.Now().AddHours(1),
            Type = AuctionType.Open,
            MinimumIncrement = 10,
            MinPrice = 100,
            TieBreakingPolicy = TieBreakingPolicy.Earliest
        });
        var handler = new PlaceBidCommandHandler(_auctionsRepository, _bidsRepository, _timeProvider);
        var command = new PlaceBidCommand
        {
            AuctionId = auction.Id,
            UserId = Guid.NewGuid(),
            Amount = 120
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsError, Is.True);
        Assert.That(result.Result.GetError(), Is.EqualTo(PlaceBidError.AuctionNotActive));
    }

    [Test]
    public async Task PlaceBid_WhenAuctionDoesNotExist_ShouldFail()
    {
        // Arrange
        var handler = new PlaceBidCommandHandler(_auctionsRepository, _bidsRepository, _timeProvider);
        var command = new PlaceBidCommand
        {
            AuctionId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Amount = 120
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsError, Is.True);
        Assert.That(result.Result.GetError(), Is.EqualTo(PlaceBidError.AuctionNotFound));
    }

    [Test]
    public async Task PlaceBid_WithSoftClose_ShouldExtendAuction()
    {
        // Arrange
        var originalEndTime = _timeProvider.Now().AddMinutes(3);
        var auction = await _auctionsRepository.CreateAuction(new Auction
        {
            Title = "Test Auction",
            EndTime = originalEndTime,
            Type = AuctionType.Open,
            MinimumIncrement = 10,
            MinPrice = 100,
            SoftCloseWindow = TimeSpan.FromMinutes(5),
            TieBreakingPolicy = TieBreakingPolicy.Earliest
        });
        auction.TransitionToActive();
        await _auctionsRepository.UpdateAuction(auction);
        var handler = new PlaceBidCommandHandler(_auctionsRepository, _bidsRepository, _timeProvider);
        var command = new PlaceBidCommand
        {
            AuctionId = auction.Id,
            UserId = Guid.NewGuid(),
            Amount = 110
        };

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var updatedAuction = await _auctionsRepository.GetAuction(auction.Id);
        Assert.NotNull(updatedAuction);
        Assert.That(updatedAuction!.EndTime, Is.GreaterThan(originalEndTime));
    }

    private async Task<Auction> CreateActiveOpenAuction(decimal minPrice, decimal increment)
    {
        var auction = await _auctionsRepository.CreateAuction(new Auction
        {
            Title = "Test Auction",
            EndTime = _timeProvider.Now().AddHours(1),
            Type = AuctionType.Open,
            MinimumIncrement = increment,
            MinPrice = minPrice,
            TieBreakingPolicy = TieBreakingPolicy.Earliest
        });
        auction.TransitionToActive();
        await _auctionsRepository.UpdateAuction(auction);
        return auction;
    }

    private async Task<Auction> CreateActiveBlindAuction(decimal minPrice)
    {
        var auction = await _auctionsRepository.CreateAuction(new Auction
        {
            Title = "Test Auction",
            EndTime = _timeProvider.Now().AddHours(1),
            Type = AuctionType.Blind,
            MinPrice = minPrice,
            TieBreakingPolicy = TieBreakingPolicy.Earliest
        });
        auction.TransitionToActive();
        await _auctionsRepository.UpdateAuction(auction);
        return auction;
    }

    private async Task<Bid> PlaceInitialBid(Guid auctionId, Guid userId, decimal amount)
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