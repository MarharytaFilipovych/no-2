using Application.Api.Auctions;
using Application.Commands.Auctions;
using Application.Configs;
using Domain.Auctions;
using Infrastructure.InMemory;
using Tests.Utils;

namespace Tests.Domain;

[TestFixture]
public class FinalizationAndTieBreakingTests
{
    private IAuctionsRepository _auctionsRepository = null!;
    private IBidsRepository _bidsRepository = null!;
    private TestTimeProvider _timeProvider = null!;
    private WinnerSelectionService _winnerSelectionService = null!;
    private IPaymentWindowConfig _paymentConfig = null!;

    [SetUp]
    public void SetUp()
    {
        _auctionsRepository = new AuctionsRepository();
        _bidsRepository = new BidsRepository();
        _timeProvider = new TestTimeProvider();
        _winnerSelectionService = new WinnerSelectionService();

        _paymentConfig = new TestPaymentWindowConfig
        {
            PaymentDeadline = TimeSpan.FromHours(3),
            BanDurationDays = 7
        };
    }

    [Test]
    public async Task FinalizeAuction_WithSingleValidBid_ShouldSelectWinner()
    {
        // Arrange
        var auction = await CreateEndedAuction(minPrice: 100);
        var userId = Guid.NewGuid();
        await PlaceBid(auction.Id, userId, 150);
        var handler = CreateHandler();
        var command = new FinalizeAuctionCommand { AuctionId = auction.Id };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsOk, Is.True);
        Assert.That(result.WinnerId, Is.EqualTo(userId));
        Assert.That(result.WinningAmount, Is.EqualTo(150));

