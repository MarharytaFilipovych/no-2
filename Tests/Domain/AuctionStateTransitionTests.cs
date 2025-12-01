using Domain.Auctions;

namespace Tests.Domain;

[TestFixture]
public class AuctionStateTransitionTests
{
    [Test]
    public void NewAuction_ShouldBeInPendingState()
    {
        // Arrange & Act
        var auction = CreateAuction();

        // Assert
        Assert.That(auction.State, Is.EqualTo(AuctionState.Pending));
    }

    [Test]
    public void CanTransitionToActive_WhenInPendingStateAndNoStartTime_ShouldReturnTrue()
    {
        // Arrange
        var auction = CreateAuction();

        // Act
        var result = auction.CanTransitionToActive(DateTime.UtcNow);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void CanTransitionToActive_WhenInPendingStateAndStartTimeReached_ShouldReturnTrue()
    {
        // Arrange
        var auction = CreateAuction(startTime: DateTime.UtcNow.AddMinutes(-5));

        // Act
        var result = auction.CanTransitionToActive(DateTime.UtcNow);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void CanTransitionToActive_WhenInPendingStateButStartTimeNotReached_ShouldReturnFalse()
    {
        // Arrange
        var auction = CreateAuction(startTime: DateTime.UtcNow.AddMinutes(10));

        // Act
        var result = auction.CanTransitionToActive(DateTime.UtcNow);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void TransitionToActive_WhenInPendingState_ShouldSucceed()
    {
        // Arrange
        var auction = CreateAuction();

        // Act
        auction.TransitionToActive();

        // Assert
        Assert.That(auction.State, Is.EqualTo(AuctionState.Active));
    }

    [Test]
    public void TransitionToActive_WhenNotInPendingState_ShouldThrowException()
    {
        // Arrange
        var auction = CreateAuction();
        auction.TransitionToActive();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => auction.TransitionToActive());
    }

    [Test]
    public void IsActive_WhenInActiveStateAndBeforeEndTime_ShouldReturnTrue()
    {
        // Arrange
        var auction = CreateAuction();
        auction.TransitionToActive();

        // Act
        var result = auction.IsActive(DateTime.UtcNow);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void IsActive_WhenInActiveStateButAfterEndTime_ShouldReturnFalse()
    {
        // Arrange
        var auction = CreateAuction(endTime: DateTime.UtcNow.AddMinutes(-10));
        auction.TransitionToActive();

        // Act
        var result = auction.IsActive(DateTime.UtcNow);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void CanTransitionToEnded_WhenActiveAndEndTimeReached_ShouldReturnTrue()
    {
        // Arrange
        var auction = CreateAuction(endTime: DateTime.UtcNow.AddMinutes(-5));
        auction.TransitionToActive();

        // Act
        var result = auction.CanTransitionToEnded(DateTime.UtcNow);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void CanTransitionToEnded_WhenNotActive_ShouldReturnFalse()
    {
        // Arrange
        var auction = CreateAuction(endTime: DateTime.UtcNow.AddMinutes(-5));

        // Act
        var result = auction.CanTransitionToEnded(DateTime.UtcNow);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void TransitionToEnded_WhenInActiveState_ShouldSucceed()
    {
        // Arrange
        var auction = CreateAuction(endTime: DateTime.UtcNow.AddMinutes(-5));
        auction.TransitionToActive();

        // Act
        auction.TransitionToEnded();

        // Assert
        Assert.That(auction.State, Is.EqualTo(AuctionState.Ended));
    }

    [Test]
    public void TransitionToEnded_WhenNotInActiveState_ShouldThrowException()
    {
        // Arrange
        var auction = CreateAuction();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => auction.TransitionToEnded());
    }

    [Test]
    public void CanFinalize_WhenInEndedState_ShouldReturnTrue()
    {
        // Arrange
        var auction = CreateAuction(endTime: DateTime.UtcNow.AddMinutes(-5));
        auction.TransitionToActive();
        auction.TransitionToEnded();

        // Act
        var result = auction.CanFinalize();

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void CanFinalize_WhenNotInEndedState_ShouldReturnFalse()
    {
        // Arrange
        var auction = CreateAuction();
        auction.TransitionToActive();

        // Act
        var result = auction.CanFinalize();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void Finalize_WhenInEndedState_ShouldSucceed()
    {
        // Arrange
        var winnerId = Guid.NewGuid();
        var auction = CreateAuction(endTime: DateTime.UtcNow.AddMinutes(-5));
        auction.TransitionToActive();
        auction.TransitionToEnded();

        // Act
        auction.Finalize(winnerId: winnerId, winningAmount: 500m);

        // Assert
        Assert.That(auction.State, Is.EqualTo(AuctionState.Finalized));
        Assert.That(auction.WinnerId, Is.EqualTo(winnerId));
        Assert.That(auction.WinningBidAmount, Is.EqualTo(500m));
    }

    [Test]
    public void Finalize_WithNoWinner_ShouldSucceed()
    {
        // Arrange
        var auction = CreateAuction(endTime: DateTime.UtcNow.AddMinutes(-5));
        auction.TransitionToActive();
        auction.TransitionToEnded();

        // Act
        auction.Finalize(winnerId: null, winningAmount: null);

        // Assert
        Assert.That(auction.State, Is.EqualTo(AuctionState.Finalized));
        Assert.That(auction.WinnerId, Is.Null);
        Assert.That(auction.WinningBidAmount, Is.Null);
    }

    [Test]
    public void Finalize_WhenNotInEndedState_ShouldThrowException()
    {
        // Arrange
        var auction = CreateAuction();
        auction.TransitionToActive();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => auction.Finalize(Guid.NewGuid(), 500m));
    }

    [Test]
    public void ExtendEndTime_WhenInActiveState_ShouldSucceed()
    {
        // Arrange
        var originalEndTime = DateTime.UtcNow.AddHours(1);
        var auction = CreateAuction(endTime: originalEndTime);
        auction.TransitionToActive();

        // Act
        auction.ExtendEndTime(TimeSpan.FromMinutes(5));

        // Assert
        Assert.That(auction.EndTime, Is.EqualTo(originalEndTime.AddMinutes(5)));
    }

    [Test]
    public void ExtendEndTime_WhenNotInActiveState_ShouldThrowException()
    {
        // Arrange
        var auction = CreateAuction();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => auction.ExtendEndTime(TimeSpan.FromMinutes(5)));
    }

    [Test]
    public void StateTransitionSequence_FromPendingToFinalized_ShouldSucceed()
    {
        // Arrange
        var auction = CreateAuction();

        // Act & Assert
        Assert.That(auction.State, Is.EqualTo(AuctionState.Pending));

        auction.TransitionToActive();
        Assert.That(auction.State, Is.EqualTo(AuctionState.Active));

        auction.TransitionToEnded();
        Assert.That(auction.State, Is.EqualTo(AuctionState.Ended));

        auction.Finalize(Guid.NewGuid(), 500m);
        Assert.That(auction.State, Is.EqualTo(AuctionState.Finalized));
    }
    
    private static Auction CreateAuction(
        DateTime? endTime = null,
        DateTime? startTime = null,
        AuctionType type = AuctionType.Open,
        decimal minPrice = 100,
        TieBreakingPolicy tieBreaking = TieBreakingPolicy.Earliest)
    {
        return new Auction
        {
            Id = Guid.NewGuid(),
            Title = "Test Auction",
            StartTime = startTime,
            EndTime = endTime ?? DateTime.UtcNow.AddHours(1),
            Type = type,
            MinPrice = minPrice,
            TieBreakingPolicy = tieBreaking
        };
    }
}