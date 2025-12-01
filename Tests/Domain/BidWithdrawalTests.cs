using Domain.Auctions;

namespace Tests.Domain;

[TestFixture]
public class BidWithdrawalTests
{
    [Test]
    public void Withdraw_WhenBidIsNotWithdrawn_ShouldSucceed()
    {
        // Arrange
        var bid = CreateBid();
        
        // Act
        bid.Withdraw();

        // Assert
        Assert.That(bid.IsWithdrawn, Is.True);
    }

    [Test]
    public void Withdraw_WhenBidAlreadyWithdrawn_ShouldThrowException()
    {
        // Arrange
        var bid = CreateBid();
        
        // Act
        bid.Withdraw();

        // Assert
        Assert.Throws<InvalidOperationException>(() => bid.Withdraw());
    }

    [Test]
    public void NewBid_ShouldNotBeWithdrawn()
    {
        // Arrange
        var bid = CreateBid();

        // Assert
        Assert.That(bid.IsWithdrawn, Is.False);
    }

    private static Bid CreateBid()
    {
        return new Bid
        {
            Id = Guid.NewGuid(),
            AuctionId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Amount = 100,
            PlacedAt = DateTime.UtcNow
        };
    }
}