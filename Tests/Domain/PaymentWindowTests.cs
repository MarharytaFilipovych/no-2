using Domain.Auctions;
using Application.Api.Auctions;
using Application.Commands.Auctions;
using Infrastructure.InMemory;
using Tests.Utils;

namespace Tests.Domain;

[TestFixture]
public class PaymentWindowTests
{
    private IAuctionsRepository _auctionsRepository = null!;
    private IBidsRepository _bidsRepository = null!;
    private IParticipantBalanceRepository _balanceRepository = null!;
    private Application.Api.Users.IUsersRepository _usersRepository = null!;
    private TestTimeProvider _timeProvider = null!;
    private TestPaymentWindowConfig _paymentConfig = null!;
    private WinnerSelectionService _winnerSelectionService = null!;

    [SetUp]
    public void SetUp()
    {
        _auctionsRepository = new AuctionsRepository();
        _bidsRepository = new BidsRepository();
        _balanceRepository = new ParticipantBalanceRepository();
        _usersRepository = new UserRepository();
        _timeProvider = new TestTimeProvider();
        _paymentConfig = new TestPaymentWindowConfig
        {
            PaymentDeadline = TimeSpan.FromHours(3),
            BanDurationDays = 7
        };
        _winnerSelectionService = new WinnerSelectionService();
    }

