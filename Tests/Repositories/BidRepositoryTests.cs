using Application.Api.Auctions;
using Domain.Auctions;
using Infrastructure.InMemory;

namespace Tests.Repositories;

[TestFixture]
public class BidRepositoryTests
{
    private IBidsRepository _bidsRepository = null!;

    [SetUp]
    public void SetUp()
    {
        _bidsRepository = new BidsRepository();
    }

    [Test]
    public async Task GetHighestBidForAuction_WithMultipleBids_ShouldReturnHighest()
    {
        // Arrange
        var auctionId = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        await _bidsRepository.CreateBid(CreateBid(auctionId, Guid.NewGuid(), 100));
        await _bidsRepository.CreateBid(CreateBid(auctionId, userId2, 150));
        await _bidsRepository.CreateBid(CreateBid(auctionId, Guid.NewGuid(), 120));

        // Act
        var highest = await _bidsRepository.GetHighestBidForAuction(auctionId);

        // Assert
        Assert.That(highest, Is.Not.Null);
        Assert.That(highest!.Amount, Is.EqualTo(150));
        Assert.That(highest.UserId, Is.EqualTo(userId2));
    }

    [Test]
    public async Task GetHighestBidForAuction_WithWithdrawnBids_ShouldIgnoreWithdrawn()
    {
        // Arrange
        var auctionId = Guid.NewGuid();
        var userId3 = Guid.NewGuid();
        await _bidsRepository.CreateBid(CreateBid(auctionId, Guid.NewGuid(), 100));
        var highBid = await _bidsRepository.CreateBid(CreateBid(auctionId, Guid.NewGuid(), 150));
        await _bidsRepository.CreateBid(CreateBid(auctionId, userId3, 120));

        highBid.Withdraw();
        await _bidsRepository.UpdateBid(highBid);

        // Act
        var highest = await _bidsRepository.GetHighestBidForAuction(auctionId);

        // Assert
        Assert.That(highest, Is.Not.Null);
        Assert.That(highest!.Amount, Is.EqualTo(120));
        Assert.That(highest.UserId, Is.EqualTo(userId3));
    }

    [Test]
    public async Task GetHighestBidForAuction_WithEqualAmounts_ShouldReturnEarliest()
    {
        // Arrange
        var auctionId = Guid.NewGuid();
        var userId1 = Guid.NewGuid();
        var time = DateTime.UtcNow;
        await _bidsRepository.CreateBid(CreateBid(auctionId, userId1, 100, time));
        await _bidsRepository.CreateBid(CreateBid(auctionId, Guid.NewGuid(), 100, time.AddMinutes(1)));

        // Act
        var highest = await _bidsRepository.GetHighestBidForAuction(auctionId);

        // Assert
        Assert.That(highest, Is.Not.Null);
        Assert.That(highest!.UserId, Is.EqualTo(userId1));
    }

    [Test]
    public async Task GetActiveBidsByAuction_ShouldExcludeWithdrawn()
    {
        // Arrange
        var auctionId = Guid.NewGuid();
        await _bidsRepository.CreateBid(CreateBid(auctionId, Guid.NewGuid(), 100));
        var withdrawnBid = await _bidsRepository.CreateBid(CreateBid(auctionId, Guid.NewGuid(), 150));
        await _bidsRepository.CreateBid(CreateBid(auctionId, Guid.NewGuid(), 120));

        withdrawnBid.Withdraw();
        await _bidsRepository.UpdateBid(withdrawnBid);

        // Act
        var activeBids = await _bidsRepository.GetActiveBidsByAuction(auctionId);

        // Assert
        Assert.That(activeBids.Count, Is.EqualTo(2));
        Assert.That(activeBids.All(b => !b.IsWithdrawn), Is.True);
    }

    [Test]
    public async Task GetUserBidForAuction_WithMultipleBids_ShouldReturnMostRecent()
    {
        // Arrange
        var auctionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var time = DateTime.UtcNow;
        await _bidsRepository.CreateBid(CreateBid(auctionId, userId, 100, time));
        await _bidsRepository.CreateBid(CreateBid(auctionId, userId, 150, time.AddMinutes(1)));
        await _bidsRepository.CreateBid(CreateBid(auctionId, userId, 120, time.AddMinutes(2)));

        // Act
        var userBid = await _bidsRepository.GetUserBidForAuction(auctionId, userId);

        // Assert
        Assert.That(userBid, Is.Not.Null);
        Assert.That(userBid!.Amount, Is.EqualTo(120));
    }

    [Test]
    public async Task GetBidsByAuction_ShouldReturnAllBidsIncludingWithdrawn()
    {
        // Arrange
        var auctionId = Guid.NewGuid();
        await _bidsRepository.CreateBid(CreateBid(auctionId, Guid.NewGuid(), 100));
        var withdrawnBid = await _bidsRepository.CreateBid(CreateBid(auctionId, Guid.NewGuid(), 150));
        await _bidsRepository.CreateBid(CreateBid(auctionId, Guid.NewGuid(), 120));

        // Act
        withdrawnBid.Withdraw();
        await _bidsRepository.UpdateBid(withdrawnBid);

        var allBids = await _bidsRepository.GetBidsByAuction(auctionId);

        // Assert
        Assert.That(allBids.Count, Is.EqualTo(3));
    }

    [Test]
    public async Task GetHighestBidForAuction_WithNoBids_ShouldReturnNull()
    {
        // Arrange
        var highest = await _bidsRepository.GetHighestBidForAuction(Guid.NewGuid());

        // Assert
        Assert.That(highest, Is.Null);
    }

    [Test]
    public async Task GetUserBidForAuction_WhenUserHasNoBids_ShouldReturnNull()
    {
        // Arrange
        var auctionId = Guid.NewGuid();
        await _bidsRepository.CreateBid(CreateBid(auctionId, Guid.NewGuid(), 100));

        // Act
        var userBid = await _bidsRepository.GetUserBidForAuction(auctionId, Guid.NewGuid());

        // Assert
        Assert.That(userBid, Is.Null);
    }

    private static Bid CreateBid(Guid auctionId, Guid userId, decimal amount, DateTime? placedAt = null)
    {
        return new Bid
        {
            AuctionId = auctionId,
            UserId = userId,
            Amount = amount,
            PlacedAt = placedAt ?? DateTime.UtcNow
        };
    }
}