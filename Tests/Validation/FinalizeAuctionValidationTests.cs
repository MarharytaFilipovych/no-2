using Application.Api.Auctions;
using Application.Commands.Auctions;
using Application.Validators.Auctions;
using Domain.Auctions;
using Infrastructure.InMemory;
using Tests.Utils;

namespace Tests.Validation;

[TestFixture]
public class FinalizeAuctionValidationTests
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
    public async Task FinalizeAuction_AuctionNotFound_ShouldFail()
    {
        var handler = CreateHandler();
        var command = new FinalizeAuctionCommand { AuctionId = Guid.NewGuid() };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.That(result.Result.IsError, Is.True);
        Assert.That(result.Result.GetError(), Is.EqualTo(FinalizeAuctionError.AuctionNotFound));
    }

    [Test]
    public async Task FinalizeAuction_AuctionNotEnded_ShouldFail()
    {
        var auction = await CreateActiveAuction();
        var handler = CreateHandler();
        var command = new FinalizeAuctionCommand { AuctionId = auction.Id };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.That(result.Result.IsError, Is.True);
        Assert.That(result.Result.GetError(), Is.EqualTo(FinalizeAuctionError.AuctionNotEnded));
    }

    [Test]
    public async Task FinalizeAuction_AlreadyFinalized_ShouldFail()
    {
        var auction = await CreateEndedAuction();
        await PlaceBid(auction.Id, Guid.NewGuid(), 150);
        var handler = CreateHandler();
        var command = new FinalizeAuctionCommand { AuctionId = auction.Id };

        await handler.Handle(command, CancellationToken.None);
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.That(result.Result.IsError, Is.True);
        Assert.That(result.Result.GetError(), Is.EqualTo(FinalizeAuctionError.AlreadyFinalized));
    }

    private FinalizeAuctionCommandHandler CreateHandler()
    {
        var validators = new List<IFinalizeAuctionValidator>
        {
            new AuctionExistsForFinalizationValidator(),
            new AuctionNotAlreadyFinalizedValidator(),
            new AuctionCanBeFinalizedValidator()
        };

        var cycleRepository = new AuctionCycleRepository();
        var paymentConfig = new TestPaymentWindowConfig();
        var winnerService = new WinnerSelectionService();
        var noRepeatPolicy = new NoRepeatWinnerPolicy();

        return new FinalizeAuctionCommandHandler(
            _auctionsRepository,
            _bidsRepository,
            cycleRepository,
            _timeProvider,
            paymentConfig,
            winnerService,
            noRepeatPolicy,
            validators);
    }

    private async Task<Auction> CreateActiveAuction()
    {
        var auction = await _auctionsRepository.CreateAuction(new Auction
        {
            Title = "Test",
            EndTime = _timeProvider.Now().AddHours(1),
            Type = AuctionType.Open,
            MinPrice = 100,
            MinimumIncrement = 10,
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
            Title = "Test",
            EndTime = _timeProvider.Now().AddMinutes(-5),
            Type = AuctionType.Open,
            MinPrice = 100,
            MinimumIncrement = 10,
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