using Application.Api.Auctions;
using Application.Commands.Auctions;
using Application.Configs;
using Domain.Auctions;
using Infrastructure.InMemory;
using Tests.Utils;

namespace Tests.Validation;

[TestFixture]
public class BidValidationTests
{
    private IAuctionsRepository _auctionsRepository = null!;
    private IBidsRepository _bidsRepository = null!;
    private IParticipantBalanceRepository _balanceRepository = null!;
    private TestTimeProvider _timeProvider = null!;
    private TestBiddingConfig _biddingConfig = null!;

    [SetUp]
    public void SetUp()
    {
        _auctionsRepository = new AuctionsRepository();
        _bidsRepository = new BidsRepository();
        _balanceRepository = new ParticipantBalanceRepository();
        _timeProvider = new TestTimeProvider();
        _biddingConfig = new TestBiddingConfig
        {
            MaxBidAmount = 1000000,
            BalanceRatioLimit = 0.5m
        };
    }
    
    [Test]
    public async Task PlaceBid_OnOpenAuction_WhenBidTooLow_ShouldFail()
    {
        // Arrange
        var auction = await CreateActiveOpenAuction(minPrice: 100, increment: 10);
        var userId2 = Guid.NewGuid();
        await SetupUserBalance(userId2, 10000);
        await PlaceInitialBid(auction.Id, userId: Guid.NewGuid(), amount: 120);
        var handler = CreateHandler();
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
        await SetupUserBalance(userId2, 10000);
        await PlaceInitialBid(auction.Id, userId: Guid.NewGuid(), amount: 120);
        var handler = CreateHandler();
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
        var userId = Guid.NewGuid();
        await SetupUserBalance(userId, 10000);
        var handler = CreateHandler();
        var command = new PlaceBidCommand
        {
            AuctionId = auction.Id,
            UserId = userId,
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
        var handler = CreateHandler();
        var userId = Guid.NewGuid();
        await SetupUserBalance(userId, 10000);
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
        var userId = Guid.NewGuid();
        await SetupUserBalance(userId, 10000);
        var handler = CreateHandler();
        var command = new PlaceBidCommand
        {
            AuctionId = auction.Id,
            UserId = userId,
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
        await SetupUserBalance(userId, 10000);
        await PlaceInitialBid(auction.Id, userId: userId, amount: 120);
        var handler = CreateHandler();
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
        await SetupUserBalance(userId, 10000);
        var firstBid = await PlaceInitialBid(auction.Id, userId: userId, amount: 120);
        firstBid.Withdraw();
        await _bidsRepository.UpdateBid(firstBid);
        var handler = CreateHandler();
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
        var userId = Guid.NewGuid();
        await SetupUserBalance(userId, 10000);
        var handler = CreateHandler();
        var command = new PlaceBidCommand
        {
            AuctionId = auction.Id,
            UserId = userId,
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
        var userId = Guid.NewGuid();
        await SetupUserBalance(userId, 10000);
        var handler = CreateHandler();
        var command = new PlaceBidCommand
        {
            AuctionId = Guid.NewGuid(),
            UserId = userId,
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
        var userId = Guid.NewGuid();
        await SetupUserBalance(userId, 10000);
        var handler = CreateHandler();
        var command = new PlaceBidCommand
        {
            AuctionId = auction.Id,
            UserId = userId,
            Amount = 110
        };

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var updatedAuction = await _auctionsRepository.GetAuction(auction.Id);
        Assert.NotNull(updatedAuction);
        Assert.That(updatedAuction!.EndTime, Is.GreaterThan(originalEndTime));
    }
    
    [Test]
    public async Task PlaceBid_ExceedsMaxBidAmount_ShouldFail()
    {
        // Arrange
        var auction = await CreateActiveOpenAuction(minPrice: 100, increment: 10);
        var userId = Guid.NewGuid();
        await SetupUserBalance(userId, 5000000);
        var handler = CreateHandler();
        var command = new PlaceBidCommand
        {
            AuctionId = auction.Id,
            UserId = userId,
            Amount = 1500000
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsError, Is.True);
        Assert.That(result.Result.GetError(), Is.EqualTo(PlaceBidError.ExceedsMaxBidAmount));
    }

    [Test]
    public async Task PlaceBid_WithinMaxBidAmount_ShouldSucceed()
    {
        // Arrange
        var auction = await CreateActiveOpenAuction(minPrice: 100, increment: 10);
        var userId = Guid.NewGuid();
        await SetupUserBalance(userId, 2000000);
        var handler = CreateHandler();
        var command = new PlaceBidCommand
        {
            AuctionId = auction.Id,
            UserId = userId,
            Amount = 900000
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsOk, Is.True);
    }

    [Test]
    public async Task PlaceBid_ExceedsBalanceRatio_ShouldFail()
    {
        // Arrange
        var auction = await CreateActiveOpenAuction(minPrice: 100, increment: 10);
        var userId = Guid.NewGuid();
        await SetupUserBalance(userId, 1000);
        var handler = CreateHandler();
        var command = new PlaceBidCommand
        {
            AuctionId = auction.Id,
            UserId = userId,
            Amount = 600
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsError, Is.True);
        Assert.That(result.Result.GetError(), Is.EqualTo(PlaceBidError.ExceedsBalanceLimit));
    }

    [Test]
    public async Task PlaceBid_WithinBalanceRatio_ShouldSucceed()
    {
        // Arrange
        var auction = await CreateActiveOpenAuction(minPrice: 100, increment: 10);
        var userId = Guid.NewGuid();
        await SetupUserBalance(userId, 1000);
        var handler = CreateHandler();
        var command = new PlaceBidCommand
        {
            AuctionId = auction.Id,
            UserId = userId,
            Amount = 400
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsOk, Is.True);
    }

    [Test]
    public async Task PlaceBid_InsufficientBalance_ShouldFail()
    {
        // Arrange
        var auction = await CreateActiveOpenAuction(minPrice: 100, increment: 10);
        var userId = Guid.NewGuid();
        await SetupUserBalance(userId, 200);
        var handler = CreateHandler();
        var command = new PlaceBidCommand
        {
            AuctionId = auction.Id,
            UserId = userId,
            Amount = 300
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsError, Is.True);
        Assert.That(result.Result.GetError(), Is.EqualTo(PlaceBidError.ExceedsBalanceLimit));
    }

    [Test]
    public async Task PlaceBid_MaxAllowedByBalanceRatio_ShouldSucceed()
    {
        // Arrange
        var auction = await CreateActiveOpenAuction(minPrice: 100, increment: 10);
        var userId = Guid.NewGuid();
        await SetupUserBalance(userId, 1000);
        var handler = CreateHandler();
        var command = new PlaceBidCommand
        {
            AuctionId = auction.Id,
            UserId = userId,
            Amount = 500
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsOk, Is.True);
    }

    [Test]
    public async Task PlaceBid_BalanceRatioLimit200Percent_ShouldAllowDoubleBid()
    {
        // Arrange
        var customConfig = new TestBiddingConfig
        {
            MaxBidAmount = 1000000,
            BalanceRatioLimit = 2.0m
        };
        var auction = await CreateActiveBlindAuction(minPrice: 100);
        var userId = Guid.NewGuid();
        await SetupUserBalance(userId, 500);
        var handler = new PlaceBidCommandHandler(
            _auctionsRepository, 
            _bidsRepository, 
            _timeProvider, 
            customConfig, 
            _balanceRepository);
        var command = new PlaceBidCommand
        {
            AuctionId = auction.Id,
            UserId = userId,
            Amount = 900
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsOk, Is.True);
    }

    [Test]
    public async Task PlaceBid_LargeBalanceButMaxLimit_ShouldEnforceMaxLimit()
    {
        // Arrange
        var auction = await CreateActiveOpenAuction(minPrice: 100, increment: 10);
        var userId = Guid.NewGuid();
        await SetupUserBalance(userId, 10000000);
        var handler = CreateHandler();
        var command = new PlaceBidCommand
        {
            AuctionId = auction.Id,
            UserId = userId,
            Amount = 1000001
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsError, Is.True);
        Assert.That(result.Result.GetError(), Is.EqualTo(PlaceBidError.ExceedsMaxBidAmount));
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

    private async Task SetupUserBalance(Guid userId, decimal amount)
    {
        await _balanceRepository.DepositFunds(userId, amount);
    }

    private PlaceBidCommandHandler CreateHandler() =>
        new(_auctionsRepository, _bidsRepository, _timeProvider, _biddingConfig, _balanceRepository);

}

public class TestBiddingConfig : IBiddingConfig
{
    public decimal MaxBidAmount { get; set; }
    public decimal BalanceRatioLimit { get; set; }
}