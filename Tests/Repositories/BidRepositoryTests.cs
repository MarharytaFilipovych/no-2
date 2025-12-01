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
        await _bidsRepository.CreateBid(CreateBid(1, 1, 100));
        await _bidsRepository.CreateBid(CreateBid(1, 2, 150));
        await _bidsRepository.CreateBid(CreateBid(1, 3, 120));

        // Act
        var highest = await _bidsRepository.GetHighestBidForAuction(1);

        // Assert
        Assert.That(highest, Is.Not.Null);
        Assert.That(highest!.Amount, Is.EqualTo(150));
        Assert.That(highest.UserId, Is.EqualTo(2));
    }

    [Test]
    public async Task GetHighestBidForAuction_WithWithdrawnBids_ShouldIgnoreWithdrawn()
    {
        // Arrange
        await _bidsRepository.CreateBid(CreateBid(1, 1, 100));
        var highBid = await _bidsRepository.CreateBid(CreateBid(1, 2, 150));
        await _bidsRepository.CreateBid(CreateBid(1, 3, 120));

        highBid.Withdraw();
        await _bidsRepository.UpdateBid(highBid);

        // Act
        var highest = await _bidsRepository.GetHighestBidForAuction(1);

        // Assert
        Assert.That(highest, Is.Not.Null);
        Assert.That(highest!.Amount, Is.EqualTo(120));
        Assert.That(highest.UserId, Is.EqualTo(3));
    }

    [Test]
    public async Task GetHighestBidForAuction_WithEqualAmounts_ShouldReturnEarliest()
    {
        // Arrange
        var time = DateTime.UtcNow;
        await _bidsRepository.CreateBid(CreateBid(1, 1, 100, time));
        await _bidsRepository.CreateBid(CreateBid(1, 2, 100, time.AddMinutes(1)));

        // Act
        var highest = await _bidsRepository.GetHighestBidForAuction(1);

        // Assert
        Assert.That(highest, Is.Not.Null);
        Assert.That(highest!.UserId, Is.EqualTo(1));
    }

    [Test]
    public async Task GetActiveBidsByAuction_ShouldExcludeWithdrawn()
    {
        // Arrange
        await _bidsRepository.CreateBid(CreateBid(1, 1, 100));
        var withdrawnBid = await _bidsRepository.CreateBid(CreateBid(1, 2, 150));
        await _bidsRepository.CreateBid(CreateBid(1, 3, 120));

        withdrawnBid.Withdraw();
        await _bidsRepository.UpdateBid(withdrawnBid);

        // Act
        var activeBids = await _bidsRepository.GetActiveBidsByAuction(1);

        // Assert
        Assert.That(activeBids.Count, Is.EqualTo(2));
        Assert.That(activeBids.All(b => !b.IsWithdrawn), Is.True);
    }

    [Test]
    public async Task GetUserBidForAuction_WithMultipleBids_ShouldReturnMostRecent()
    {
        // Arrange
        var time = DateTime.UtcNow;
        await _bidsRepository.CreateBid(CreateBid(1, 1, 100, time));
        await _bidsRepository.CreateBid(CreateBid(1, 1, 150, time.AddMinutes(1)));
        await _bidsRepository.CreateBid(CreateBid(1, 1, 120, time.AddMinutes(2)));

        // Act
        var userBid = await _bidsRepository.GetUserBidForAuction(1, 1);

        // Assert
        Assert.That(userBid, Is.Not.Null);
        Assert.That(userBid!.Amount, Is.EqualTo(120));
    }

    [Test]
    public async Task GetBidsByAuction_ShouldReturnAllBidsIncludingWithdrawn()
    {
        // Arrange
        await _bidsRepository.CreateBid(CreateBid(1, 1, 100));
        var withdrawnBid = await _bidsRepository.CreateBid(CreateBid(1, 2, 150));
        await _bidsRepository.CreateBid(CreateBid(1, 3, 120));

        // Act
        withdrawnBid.Withdraw();
        await _bidsRepository.UpdateBid(withdrawnBid);

        var allBids = await _bidsRepository.GetBidsByAuction(1);

        // Assert
        Assert.That(allBids.Count, Is.EqualTo(3));
    }

    [Test]
    public async Task GetHighestBidForAuction_WithNoBids_ShouldReturnNull()
    {
        // Arrange
        var highest = await _bidsRepository.GetHighestBidForAuction(999);

        // Assert
        Assert.That(highest, Is.Null);
    }

    [Test]
    public async Task GetUserBidForAuction_WhenUserHasNoBids_ShouldReturnNull()
    {
        // Arrange
        await _bidsRepository.CreateBid(CreateBid(1, 1, 100));

        // Act
        var userBid = await _bidsRepository.GetUserBidForAuction(1, 999);

        // Assert
        Assert.That(userBid, Is.Null);
    }

    private static Bid CreateBid(int auctionId, int userId, decimal amount, DateTime? placedAt = null)
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

