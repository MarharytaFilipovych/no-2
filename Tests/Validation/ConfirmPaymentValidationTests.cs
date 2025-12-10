using Application.Api.Auctions;
using Application.Commands.Auctions;
using Application.Validators.Auctions;
using Domain.Auctions;
using Infrastructure.InMemory;
using Tests.Utils;

namespace Tests.Validation;

[TestFixture]
public class ConfirmPaymentValidationTests
{
    private IAuctionsRepository _auctionsRepository = null!;
    private IParticipantBalanceRepository _balanceRepository = null!;
    private TestTimeProvider _timeProvider = null!;

    [SetUp]
    public void SetUp()
    {
        _auctionsRepository = new AuctionsRepository();
        _balanceRepository = new ParticipantBalanceRepository();
        _timeProvider = new TestTimeProvider();
    }

    [Test]
    public async Task ConfirmPayment_AuctionNotFound_ShouldFail()
    {
        var handler = CreateHandler();
        var command = new ConfirmPaymentCommand { AuctionId = Guid.NewGuid() };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.That(result.Result.IsError, Is.True);
        Assert.That(result.Result.GetError(), Is.EqualTo(ConfirmPaymentError.AuctionNotFound));
    }

    [Test]
    public async Task ConfirmPayment_NoProvisionalWinner_ShouldFail()
    {
        var auction = await CreateFinalizedAuctionWithoutWinner();
        var handler = CreateHandler();
        var command = new ConfirmPaymentCommand { AuctionId = auction.Id };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.That(result.Result.IsError, Is.True);
        Assert.That(result.Result.GetError(), Is.EqualTo(ConfirmPaymentError.NoProvisionalWinner));
    }

    [Test]
    public async Task ConfirmPayment_InsufficientBalance_ShouldFail()
    {
        var auction = await CreateFinalizedAuctionWithProvisionalWinner(100, 150);
        await _balanceRepository.DepositFunds(auction.ProvisionalWinnerId!.Value, 50);
        var handler = CreateHandler();
        var command = new ConfirmPaymentCommand { AuctionId = auction.Id };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.That(result.Result.IsError, Is.True);
        Assert.That(result.Result.GetError(), Is.EqualTo(ConfirmPaymentError.InsufficientBalance));
    }

    [Test]
    public async Task ConfirmPayment_DeadlineAlreadyPassed_ShouldFail()
    {
        var auction = await CreateFinalizedAuctionWithProvisionalWinner(100, 150);
        await _balanceRepository.DepositFunds(auction.ProvisionalWinnerId!.Value, 200);
        
        _timeProvider.AddTime(TimeSpan.FromHours(4));
        
        var handler = CreateHandler();
        var command = new ConfirmPaymentCommand { AuctionId = auction.Id };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.That(result.Result.IsError, Is.True);
        Assert.That(result.Result.GetError(), Is.EqualTo(ConfirmPaymentError.DeadlineAlreadyPassed));
    }

    private ConfirmPaymentCommandHandler CreateHandler()
    {
        var validators = new List<IConfirmPaymentValidator>
        {
            new AuctionExistsForPaymentValidator(),
            new HasProvisionalWinnerValidator(),
            new PaymentDeadlinePassedValidator(),
            new SufficientBalanceValidator()
        };

        return new ConfirmPaymentCommandHandler(
            _auctionsRepository,
            _balanceRepository,
            _timeProvider,
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
        
        var deadline = _timeProvider.Now().Add(TimeSpan.FromHours(3));
        auction.SetProvisionalWinner(Guid.NewGuid(), winningBid, deadline);
        await _auctionsRepository.UpdateAuction(auction);
        
        return auction;
    }
}