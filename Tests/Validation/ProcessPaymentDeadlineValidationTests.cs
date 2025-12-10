using Application.Api.Auctions;
using Application.Commands.Auctions;
using Application.Validators.Auctions;
using Domain.Auctions;
using Domain.Users;
using Infrastructure.InMemory;
using Tests.Utils;

namespace Tests.Validation;

[TestFixture]
public class ProcessPaymentDeadlineValidationTests
{
    private IAuctionsRepository _auctionsRepository = null!;
    private IBidsRepository _bidsRepository = null!;
    private IParticipantBalanceRepository _balanceRepository = null!;
    private Application.Api.Users.IUsersRepository _usersRepository = null!;
    private IAuctionCycleRepository _cycleRepository = null!;
    private TestTimeProvider _timeProvider = null!;
    private TestPaymentWindowConfig _paymentConfig = null!;

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
    }

    [Test]
    public async Task ProcessDeadline_AuctionNotFound_ShouldFail()
    {
        var handler = CreateHandler();
        var command = new ProcessPaymentDeadlineCommand { AuctionId = Guid.NewGuid() };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.That(result.Result.IsError, Is.True);
        Assert.That(result.Result.GetError(), Is.EqualTo(ProcessPaymentError.AuctionNotFound));
    }

    [Test]
    public async Task ProcessDeadline_NoProvisionalWinner_ShouldFail()
    {
        var auction = await CreateFinalizedAuctionWithoutWinner();
        var handler = CreateHandler();
        var command = new ProcessPaymentDeadlineCommand { AuctionId = auction.Id };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.That(result.Result.IsError, Is.True);
        Assert.That(result.Result.GetError(), Is.EqualTo(ProcessPaymentError.NoProvisionalWinner));
    }

    [Test]
    public async Task ProcessDeadline_BeforeDeadline_ShouldFail()
    {
        var auction = await CreateFinalizedAuctionWithProvisionalWinner(100, 150);
        var handler = CreateHandler();
        var command = new ProcessPaymentDeadlineCommand { AuctionId = auction.Id };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.That(result.Result.IsError, Is.True);
        Assert.That(result.Result.GetError(), Is.EqualTo(ProcessPaymentError.DeadlineNotPassed));
    }

    private ProcessPaymentDeadlineCommandHandler CreateHandler()
    {
        var validators = new List<IProcessPaymentDeadlineValidator>
        {
            new AuctionExistsForDeadlineValidator(),
            new HasProvisionalWinnerForDeadlineValidator(),
            new DeadlineHasPassedValidator()
        };

        var winnerService = new WinnerSelectionService();
        var noRepeatPolicy = new NoRepeatWinnerPolicy();
        var paymentService = new PaymentProcessingService();
        var banPolicy = new BanPolicy();

        return new ProcessPaymentDeadlineCommandHandler(
            _auctionsRepository,
            _bidsRepository,
            _cycleRepository,
            _balanceRepository,
            _usersRepository,
            _timeProvider,
            _paymentConfig,
            winnerService,
            noRepeatPolicy,
            paymentService,
            banPolicy,
            validators);
    }

    private async Task<Auction> CreateFinalizedAuctionWithoutWinner()
    {
        var auction = await _auctionsRepository.CreateAuction(new Auction
        {
            Title = "Test",
            EndTime = _timeProvider.Now().AddMinutes(-5),
            Type = AuctionType.Open,
            MinPrice = 100,
            MinimumIncrement = 10,
            TieBreakingPolicy = TieBreakingPolicy.Earliest
        });
        auction.TransitionToActive();
        auction.TransitionToEnded();
        auction.FinalizeWithNoWinner();
        await _auctionsRepository.UpdateAuction(auction);
        return auction;
    }

    private async Task<Auction> CreateFinalizedAuctionWithProvisionalWinner(
        decimal minPrice,
        decimal winningBid)
    {
        var auction = await _auctionsRepository.CreateAuction(new Auction
        {
            Title = "Test",
            EndTime = _timeProvider.Now().AddMinutes(-5),
            Type = AuctionType.Open,
            MinPrice = minPrice,
            MinimumIncrement = 10,
            TieBreakingPolicy = TieBreakingPolicy.Earliest
        });
        auction.TransitionToActive();
        auction.TransitionToEnded();
        auction.Finalize(null, null);
        
        var deadline = _timeProvider.Now().Add(_paymentConfig.PaymentDeadline);
        auction.SetProvisionalWinner(Guid.NewGuid(), winningBid, deadline);
        await _auctionsRepository.UpdateAuction(auction);
        
        return auction;
    }
}