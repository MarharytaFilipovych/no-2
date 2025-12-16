using Application.Api.Auctions;
using Application.Commands.Auctions;
using Application.Validators.Auctions;
using Domain.Auctions;
using Domain.Users;
using Infrastructure.InMemory;
using Tests.Utils;

namespace Tests.Domain;

[TestFixture]
public class StateTransitionTests
{
    private IAuctionsRepository _auctionsRepository = null!;
    private IBidsRepository _bidsRepository = null!;
    private IAuctionCycleRepository _cycleRepository = null!;
    private IParticipantBalanceRepository _balanceRepository = null!;
    private Application.Api.Users.IUsersRepository _usersRepository = null!;
    private TestTimeProvider _timeProvider = null!;
    private TestPaymentWindowConfig _paymentConfig = null!;

    [SetUp]
    public void SetUp()
    {
        _auctionsRepository = new AuctionsRepository();
        _bidsRepository = new BidsRepository();
        _cycleRepository = new AuctionCycleRepository();
        _balanceRepository = new ParticipantBalanceRepository();
        _usersRepository = new UserRepository();
        _timeProvider = new TestTimeProvider();
        _paymentConfig = new TestPaymentWindowConfig
        {
            PaymentDeadline = TimeSpan.FromHours(3),
            BanDurationDays = 7
        };
    }

    [Test]
    public async Task FinalizeExpiredAuction_ShouldWork()
    {
        var auction = await _auctionsRepository.CreateAuction(new Auction
        {
            Title = "Expired Auction",
            EndTime = _timeProvider.Now().AddHours(-1),
            Type = AuctionType.Open,
            MinPrice = 100,
            MinimumIncrement = 10,
            TieBreakingPolicy = TieBreakingPolicy.Earliest
        });
        auction.TransitionToActive();
        await _auctionsRepository.UpdateAuction(auction);

        var userId = Guid.NewGuid();
        await _bidsRepository.CreateBid(new Bid
        {
            AuctionId = auction.Id,
            UserId = userId,
            Amount = 150,
            PlacedAt = _timeProvider.Now()
        });

        var handler = CreateFinalizeHandler();
        var result = await handler.Handle(
            new FinalizeAuctionCommand { AuctionId = auction.Id },
            CancellationToken.None);

        Assert.That(result.Result.IsOk, Is.True);
        Assert.That(result.WinnerId, Is.EqualTo(userId));
        Assert.That(result.WinningAmount, Is.EqualTo(150));
    }

    [Test]
    public async Task FinalizeNotExpiredAuction_ShouldFail()
    {
        var auction = await _auctionsRepository.CreateAuction(new Auction
        {
            Title = "Active Auction",
            EndTime = _timeProvider.Now().AddHours(1),
            Type = AuctionType.Open,
            MinPrice = 100,
            MinimumIncrement = 10,
            TieBreakingPolicy = TieBreakingPolicy.Earliest
        });
        auction.TransitionToActive();
        await _auctionsRepository.UpdateAuction(auction);

        var handler = CreateFinalizeHandler();
        var result = await handler.Handle(
            new FinalizeAuctionCommand { AuctionId = auction.Id },
            CancellationToken.None);

        Assert.That(result.Result.IsError, Is.True);
        Assert.That(result.Result.GetError(), Is.EqualTo(FinalizeAuctionError.AuctionNotEnded));
    }

    [Test]
    public async Task ProcessPaymentDeadline_WithSufficientBalance_ShouldConfirm()
    {
        var user = await _usersRepository.CreateUser("test@test.com", "hash");
        var auction = await CreateAndFinalizeAuction(user!.UserId, 150);

        await _balanceRepository.DepositFunds(user.UserId, 200);
        _timeProvider.AddTime(TimeSpan.FromHours(4));

        var handler = CreateProcessDeadlineHandler();
        var result = await handler.Handle(
            new ProcessPaymentDeadlineCommand { AuctionId = auction.Id },
            CancellationToken.None);

        Assert.That(result.Result.IsOk, Is.True);

        var finalAuction = await _auctionsRepository.GetAuction(auction.Id);
        Assert.That(finalAuction!.IsPaymentConfirmed, Is.True);
    }

    [Test]
    public async Task ProcessPaymentDeadline_WithInsufficientBalance_ShouldPromoteNextBid()
    {
        var user1 = await _usersRepository.CreateUser("user1@test.com", "hash");
        var user2 = Guid.NewGuid();

        var auction = await _auctionsRepository.CreateAuction(new Auction
        {
            Title = "Test",
            EndTime = _timeProvider.Now().AddHours(-1),
            Type = AuctionType.Open,
            MinPrice = 100,
            MinimumIncrement = 10,
            TieBreakingPolicy = TieBreakingPolicy.Earliest
        });
        auction.TransitionToActive();
        await _auctionsRepository.UpdateAuction(auction);

        await _bidsRepository.CreateBid(new Bid
        {
            AuctionId = auction.Id,
            UserId = user1!.UserId,
            Amount = 150,
            PlacedAt = _timeProvider.Now()
        });
        await _bidsRepository.CreateBid(new Bid
        {
            AuctionId = auction.Id,
            UserId = user2,
            Amount = 140,
            PlacedAt = _timeProvider.Now()
        });

        var finalizeHandler = CreateFinalizeHandler();
        await finalizeHandler.Handle(
            new FinalizeAuctionCommand { AuctionId = auction.Id },
            CancellationToken.None);

        await _balanceRepository.DepositFunds(user1.UserId, 50);
        await _balanceRepository.DepositFunds(user2, 200);
        _timeProvider.AddTime(TimeSpan.FromHours(4));

        var handler = CreateProcessDeadlineHandler();
        var result = await handler.Handle(
            new ProcessPaymentDeadlineCommand { AuctionId = auction.Id },
            CancellationToken.None);

        Assert.That(result.Result.IsOk, Is.True);
        Assert.That(result.NewWinnerId, Is.EqualTo(user2));
    }

