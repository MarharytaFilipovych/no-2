using Domain.Auctions;
using Domain.Users;
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
    private IAuctionCycleRepository _cycleRepository = null!;
    private NoRepeatWinnerPolicy _noRepeatWinnerPolicy = null!;
    private PaymentProcessingService _paymentProcessingService = null!;
    private BanPolicy _banPolicy = null!;

    [SetUp]
    public void SetUp()
    {
        _auctionsRepository = new AuctionsRepository();
        _bidsRepository = new BidsRepository();
        _balanceRepository = new ParticipantBalanceRepository();
        _usersRepository = new UserRepository();
        _cycleRepository = new AuctionCycleRepository();
        _timeProvider = new TestTimeProvider();
        _paymentConfig = new TestPaymentWindowConfig
        {
            PaymentDeadline = TimeSpan.FromHours(3),
            BanDurationDays = 7
        };
        _winnerSelectionService = new WinnerSelectionService();
        _noRepeatWinnerPolicy = new NoRepeatWinnerPolicy();
        _paymentProcessingService = new PaymentProcessingService();
        _banPolicy = new BanPolicy();
    }

    [Test]
    public async Task FinalizeAuction_ShouldSetProvisionalWinner()
    {
        var auction = await CreateEndedAuction(minPrice: 100);
        var userId = Guid.NewGuid();
        await PlaceBid(auction.Id, userId, 150);
        var handler = CreateFinalizeHandler();
        var command = new FinalizeAuctionCommand { AuctionId = auction.Id };

        var result = await handler.Handle(command, CancellationToken.None);

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
        var auction = await CreateFinalizedAuctionWithProvisionalWinner(100, 150);
        await _balanceRepository.DepositFunds(auction.ProvisionalWinnerId!.Value, 200);
        var handler = CreateConfirmPaymentHandler();
        var command = new ConfirmPaymentCommand { AuctionId = auction.Id };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.That(result.Result.IsOk, Is.True);
        Assert.That(result.PaymentConfirmed, Is.True);

        var updatedAuction = await _auctionsRepository.GetAuction(auction.Id);
        Assert.That(updatedAuction!.IsPaymentConfirmed, Is.True);
        Assert.That(updatedAuction.WinnerId, Is.EqualTo(auction.ProvisionalWinnerId));
    }

    [Test]
    public async Task ConfirmPayment_WithInsufficientBalance_ShouldFail()
    {
        var auction = await CreateFinalizedAuctionWithProvisionalWinner(100, 150);
        await _balanceRepository.DepositFunds(auction.ProvisionalWinnerId!.Value, 50);
        var handler = CreateConfirmPaymentHandler();
        var command = new ConfirmPaymentCommand { AuctionId = auction.Id };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.That(result.Result.IsError, Is.True);
        Assert.That(result.Result.GetError(), Is.EqualTo(ConfirmPaymentError.InsufficientBalance));
        Assert.That(result.PaymentConfirmed, Is.False);
    }

    [Test]
    public async Task ProcessDeadline_AfterDeadlineWithInsufficientFunds_ShouldPromoteNextBid()
    {
        var auction = await CreateFinalizedAuctionWithProvisionalWinner(100, 150);
        var user2 = Guid.NewGuid();
        await PlaceBid(auction.Id, user2, 140);

        await _balanceRepository.DepositFunds(auction.ProvisionalWinnerId!.Value, 50);
        await _balanceRepository.DepositFunds(user2, 200);

        _timeProvider.AddTime(TimeSpan.FromHours(4));

        var handler = CreateProcessDeadlineHandler();
        var command = new ProcessPaymentDeadlineCommand { AuctionId = auction.Id };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.That(result.Result.IsOk, Is.True);
        Assert.That(result.NewWinnerId, Is.EqualTo(user2));
        Assert.That(result.AllBidsExhausted, Is.False);

        var updatedAuction = await _auctionsRepository.GetAuction(auction.Id);
        Assert.That(updatedAuction!.ProvisionalWinnerId, Is.EqualTo(user2));
    }

    [Test]
    public async Task ProcessDeadline_ShouldBanRejectedUser()
    {
        var rejectedUserId = Guid.NewGuid();
        var user = await _usersRepository.CreateUser("rejected@test.com", "hash");

        var auction = await CreateFinalizedAuctionWithProvisionalWinner(100, 150, user!.UserId);
        await _balanceRepository.DepositFunds(user.UserId, 50);

        _timeProvider.AddTime(TimeSpan.FromHours(4));

        var handler = CreateProcessDeadlineHandler();
        var command = new ProcessPaymentDeadlineCommand { AuctionId = auction.Id };

        await handler.Handle(command, CancellationToken.None);

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
        var user1 = Guid.NewGuid();
        var auction = await CreateFinalizedAuctionWithProvisionalWinner(100, 150, user1);

        await _balanceRepository.DepositFunds(user1, 50);

        _timeProvider.AddTime(TimeSpan.FromHours(4));

        var handler = CreateProcessDeadlineHandler();

        var result = await handler.Handle(
            new ProcessPaymentDeadlineCommand { AuctionId = auction.Id },
            CancellationToken.None);

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
        var auction = await CreateFinalizedAuctionWithProvisionalWinner(100, 150);
        await _balanceRepository.DepositFunds(auction.ProvisionalWinnerId!.Value, 50);

        var handler = CreateProcessDeadlineHandler();
        var command = new ProcessPaymentDeadlineCommand { AuctionId = auction.Id };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.That(result.Result.IsError, Is.True);
        Assert.That(result.Result.GetError(), Is.EqualTo(ProcessPaymentError.DeadlineNotPassed));
    }

    [Test]
    public async Task ProcessDeadline_WinnerPaysBeforeDeadline_ShouldConfirmPayment()
    {
        var auction = await CreateFinalizedAuctionWithProvisionalWinner(100, 150);
        await _balanceRepository.DepositFunds(auction.ProvisionalWinnerId!.Value, 200);

        _timeProvider.AddTime(TimeSpan.FromHours(4));

        var handler = CreateProcessDeadlineHandler();
        var command = new ProcessPaymentDeadlineCommand { AuctionId = auction.Id };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.That(result.Result.IsOk, Is.True);
        var finalAuction = await _auctionsRepository.GetAuction(auction.Id);
        Assert.That(finalAuction!.IsPaymentConfirmed, Is.True);
        Assert.That(finalAuction.WinnerId, Is.EqualTo(auction.ProvisionalWinnerId));
    }

    [Test]
    public async Task Auction_HasProvisionalWinner_ShouldReturnTrue()
    {
        var auction = await CreateFinalizedAuctionWithProvisionalWinner(100, 150);

        Assert.That(auction.HasProvisionalWinner(), Is.True);
    }

    [Test]
    public async Task Auction_AfterPaymentConfirmed_ShouldNotHaveProvisionalWinner()
    {
        var auction = await CreateFinalizedAuctionWithProvisionalWinner(100, 150);
        auction.ConfirmPayment();

        Assert.That(auction.HasProvisionalWinner(), Is.False);
        Assert.That(auction.IsPaymentConfirmed, Is.True);
    }

    [Test]
    public void Auction_SetProvisionalWinner_NotFinalized_ShouldThrow()
    {
        var auction = new Auction
        {
            Title = "Test",
            EndTime = DateTime.UtcNow.AddHours(1),
            MinPrice = 100,
            Type = AuctionType.Open
        };

        Assert.Throws<InvalidOperationException>(() =>
            auction.SetProvisionalWinner(Guid.NewGuid(), 150, DateTime.UtcNow.AddHours(3)));
    }

    [Test]
    public void PaymentProcessingService_SufficientBalance_ShouldConfirm()
    {
        var service = new PaymentProcessingService();
        var auction = CreateFinalizedAuctionWithProvisionalWinnerSync(100, 150);
        var balance = 200m;

        var result = service.ProcessPaymentDeadline(
            auction, 
            new List<Bid>(), 
            balance,
            new HashSet<Guid>(),
            _ => false);

        Assert.That(result.IsConfirmed, Is.True);
        Assert.That(auction.IsPaymentConfirmed, Is.True);
    }

    private FinalizeAuctionCommandHandler CreateFinalizeHandler()
    {
        return new FinalizeAuctionCommandHandler(
            _auctionsRepository,
            _bidsRepository,
            _cycleRepository,
            _timeProvider,
            _paymentConfig,
            _winnerSelectionService,
            _noRepeatWinnerPolicy);
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
            _cycleRepository,
            _balanceRepository,
            _usersRepository,
            _timeProvider,
            _paymentConfig,
            _winnerSelectionService,
            _noRepeatWinnerPolicy,
            _paymentProcessingService,
            _banPolicy);
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

    private Auction CreateFinalizedAuctionWithProvisionalWinnerSync(
        decimal minPrice,
        decimal winningBid)
    {
        var auction = new Auction
        {
            Title = "Test auction",
            EndTime = DateTime.UtcNow.AddMinutes(-5),
            Type = AuctionType.Open,
            MinimumIncrement = 10,
            MinPrice = minPrice,
            TieBreakingPolicy = TieBreakingPolicy.Earliest
        };
        auction.TransitionToActive();
        auction.TransitionToEnded();
        auction.Finalize(null, null);
        var deadline = DateTime.UtcNow.AddHours(3);
        auction.SetProvisionalWinner(Guid.NewGuid(), winningBid, deadline);
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