    [Test]
    public async Task FinalizeAuction_ShouldSetProvisionalWinner()
    {
        // Arrange
        var auction = await CreateEndedAuction(minPrice: 100);
        var userId = Guid.NewGuid();
        await PlaceBid(auction.Id, userId, 150);
        var handler = CreateFinalizeHandler();
        var command = new FinalizeAuctionCommand { AuctionId = auction.Id };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsOk, Is.True);
        var updatedAuction = await _auctionsRepository.GetAuction(auction.Id);
        Assert.That(updatedAuction!.ProvisionalWinnerId, Is.EqualTo(userId));
        Assert.That(updatedAuction.ProvisionalWinningAmount, Is.EqualTo(150));
        Assert.That(updatedAuction.PaymentDeadline, Is.Not.Null);
        Assert.That(updatedAuction.IsPaymentConfirmed, Is.False);
    }

    [Test]
    public async Task ConfirmPayment_WithSufficientBalance_ShouldSucceed()
    {
        // Arrange
        var auction = await CreateFinalizedAuctionWithProvisionalWinner(100, 150);
        await _balanceRepository.DepositFunds(auction.ProvisionalWinnerId!.Value, 200);
        var handler = CreateConfirmPaymentHandler();
        var command = new ConfirmPaymentCommand { AuctionId = auction.Id };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsOk, Is.True);
        Assert.That(result.PaymentConfirmed, Is.True);

        var updatedAuction = await _auctionsRepository.GetAuction(auction.Id);
        Assert.That(updatedAuction!.IsPaymentConfirmed, Is.True);
        Assert.That(updatedAuction.WinnerId, Is.EqualTo(auction.ProvisionalWinnerId));
    }

    [Test]
    public async Task ConfirmPayment_WithInsufficientBalance_ShouldFail()
    {
        // Arrange
        var auction = await CreateFinalizedAuctionWithProvisionalWinner(100, 150);
        await _balanceRepository.DepositFunds(auction.ProvisionalWinnerId!.Value, 50);
        var handler = CreateConfirmPaymentHandler();
        var command = new ConfirmPaymentCommand { AuctionId = auction.Id };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsError, Is.True);
        Assert.That(result.Result.GetError(), Is.EqualTo(ConfirmPaymentError.InsufficientBalance));
        Assert.That(result.PaymentConfirmed, Is.False);
    }

    [Test]
    public async Task ProcessDeadline_AfterDeadlineWithInsufficientFunds_ShouldPromoteNextBid()
    {
        // Arrange
        var auction = await CreateFinalizedAuctionWithProvisionalWinner(100, 150);
        var user2 = Guid.NewGuid();
        await PlaceBid(auction.Id, user2, 140);

        await _balanceRepository.DepositFunds(auction.ProvisionalWinnerId!.Value, 50);
        await _balanceRepository.DepositFunds(user2, 200);

        _timeProvider.AddTime(TimeSpan.FromHours(4));

        var handler = CreateProcessDeadlineHandler();
        var command = new ProcessPaymentDeadlineCommand { AuctionId = auction.Id };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsOk, Is.True);
        Assert.That(result.NewWinnerId, Is.EqualTo(user2));
        Assert.That(result.AllBidsExhausted, Is.False);

        var updatedAuction = await _auctionsRepository.GetAuction(auction.Id);
        Assert.That(updatedAuction!.ProvisionalWinnerId, Is.EqualTo(user2));
    }

    [Test]
    public async Task ProcessDeadline_ShouldBanRejectedUser()
    {
        // Arrange
        var rejectedUserId = Guid.NewGuid();
        var user = await _usersRepository.CreateUser("rejected@test.com", "hash");

        var auction = await CreateFinalizedAuctionWithProvisionalWinner(100, 150, user!.UserId);
        await _balanceRepository.DepositFunds(user.UserId, 50);

        _timeProvider.AddTime(TimeSpan.FromHours(4));

        var handler = CreateProcessDeadlineHandler();
        var command = new ProcessPaymentDeadlineCommand { AuctionId = auction.Id };

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var bannedUser = await _usersRepository.GetUser(user.UserId);
        Assert.That(bannedUser, Is.Not.Null);
        Assert.That(bannedUser!.BannedUntil, Is.Not.Null);
        Assert.That(bannedUser.IsBanned(_timeProvider.Now()), Is.True);

        var expectedBanUntil = _timeProvider.Now().AddDays(_paymentConfig.BanDurationDays);
        Assert.That(bannedUser.BannedUntil!.Value.Date, Is.EqualTo(expectedBanUntil.Date));
    }

    [Test]
    public async Task ProcessDeadline_AllBidsInsufficientFunds_ShouldExhaustBids()
    {
        // Arrange
        var user1 = Guid.NewGuid();
        var auction = await CreateFinalizedAuctionWithProvisionalWinner(100, 150, user1);

        await _balanceRepository.DepositFunds(user1, 50);

        _timeProvider.AddTime(TimeSpan.FromHours(4));

        var handler = CreateProcessDeadlineHandler();

        // Act
        var result = await handler.Handle(
            new ProcessPaymentDeadlineCommand { AuctionId = auction.Id },
            CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsOk, Is.True);
        Assert.That(result.AllBidsExhausted, Is.True);
        Assert.That(result.NewWinnerId, Is.Null);

        var finalAuction = await _auctionsRepository.GetAuction(auction.Id);
        Assert.That(finalAuction!.State, Is.EqualTo(AuctionState.Finalized));
        Assert.That(finalAuction.WinnerId, Is.Null);
        Assert.That(finalAuction.WinningBidAmount, Is.Null);
    }

    [Test]
    public async Task ProcessDeadline_BeforeDeadline_ShouldFail()
    {
        // Arrange
        var auction = await CreateFinalizedAuctionWithProvisionalWinner(100, 150);
        await _balanceRepository.DepositFunds(auction.ProvisionalWinnerId!.Value, 50);

        var handler = CreateProcessDeadlineHandler();
        var command = new ProcessPaymentDeadlineCommand { AuctionId = auction.Id };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsError, Is.True);
        Assert.That(result.Result.GetError(), Is.EqualTo(ProcessPaymentError.DeadlineNotPassed));
    }

    [Test]
    public async Task ProcessDeadline_WinnerPaysBeforeDeadline_ShouldConfirmPayment()
    {
        // Arrange
        var auction = await CreateFinalizedAuctionWithProvisionalWinner(100, 150);
        await _balanceRepository.DepositFunds(auction.ProvisionalWinnerId!.Value, 200);

        _timeProvider.AddTime(TimeSpan.FromHours(4));

        var handler = CreateProcessDeadlineHandler();
        var command = new ProcessPaymentDeadlineCommand { AuctionId = auction.Id };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsOk, Is.True);
        var finalAuction = await _auctionsRepository.GetAuction(auction.Id);
        Assert.That(finalAuction!.IsPaymentConfirmed, Is.True);
        Assert.That(finalAuction.WinnerId, Is.EqualTo(auction.ProvisionalWinnerId));
    }

    [Test]
    public async Task Auction_HasProvisionalWinner_ShouldReturnTrue()
    {
        // Arrange
        var auction = await CreateFinalizedAuctionWithProvisionalWinner(100, 150);

        // Assert
        Assert.That(auction.HasProvisionalWinner(), Is.True);
    }

    [Test]
    public async Task Auction_AfterPaymentConfirmed_ShouldNotHaveProvisionalWinner()
    {
        // Arrange
        var auction = await CreateFinalizedAuctionWithProvisionalWinner(100, 150);
        auction.ConfirmPayment();

        // Assert
        Assert.That(auction.HasProvisionalWinner(), Is.False);
        Assert.That(auction.IsPaymentConfirmed, Is.True);
    }

    [Test]
    public void Auction_SetProvisionalWinner_NotFinalized_ShouldThrow()
    {
        // Arrange
        var auction = new Auction
        {
            Title = "Test",
            EndTime = DateTime.UtcNow.AddHours(1),
            MinPrice = 100,
            Type = AuctionType.Open
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            auction.SetProvisionalWinner(Guid.NewGuid(), 150, DateTime.UtcNow.AddHours(3)));
    }

    private FinalizeAuctionCommandHandler CreateFinalizeHandler()
    {
        return new FinalizeAuctionCommandHandler(
            _auctionsRepository,
            _bidsRepository,
            _timeProvider,
            _paymentConfig,
            _winnerSelectionService);
    }

    private ConfirmPaymentCommandHandler CreateConfirmPaymentHandler()
    {
        return new ConfirmPaymentCommandHandler(
            _auctionsRepository,
            _balanceRepository,
            _timeProvider);
    }

    private ProcessPaymentDeadlineCommandHandler CreateProcessDeadlineHandler()
    {
        return new ProcessPaymentDeadlineCommandHandler(
            _auctionsRepository,
            _bidsRepository,
            _balanceRepository,
            _usersRepository,
            _timeProvider,
            _paymentConfig,
            _winnerSelectionService);
    }

    private async Task<Auction> CreateEndedAuction(decimal minPrice = 100)
    {
        var auction = await _auctionsRepository.CreateAuction(new Auction
        {
            Title = "Test auction",
            EndTime = _timeProvider.Now().AddMinutes(-5),
            Type = AuctionType.Open,
            MinimumIncrement = 10,
            MinPrice = minPrice,
            TieBreakingPolicy = TieBreakingPolicy.Earliest
        });
        auction.TransitionToActive();
        auction.TransitionToEnded();
        await _auctionsRepository.UpdateAuction(auction);
        return auction;
    }

    private async Task<Auction> CreateFinalizedAuctionWithProvisionalWinner(
        decimal minPrice,
        decimal winningBid,
        Guid? specificUserId = null)
    {
        var auction = await CreateEndedAuction(minPrice);
        var userId = specificUserId ?? Guid.NewGuid();
        await PlaceBid(auction.Id, userId, winningBid);

        auction.Finalize(null, null);
        var deadline = _timeProvider.Now().Add(_paymentConfig.PaymentDeadline);
        auction.SetProvisionalWinner(userId, winningBid, deadline);
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