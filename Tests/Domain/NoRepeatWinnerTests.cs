using Application.Api.Auctions;
using Application.Commands.Auctions;
using Domain.Auctions;
using Infrastructure.InMemory;
using Tests.Utils;

namespace Tests.Domain;

[TestFixture]
public class NoRepeatWinnerTests
{
    private IAuctionsRepository _auctionsRepository = null!;
    private IBidsRepository _bidsRepository = null!;
    private IParticipantBalanceRepository _balanceRepository = null!;
    private IAuctionCycleRepository _cycleRepository = null!;
    private TestTimeProvider _timeProvider = null!;
    private TestPaymentWindowConfig _paymentConfig = null!;
    private WinnerSelectionService _winnerSelectionService = null!;
    private NoRepeatWinnerPolicy _noRepeatWinnerPolicy = null!;

    [SetUp]
    public void SetUp()
    {
        _auctionsRepository = new AuctionsRepository();
        _bidsRepository = new BidsRepository();
        _balanceRepository = new ParticipantBalanceRepository();
        _cycleRepository = new AuctionCycleRepository();
        _timeProvider = new TestTimeProvider();
        _paymentConfig = new TestPaymentWindowConfig
        {
            PaymentDeadline = TimeSpan.FromHours(3),
            BanDurationDays = 7
        };
        _winnerSelectionService = new WinnerSelectionService();
        _noRepeatWinnerPolicy = new NoRepeatWinnerPolicy();
    }

    [Test]
    public async Task FinalizeAuction_UserAlreadyWonInCategory_ShouldSkipToNextBidder()
    {
        var cycle = await CreateActiveCycle();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        var auction1 = await CreateAndFinalizeAuction("Electronics", user1, 150);

        var auction2 = await CreateActiveAuction("Electronics", minPrice: 100);
        await SetupUserBalance(user1, 10000);
        await SetupUserBalance(user2, 10000);
        await PlaceBid(auction2.Id, user1, 200);
        await PlaceBid(auction2.Id, user2, 180);

        auction2.TransitionToEnded();
        await _auctionsRepository.UpdateAuction(auction2);

        var handler = CreateFinalizeHandler();

        var result = await handler.Handle(
            new FinalizeAuctionCommand { AuctionId = auction2.Id },
            CancellationToken.None);

        Assert.That(result.Result.IsOk, Is.True);
        
        var finalizedAuction = await _auctionsRepository.GetAuction(auction2.Id);
        Assert.That(finalizedAuction!.ProvisionalWinnerId, Is.EqualTo(user2));
        Assert.That(finalizedAuction.ProvisionalWinningAmount, Is.EqualTo(180));
    }

    [Test]
    public async Task FinalizeAuction_DifferentCategory_NoExclusion()
    {
        var cycle = await CreateActiveCycle();
        var user1 = Guid.NewGuid();

        await CreateAndFinalizeAuction("Electronics", user1, 150);

        var auction2 = await CreateActiveAuction("Books", minPrice: 100);
        await SetupUserBalance(user1, 10000);
        await PlaceBid(auction2.Id, user1, 200);

        auction2.TransitionToEnded();
        await _auctionsRepository.UpdateAuction(auction2);

        var handler = CreateFinalizeHandler();

        var result = await handler.Handle(
            new FinalizeAuctionCommand { AuctionId = auction2.Id },
            CancellationToken.None);

        Assert.That(result.Result.IsOk, Is.True);
        
        var finalizedAuction = await _auctionsRepository.GetAuction(auction2.Id);
        Assert.That(finalizedAuction!.ProvisionalWinnerId, Is.EqualTo(user1));
    }

    [Test]
    public async Task FinalizeAuction_NoCycle_NoExclusion()
    {
        var user1 = Guid.NewGuid();
        await CreateAndFinalizeAuction("Electronics", user1, 150);

        var auction2 = await CreateActiveAuction("Electronics", minPrice: 100);
        await SetupUserBalance(user1, 10000);
        await PlaceBid(auction2.Id, user1, 200);

        auction2.TransitionToEnded();
        await _auctionsRepository.UpdateAuction(auction2);

        var handler = CreateFinalizeHandler();

        var result = await handler.Handle(
            new FinalizeAuctionCommand { AuctionId = auction2.Id },
            CancellationToken.None);

        Assert.That(result.Result.IsOk, Is.True);
        
        var finalizedAuction = await _auctionsRepository.GetAuction(auction2.Id);
        Assert.That(finalizedAuction!.ProvisionalWinnerId, Is.EqualTo(user1));
    }

    [Test]
    public void NoRepeatWinnerPolicy_UserAlreadyWon_ShouldExclude()
    {
        var policy = new NoRepeatWinnerPolicy();
        var winnerId = Guid.NewGuid();
        
        var currentAuction = new Auction
        {
            Id = Guid.NewGuid(),
            Title = "Current",
            Category = "Electronics",
            EndTime = DateTime.UtcNow,
            MinPrice = 100,
            Type = AuctionType.Open
        };

        var previousAuction = new Auction
        {
            Id = Guid.NewGuid(),
            Title = "Previous",
            Category = "Electronics",
            EndTime = DateTime.UtcNow.AddDays(-1),
            MinPrice = 100,
            Type = AuctionType.Open
        };
        previousAuction.TransitionToActive();
        previousAuction.TransitionToEnded();
        previousAuction.Finalize(winnerId, 150m);

        var finalizedAuctions = new List<Auction> { previousAuction };

        var excluded = policy.GetExcludedUsers(currentAuction, finalizedAuctions);

        Assert.That(excluded, Contains.Item(winnerId));
    }

    private async Task<AuctionCycle> CreateActiveCycle()
    {
        var cycle = new AuctionCycle
        {
            Name = "Q1 2025",
            StartDate = _timeProvider.Now().AddDays(-30),
            EndDate = _timeProvider.Now().AddDays(30),
            IsActive = true
        };
        return await _cycleRepository.CreateCycle(cycle);
    }

    private async Task<Auction> CreateAndFinalizeAuction(string category, Guid winnerId, decimal amount)
    {
        var auction = await _auctionsRepository.CreateAuction(new Auction
        {
            Title = $"Test {category} Auction",
            Category = category,
            EndTime = _timeProvider.Now().AddMinutes(-10),
            Type = AuctionType.Open,
            MinPrice = 100,
            MinimumIncrement = 10,
            TieBreakingPolicy = TieBreakingPolicy.Earliest
        });

        auction.TransitionToActive();
        auction.TransitionToEnded();
        auction.Finalize(null, null);
        auction.SetProvisionalWinner(winnerId, amount, _timeProvider.Now().AddHours(3));
        auction.ConfirmPayment();

        await _auctionsRepository.UpdateAuction(auction);
        return auction;
    }

    private async Task<Auction> CreateActiveAuction(string category, decimal minPrice)
    {
        var auction = await _auctionsRepository.CreateAuction(new Auction
        {
            Title = $"Test {category} Auction",
            Category = category,
            EndTime = _timeProvider.Now().AddHours(1),
            Type = AuctionType.Open,
            MinPrice = minPrice,
            MinimumIncrement = 10,
            TieBreakingPolicy = TieBreakingPolicy.Earliest
        });
        auction.TransitionToActive();
        await _auctionsRepository.UpdateAuction(auction);
        return auction;
    }

    private async Task PlaceBid(Guid auctionId, Guid userId, decimal amount)
    {
        await _bidsRepository.CreateBid(new Bid
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
}