    [Test]
    public async Task ConfirmPayment_BeforeDeadline_ShouldWork()
    {
        var userId = Guid.NewGuid();
        var auction = await CreateAndFinalizeAuction(userId, 150);

        await _balanceRepository.DepositFunds(userId, 200);

        var handler = CreateConfirmPaymentHandler();
        var result = await handler.Handle(
            new ConfirmPaymentCommand { AuctionId = auction.Id },
            CancellationToken.None);

        Assert.That(result.Result.IsOk, Is.True);
        Assert.That(result.PaymentConfirmed, Is.True);
    }

    [Test]
    public async Task StateTransition_AuctionStaysActiveUntilCommandCalled()
    {
        var auction = await _auctionsRepository.CreateAuction(new Auction
        {
            Title = "Test",
            EndTime = _timeProvider.Now().AddHours(-2),
            Type = AuctionType.Open,
            MinPrice = 100,
            MinimumIncrement = 10,
            TieBreakingPolicy = TieBreakingPolicy.Earliest
        });
        auction.TransitionToActive();
        await _auctionsRepository.UpdateAuction(auction);

        var loadedAuction = await _auctionsRepository.GetAuction(auction.Id);
        Assert.That(loadedAuction!.State, Is.EqualTo(AuctionState.Active));

        await _bidsRepository.CreateBid(new Bid
        {
            AuctionId = auction.Id,
            UserId = Guid.NewGuid(),
            Amount = 150,
            PlacedAt = _timeProvider.Now()
        });

        var handler = CreateFinalizeHandler();
        await handler.Handle(
            new FinalizeAuctionCommand { AuctionId = auction.Id },
            CancellationToken.None);

        var finalizedAuction = await _auctionsRepository.GetAuction(auction.Id);
        Assert.That(finalizedAuction!.State, Is.EqualTo(AuctionState.Finalized));
    }

    private async Task<Auction> CreateAndFinalizeAuction(Guid userId, decimal amount)
    {
        var auction = await _auctionsRepository.CreateAuction(new Auction
        {
            Title = "Test",
            EndTime = _timeProvider.Now().AddHours(-1),
            Type = AuctionType.Open,
            MinPrice = 100,
            MinimumIncrement = 10,
            TieBreakingPolicy = TieBreakingPolicy.Earliest
        });
        auction.TransitionToActive();
        await _auctionsRepository.UpdateAuction(auction);

        await _bidsRepository.CreateBid(new Bid
        {
            AuctionId = auction.Id,
            UserId = userId,
            Amount = amount,
            PlacedAt = _timeProvider.Now()
        });

        var handler = CreateFinalizeHandler();
        await handler.Handle(
            new FinalizeAuctionCommand { AuctionId = auction.Id },
            CancellationToken.None);

        return await _auctionsRepository.GetAuction(auction.Id);
    }

    private FinalizeAuctionCommandHandler CreateFinalizeHandler()
    {
        return new FinalizeAuctionCommandHandler(
            _auctionsRepository,
            _bidsRepository,
            _cycleRepository,
            _timeProvider,
            _paymentConfig,
            new WinnerSelectionService(),
            new NoRepeatWinnerPolicy(),
            new List<IFinalizeAuctionValidator>
            {
                new AuctionExistsForFinalizationValidator(),
                new AuctionNotAlreadyFinalizedValidator(),
                new AuctionCanBeFinalizedValidator()
            });
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
            new WinnerSelectionService(),
            new NoRepeatWinnerPolicy(),
            new PaymentProcessingService(),
            new BanPolicy(),
            new List<IProcessPaymentDeadlineValidator>
            {
                new AuctionExistsForDeadlineValidator(),
                new HasProvisionalWinnerForDeadlineValidator(),
                new DeadlineHasPassedValidator()
            });
    }

    private ConfirmPaymentCommandHandler CreateConfirmPaymentHandler()
    {
        return new ConfirmPaymentCommandHandler(
            _auctionsRepository,
            _balanceRepository,
            _timeProvider,
            new List<IConfirmPaymentValidator>
            {
                new AuctionExistsForPaymentValidator(),
                new HasProvisionalWinnerValidator(),
                new PaymentDeadlinePassedValidator(),
                new SufficientBalanceValidator()
            });
    }
}