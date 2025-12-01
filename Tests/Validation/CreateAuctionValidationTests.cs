using Application.Api.Auctions;
using Application.Commands.Auctions;
using Domain.Auctions;
using Infrastructure.InMemory;
using Tests.Utils;

namespace Tests.Validation;

[TestFixture]
public class CreateAuctionValidationTests
{
    private IAuctionsRepository _auctionsRepository = null!;
    private TestTimeProvider _timeProvider = null!;

    [SetUp]
    public void SetUp()
    {
        _auctionsRepository = new AuctionsRepository();
        _timeProvider = new TestTimeProvider();
    }

    [Test]
    public async Task CreateAuction_WithValidData_ShouldSucceed()
    {
        // Arrange
        var handler = new CreateAuctionCommandHandler(_auctionsRepository, _timeProvider);
        var command = CreateCommand(minimumIncrement: 10);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsOk, Is.True);
        Assert.That(result.AuctionId, Is.GreaterThan(0));
    }

    [Test]
    public async Task CreateAuction_WithEmptyTitle_ShouldFail()
    {
        // Arrange
        var handler = new CreateAuctionCommandHandler(_auctionsRepository, _timeProvider);
        var command = CreateCommand(title: "", minimumIncrement: 10);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsError, Is.True);
        Assert.That(result.Result.GetError(), Is.EqualTo(CreateAuctionError.InvalidTitle));
    }

    [Test]
    public async Task CreateAuction_WithWhitespaceTitle_ShouldFail()
    {
        // Arrange
        var handler = new CreateAuctionCommandHandler(_auctionsRepository, _timeProvider);
        var command = CreateCommand(title: "   ", minimumIncrement: 10);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsError, Is.True);
        Assert.That(result.Result.GetError(), Is.EqualTo(CreateAuctionError.InvalidTitle));
    }

    [Test]
    public async Task CreateAuction_WithStartTimeAfterEndTime_ShouldFail()
    {
        // Arrange
        var handler = new CreateAuctionCommandHandler(_auctionsRepository, _timeProvider);
        var command = CreateCommand(
            startTime: _timeProvider.Now().AddHours(2),
            endTime: _timeProvider.Now().AddHours(1),
            minimumIncrement: 10
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsError, Is.True);
        Assert.That(result.Result.GetError(), Is.EqualTo(CreateAuctionError.InvalidTimeRange));
    }

    [Test]
    public async Task CreateAuction_OpenWithoutIncrement_ShouldFail()
    {
        // Arrange
        var handler = new CreateAuctionCommandHandler(_auctionsRepository, _timeProvider);
        var command = CreateCommand();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsError, Is.True);
        Assert.That(result.Result.GetError(), Is.EqualTo(CreateAuctionError.InvalidIncrement));
    }

    [Test]
    public async Task CreateAuction_OpenWithZeroIncrement_ShouldFail()
    {
        // Arrange
        var handler = new CreateAuctionCommandHandler(_auctionsRepository, _timeProvider);
        var command = CreateCommand(minimumIncrement: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsError, Is.True);
        Assert.That(result.Result.GetError(), Is.EqualTo(CreateAuctionError.InvalidIncrement));
    }

    [Test]
    public async Task CreateAuction_OpenWithNegativeIncrement_ShouldFail()
    {
        // Arrange
        var handler = new CreateAuctionCommandHandler(_auctionsRepository, _timeProvider);
        var command = CreateCommand(minimumIncrement: -10);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsError, Is.True);
        Assert.That(result.Result.GetError(), Is.EqualTo(CreateAuctionError.InvalidIncrement));
    }

    [Test]
    public async Task CreateAuction_BlindWithoutIncrement_ShouldSucceed()
    {
        // Arrange
        var handler = new CreateAuctionCommandHandler(_auctionsRepository, _timeProvider);
        var command = CreateCommand(type: AuctionType.Blind);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.Result.IsOk, Is.True);
    }

    [Test]
    public async Task CreateAuction_WithoutStartTime_ShouldTransitionToActive()
    {
        // Arrange
        var handler = new CreateAuctionCommandHandler(_auctionsRepository, _timeProvider);
        var command = CreateCommand(startTime: null, minimumIncrement: 10);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        var auction = await _auctionsRepository.GetAuction(result.AuctionId);
        Assert.That(auction!.State, Is.EqualTo(AuctionState.Active));
    }

    [Test]
    public async Task CreateAuction_WithFutureStartTime_ShouldStayPending()
    {
        // Arrange
        var handler = new CreateAuctionCommandHandler(_auctionsRepository, _timeProvider);
        var command = CreateCommand(
            startTime: _timeProvider.Now().AddHours(1),
            endTime: _timeProvider.Now().AddHours(2),
            minimumIncrement: 10
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        var auction = await _auctionsRepository.GetAuction(result.AuctionId);
        Assert.That(auction!.State, Is.EqualTo(AuctionState.Pending));
    }

    [Test]
    public async Task CreateAuction_WithPastStartTime_ShouldTransitionToActive()
    {
        // Arrange
        var handler = new CreateAuctionCommandHandler(_auctionsRepository, _timeProvider);
        var command = CreateCommand(
            startTime: _timeProvider.Now().AddMinutes(-10),
            minimumIncrement: 10
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        var auction = await _auctionsRepository.GetAuction(result.AuctionId);
        Assert.That(auction!.State, Is.EqualTo(AuctionState.Active));
    }

    private CreateAuctionCommand CreateCommand(
        string title = "Test Auction",
        DateTime? startTime = null,
        DateTime? endTime = null,
        AuctionType type = AuctionType.Open,
        decimal? minimumIncrement = null,
        decimal minPrice = 100,
        TieBreakingPolicy tieBreakingPolicy = TieBreakingPolicy.Earliest)
    {
        return new CreateAuctionCommand
        {
            Title = title,
            StartTime = startTime,
            EndTime = endTime ?? _timeProvider.Now().AddHours(1),
            Type = type,
            MinimumIncrement = minimumIncrement,
            MinPrice = minPrice,
            TieBreakingPolicy = tieBreakingPolicy
        };
    }
}