        var finalizedAuction = await _auctionsRepository.GetAuction(auction.Id);
        Assert.That(finalizedAuction!.State, Is.EqualTo(AuctionState.Finalized));
        Assert.That(finalizedAuction.WinnerId, Is.EqualTo(userId));
        Assert.That(finalizedAuction.WinningBidAmount, Is.EqualTo(150));
    }

    [Test]
    public async Task FinalizeAuction_WithMultipleBids_ShouldSelectHighest()
    {
        // Arrange
        var auction = await CreateEndedAuction(minPrice: 100);
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var userId3 = Guid.NewGuid();
        await PlaceBid(auction.Id, userId1, 150);
        await PlaceBid(auction.Id, userId2, 200);
        await PlaceBid(auction.Id, userId3, 175);
        var handler = CreateHandler();
        var command = new FinalizeAuctionCommand { AuctionId = auction.Id };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsOk, Is.True);
        Assert.That(result.WinnerId, Is.EqualTo(userId2));
        Assert.That(result.WinningAmount, Is.EqualTo(200));
    }

    [Test]
    public async Task FinalizeAuction_AllBidsBelowMinPrice_ShouldHaveNoWinner()
    {
        // Arrange
        var auction = await CreateEndedAuction(minPrice: 500);
        await PlaceBid(auction.Id, Guid.NewGuid(), 150);
        await PlaceBid(auction.Id, Guid.NewGuid(), 200);
        await PlaceBid(auction.Id, Guid.NewGuid(), 300);
        var handler = CreateHandler();
        var command = new FinalizeAuctionCommand { AuctionId = auction.Id };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsOk, Is.True);
        Assert.That(result.WinnerId, Is.Null);
        Assert.That(result.WinningAmount, Is.Null);

        var finalizedAuction = await _auctionsRepository.GetAuction(auction.Id);
        Assert.That(finalizedAuction!.State, Is.EqualTo(AuctionState.Finalized));
        Assert.That(finalizedAuction.WinnerId, Is.Null);
        Assert.That(finalizedAuction.WinningBidAmount, Is.Null);
    }

    [Test]
    public async Task FinalizeAuction_NoBids_ShouldHaveNoWinner()
    {
        // Arrange
        var auction = await CreateEndedAuction(minPrice: 100);
        var handler = CreateHandler();
        var command = new FinalizeAuctionCommand { AuctionId = auction.Id };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsOk, Is.True);
        Assert.That(result.WinnerId, Is.Null);
        Assert.That(result.WinningAmount, Is.Null);
    }

    [Test]
    public async Task FinalizeAuction_WithWithdrawnBids_ShouldIgnoreThem()
    {
        // Arrange
        var auction = await CreateEndedAuction(minPrice: 100);
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        await PlaceBid(auction.Id, userId1, 150);
        var withdrawnBid = await PlaceBid(auction.Id, userId2, 250);
        withdrawnBid.Withdraw();
        await _bidsRepository.UpdateBid(withdrawnBid);
        var handler = CreateHandler();
        var command = new FinalizeAuctionCommand { AuctionId = auction.Id };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsOk, Is.True);
        Assert.That(result.WinnerId, Is.EqualTo(userId1));
        Assert.That(result.WinningAmount, Is.EqualTo(150));
    }

    [Test]
    public async Task FinalizeAuction_TiedBids_EarliestPolicy_ShouldSelectFirst()
    {
        // Arrange
        var auction = await CreateEndedAuction(
            minPrice: 100,
            tieBreaking: TieBreakingPolicy.Earliest);
        var time = _timeProvider.Now();
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var userId3 = Guid.NewGuid();
        await PlaceBid(auction.Id, userId1, 200, time.AddMinutes(0));
        await PlaceBid(auction.Id, userId2, 200, time.AddMinutes(1));
        await PlaceBid(auction.Id, userId3, 200, time.AddMinutes(2));
        var handler = CreateHandler();
        var command = new FinalizeAuctionCommand { AuctionId = auction.Id };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsOk, Is.True);
        Assert.That(result.WinnerId, Is.EqualTo(userId1));
        Assert.That(result.WinningAmount, Is.EqualTo(200));
    }

    [Test]
    public async Task FinalizeAuction_TiedBids_RandomPolicy_ShouldSelectOneOfTied()
    {
        // Arrange
        var auction = await CreateEndedAuction(
            minPrice: 100,
            tieBreaking: TieBreakingPolicy.RandomAmongEquals);
        var time = _timeProvider.Now();
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var userId3 = Guid.NewGuid();
        await PlaceBid(auction.Id, userId1, 200, time.AddMinutes(0));
        await PlaceBid(auction.Id, userId2, 200, time.AddMinutes(1));
        await PlaceBid(auction.Id, userId3, 200, time.AddMinutes(2));
        var handler = CreateHandler();
        var command = new FinalizeAuctionCommand { AuctionId = auction.Id };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsOk, Is.True);
        Assert.That(result.WinnerId, Is.Not.Null);
        Assert.That(result.WinningAmount, Is.EqualTo(200));
        Assert.That(
            result.WinnerId == userId1 || result.WinnerId == userId2 || result.WinnerId == userId3,
            Is.True);
    }

    [Test]
    public async Task FinalizeAuction_TiedBids_OnlyHighestTiedAreConsidered()
    {
        // Arrange
        var auction = await CreateEndedAuction(
            minPrice: 100,
            tieBreaking: TieBreakingPolicy.Earliest);
        var time = _timeProvider.Now();
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var userId3 = Guid.NewGuid();
        var userId4 = Guid.NewGuid();
        await PlaceBid(auction.Id, userId1, 150, time.AddMinutes(0));
        await PlaceBid(auction.Id, userId2, 150, time.AddMinutes(1));
        await PlaceBid(auction.Id, userId3, 200, time.AddMinutes(2));
        await PlaceBid(auction.Id, userId4, 200, time.AddMinutes(3));
        var handler = CreateHandler();
        var command = new FinalizeAuctionCommand { AuctionId = auction.Id };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsOk, Is.True);
        Assert.That(result.WinnerId, Is.EqualTo(userId3));
        Assert.That(result.WinningAmount, Is.EqualTo(200));
    }

    [Test]
    public async Task FinalizeAuction_SomeBidsBelowMinPrice_ShouldOnlyConsiderEligible()
    {
        // Arrange
        var auction = await CreateEndedAuction(minPrice: 200);
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var userId3 = Guid.NewGuid();
        await PlaceBid(auction.Id, userId1, 150);
        await PlaceBid(auction.Id, userId2, 180);
        await PlaceBid(auction.Id, userId3, 250);
        var handler = CreateHandler();
        var command = new FinalizeAuctionCommand { AuctionId = auction.Id };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsOk, Is.True);
        Assert.That(result.WinnerId, Is.EqualTo(userId3));
        Assert.That(result.WinningAmount, Is.EqualTo(250));
    }

    [Test]
    public async Task FinalizeAuction_AuctionNotFound_ShouldFail()
    {
        // Arrange
        var handler = CreateHandler();
        var command = new FinalizeAuctionCommand { AuctionId = Guid.NewGuid() };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsError, Is.True);
        Assert.That(result.Result.GetError(), Is.EqualTo(FinalizeAuctionError.AuctionNotFound));
    }

    [Test]
    public async Task FinalizeAuction_AuctionNotEnded_ShouldFail()
    {
        // Arrange
        var auction = await CreateActiveAuction();
        var handler = CreateHandler();
        var command = new FinalizeAuctionCommand { AuctionId = auction.Id };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsError, Is.True);
        Assert.That(result.Result.GetError(), Is.EqualTo(FinalizeAuctionError.AuctionNotEnded));
    }

    [Test]
    public async Task FinalizeAuction_AlreadyFinalized_ShouldFail()
    {
        // Arrange
        var auction = await CreateEndedAuction(minPrice: 100);
        await PlaceBid(auction.Id, Guid.NewGuid(), 150);
        var handler = CreateHandler();
        var command = new FinalizeAuctionCommand { AuctionId = auction.Id };

        // First finalization
        await handler.Handle(command, CancellationToken.None);

        // Act - Try to finalize again
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsError, Is.True);
        Assert.That(result.Result.GetError(), Is.EqualTo(FinalizeAuctionError.AlreadyFinalized));
    }

    [Test]
    public async Task FinalizeAuction_AuctionPending_ShouldFail()
    {
        // Arrange
        var auction = await _auctionsRepository.CreateAuction(new Auction
        {
            Title = "Test auction",
            StartTime = _timeProvider.Now().AddHours(1),
            EndTime = _timeProvider.Now().AddHours(2),
            Type = AuctionType.Open,
            MinimumIncrement = 10,
            MinPrice = 100,
            TieBreakingPolicy = TieBreakingPolicy.Earliest
        });
        var handler = CreateHandler();
        var command = new FinalizeAuctionCommand { AuctionId = auction.Id };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsError, Is.True);
        Assert.That(result.Result.GetError(), Is.EqualTo(FinalizeAuctionError.AuctionNotEnded));
    }

    [Test]
    public async Task FinalizeAuction_BlindAuction_ShouldWorkCorrectly()
    {
        // Arrange
        var auction = await CreateEndedBlindAuction(minPrice: 100);
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var userId3 = Guid.NewGuid();
        await PlaceBid(auction.Id, userId1, 80);
        await PlaceBid(auction.Id, userId2, 250);
        await PlaceBid(auction.Id, userId3, 150);
        var handler = CreateHandler();
        var command = new FinalizeAuctionCommand { AuctionId = auction.Id };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsOk, Is.True);
        Assert.That(result.WinnerId, Is.EqualTo(userId2));
        Assert.That(result.WinningAmount, Is.EqualTo(250));
    }

    [Test]
    public async Task SelectWinner_DirectCall_TiedBidsEarliest_ShouldSelectFirst()
    {
        // Arrange
        var auction = new Auction
        {
            Title = "Test",
            EndTime = DateTime.UtcNow,
            MinPrice = 100,
            TieBreakingPolicy = TieBreakingPolicy.Earliest,
            Type = AuctionType.Open
        };
        var time = _timeProvider.Now();
        var bids = new List<Bid>
        {
            new()
            {
                Id = Guid.NewGuid(), AuctionId = auction.Id, UserId = Guid.NewGuid(), Amount = 200,
                PlacedAt = time.AddMinutes(2)
            },
            new()
            {
                Id = Guid.NewGuid(), AuctionId = auction.Id, UserId = Guid.NewGuid(), Amount = 200,
                PlacedAt = time.AddMinutes(0)
            },
            new()
            {
                Id = Guid.NewGuid(), AuctionId = auction.Id, UserId = Guid.NewGuid(), Amount = 200,
                PlacedAt = time.AddMinutes(1)
            }
        };

        // Act
        var winner = await _winnerSelectionService.SelectWinner(auction, bids);

        // Assert
        Assert.That(winner, Is.Not.Null);
        Assert.That(winner!.PlacedAt, Is.EqualTo(time.AddMinutes(0)));
    }

    [Test]
    public async Task SelectWinner_DirectCall_NoBids_ShouldReturnNull()
    {
        // Arrange
        var auction = new Auction
        {
            Title = "Test",
            EndTime = DateTime.UtcNow,
            MinPrice = 100,
            TieBreakingPolicy = TieBreakingPolicy.Earliest,
            Type = AuctionType.Open
        };

        // Act
        var winner = await _winnerSelectionService.SelectWinner(auction, new List<Bid>());

        // Assert
        Assert.That(winner, Is.Null);
    }

    [Test]
    public async Task SelectWinner_DirectCall_AllBidsBelowMinPrice_ShouldReturnNull()
    {
        // Arrange
        var auction = new Auction
        {
            Title = "Test",
            EndTime = DateTime.UtcNow,
            MinPrice = 500,
            TieBreakingPolicy = TieBreakingPolicy.Earliest,
            Type = AuctionType.Open
        };
        var bids = new List<Bid>
        {
            new()
            {
                Id = Guid.NewGuid(), AuctionId = auction.Id, UserId = Guid.NewGuid(), Amount = 200,
                PlacedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(), AuctionId = auction.Id, UserId = Guid.NewGuid(), Amount = 300,
                PlacedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(), AuctionId = auction.Id, UserId = Guid.NewGuid(), Amount = 400,
                PlacedAt = DateTime.UtcNow
            }
        };

        // Act
        var winner = await _winnerSelectionService.SelectWinner(auction, bids);

        // Assert
        Assert.That(winner, Is.Null);
    }

    private FinalizeAuctionCommandHandler CreateHandler()
    {
        return new FinalizeAuctionCommandHandler(
            _auctionsRepository,
            _bidsRepository,
            _timeProvider,
            _paymentConfig,
            _winnerSelectionService);
    }

    private async Task<Auction> CreateActiveAuction()
    {
        var auction = await _auctionsRepository.CreateAuction(new Auction
        {
            Title = "Test auction",
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

    private async Task<Auction> CreateEndedAuction(
        decimal minPrice = 100,
        TieBreakingPolicy tieBreaking = TieBreakingPolicy.Earliest)
    {
        var auction = await _auctionsRepository.CreateAuction(new Auction
        {
            Title = "Test auction",
            EndTime = _timeProvider.Now().AddMinutes(-5),
            Type = AuctionType.Open,
            MinimumIncrement = 10,
            MinPrice = minPrice,
            TieBreakingPolicy = tieBreaking
        });
        auction.TransitionToActive();
        auction.TransitionToEnded();
        await _auctionsRepository.UpdateAuction(auction);
        return auction;
    }

    private async Task<Auction> CreateEndedBlindAuction(decimal minPrice = 100)
    {
        var auction = await _auctionsRepository.CreateAuction(new Auction
        {
            Title = "Test auction",
            EndTime = _timeProvider.Now().AddMinutes(-5),
            Type = AuctionType.Blind,
            MinPrice = minPrice,
            TieBreakingPolicy = TieBreakingPolicy.Earliest
        });
        auction.TransitionToActive();
        auction.TransitionToEnded();
        await _auctionsRepository.UpdateAuction(auction);
        return auction;
    }

    private async Task<Bid> PlaceBid(
        Guid auctionId,
        Guid userId,
        decimal amount,
        DateTime? placedAt = null)
    {
        return await _bidsRepository.CreateBid(new Bid
        {
            AuctionId = auctionId,
            UserId = userId,
            Amount = amount,
            PlacedAt = placedAt ?? _timeProvider.Now()
        });
    }
}