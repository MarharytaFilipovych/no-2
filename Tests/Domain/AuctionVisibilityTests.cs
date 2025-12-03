using Domain.Auctions;

namespace Tests.Domain;

[TestFixture]
public class AuctionVisibilityTests
{
    private AuctionVisibilityService _visibilityService = null!;

    [SetUp]
    public void SetUp()
    {
        _visibilityService = new AuctionVisibilityService();
    }

    #region Open Auction Tests

    [Test]
    public void OpenAuction_Active_ShouldShowBidAmounts()
    {
        // Arrange
        var auction = CreateOpenAuction(AuctionState.Active);

        // Act
        var visible = _visibilityService.AreBidAmountsVisible(auction);

        // Assert
        Assert.That(visible, Is.True);
    }

    [Test]
    public void OpenAuction_Active_ShouldShowHighestBid()
    {
        // Arrange
        var auction = CreateOpenAuction(AuctionState.Active);

        // Act
        var visible = _visibilityService.ShouldShowHighestBid(auction);

        // Assert
        Assert.That(visible, Is.True);
    }

    [Test]
    public void OpenAuction_Pending_ShouldNotShowBidAmounts()
    {
        // Arrange
        var auction = CreateOpenAuction(AuctionState.Pending);

        // Act
        var visible = _visibilityService.AreBidAmountsVisible(auction);

        // Assert
        Assert.That(visible, Is.False);
    }

    [Test]
    public void OpenAuction_ShouldAlwaysShowMinPrice()
    {
        // Arrange
        var auction = CreateOpenAuction(AuctionState.Active);

        // Act
        var visible = _visibilityService.ShouldShowMinPrice(auction);

        // Assert
        Assert.That(visible, Is.True);
    }

    [Test]
    public void OpenAuction_Ended_ShouldShowBidAmounts()
    {
        // Arrange
        var auction = CreateOpenAuction(AuctionState.Ended);

        // Act
        var visible = _visibilityService.AreBidAmountsVisible(auction);

        // Assert
        Assert.That(visible, Is.True);
    }

    [Test]
    public void OpenAuction_Finalized_ShouldShowBidAmounts()
    {
        // Arrange
        var auction = CreateOpenAuction(AuctionState.Finalized);

        // Act
        var visible = _visibilityService.AreBidAmountsVisible(auction);

        // Assert
        Assert.That(visible, Is.True);
    }

    #endregion

    #region Blind Auction Tests

    [Test]
    public void BlindAuction_Active_ShouldNotShowBidAmounts()
    {
        // Arrange
        var auction = CreateBlindAuction(AuctionState.Active);

        // Act
        var visible = _visibilityService.AreBidAmountsVisible(auction);

        // Assert
        Assert.That(visible, Is.False);
    }

    [Test]
    public void BlindAuction_Active_ShouldNotShowHighestBid()
    {
        // Arrange
        var auction = CreateBlindAuction(AuctionState.Active);

        // Act
        var visible = _visibilityService.ShouldShowHighestBid(auction);

        // Assert
        Assert.That(visible, Is.False);
    }

    [Test]
    public void BlindAuction_Ended_ShouldShowBidAmounts()
    {
        // Arrange
        var auction = CreateBlindAuction(AuctionState.Ended);

        // Act
        var visible = _visibilityService.AreBidAmountsVisible(auction);

        // Assert
        Assert.That(visible, Is.True);
    }

    [Test]
    public void BlindAuction_Finalized_ShouldShowBidAmounts()
    {
        // Arrange
        var auction = CreateBlindAuction(AuctionState.Finalized);

        // Act
        var visible = _visibilityService.AreBidAmountsVisible(auction);

        // Assert
        Assert.That(visible, Is.True);
    }

    [Test]
    public void BlindAuction_WithShowMinPriceTrue_ShouldShowMinPrice()
    {
        // Arrange
        var auction = CreateBlindAuction(AuctionState.Active, showMinPrice: true);

        // Act
        var visible = _visibilityService.ShouldShowMinPrice(auction);

        // Assert
        Assert.That(visible, Is.True);
    }

    [Test]
    public void BlindAuction_WithShowMinPriceFalse_ShouldNotShowMinPrice()
    {
        // Arrange
        var auction = CreateBlindAuction(AuctionState.Active, showMinPrice: false);

        // Act
        var visible = _visibilityService.ShouldShowMinPrice(auction);

        // Assert
        Assert.That(visible, Is.False);
    }

    #endregion

    #region Bid Details Visibility Tests

    [Test]
    public void CanViewBidDetails_UserOwnsBlindAuctionBid_ShouldReturnTrue()
    {
        // Arrange
        var auction = CreateBlindAuction(AuctionState.Active);
        var userId = Guid.NewGuid();

        // Act
        var canView = _visibilityService.CanViewBidDetails(auction, userId, userId);

        // Assert
        Assert.That(canView, Is.True);
    }

    [Test]
    public void CanViewBidDetails_UserDoesNotOwnBlindAuctionBid_WhileActive_ShouldReturnFalse()
    {
        // Arrange
        var auction = CreateBlindAuction(AuctionState.Active);
        var requestingUser = Guid.NewGuid();
        var bidOwner = Guid.NewGuid();

        // Act
        var canView = _visibilityService.CanViewBidDetails(auction, requestingUser, bidOwner);

        // Assert
        Assert.That(canView, Is.False);
    }

    [Test]
    public void CanViewBidDetails_UserDoesNotOwnBlindAuctionBid_AfterEnded_ShouldReturnTrue()
    {
        // Arrange
        var auction = CreateBlindAuction(AuctionState.Ended);
        var requestingUser = Guid.NewGuid();
        var bidOwner = Guid.NewGuid();

        // Act
        var canView = _visibilityService.CanViewBidDetails(auction, requestingUser, bidOwner);

        // Assert
        Assert.That(canView, Is.True);
    }

    [Test]
    public void CanViewBidDetails_AnonymousUser_OpenAuction_ShouldReturnTrue()
    {
        // Arrange
        var auction = CreateOpenAuction(AuctionState.Active);
        var bidOwner = Guid.NewGuid();

        // Act
        var canView = _visibilityService.CanViewBidDetails(auction, null, bidOwner);

        // Assert
        Assert.That(canView, Is.True);
    }

    [Test]
    public void CanViewBidDetails_AnonymousUser_BlindAuction_WhileActive_ShouldReturnFalse()
    {
        // Arrange
        var auction = CreateBlindAuction(AuctionState.Active);
        var bidOwner = Guid.NewGuid();

        // Act
        var canView = _visibilityService.CanViewBidDetails(auction, null, bidOwner);

        // Assert
        Assert.That(canView, Is.False);
    }

    #endregion

    #region Bid Count Tests

    [Test]
    public void ShouldShowBidCount_ActiveAuction_ShouldReturnTrue()
    {
        // Arrange
        var openAuction = CreateOpenAuction(AuctionState.Active);
        var blindAuction = CreateBlindAuction(AuctionState.Active);

        // Act & Assert
        Assert.That(_visibilityService.ShouldShowBidCount(openAuction), Is.True);
        Assert.That(_visibilityService.ShouldShowBidCount(blindAuction), Is.True);
    }

    [Test]
    public void ShouldShowBidCount_PendingAuction_ShouldReturnFalse()
    {
        // Arrange
        var openAuction = CreateOpenAuction(AuctionState.Pending);
        var blindAuction = CreateBlindAuction(AuctionState.Pending);

        // Act & Assert
        Assert.That(_visibilityService.ShouldShowBidCount(openAuction), Is.False);
        Assert.That(_visibilityService.ShouldShowBidCount(blindAuction), Is.False);
    }

    [Test]
    public void ShouldShowBidCount_EndedAuction_ShouldReturnTrue()
    {
        // Arrange
        var auction = CreateBlindAuction(AuctionState.Ended);

        // Act
        var visible = _visibilityService.ShouldShowBidCount(auction);

        // Assert
        Assert.That(visible, Is.True);
    }

    #endregion

    private static Auction CreateOpenAuction(AuctionState state)
    {
        var auction = new Auction
        {
            Id = Guid.NewGuid(),
            Title = "Test Open Auction",
            EndTime = DateTime.UtcNow.AddHours(1),
            Type = AuctionType.Open,
            MinimumIncrement = 10,
            MinPrice = 100,
            TieBreakingPolicy = TieBreakingPolicy.Earliest
        };

        TransitionToState(auction, state);
        return auction;
    }

    private static Auction CreateBlindAuction(AuctionState state, bool showMinPrice = false)
    {
        var auction = new Auction
        {
            Id = Guid.NewGuid(),
            Title = "Test Blind Auction",
            EndTime = DateTime.UtcNow.AddHours(1),
            Type = AuctionType.Blind,
            MinPrice = 100,
            ShowMinPrice = showMinPrice,
            TieBreakingPolicy = TieBreakingPolicy.Earliest
        };

        TransitionToState(auction, state);
        return auction;
    }

    private static void TransitionToState(Auction auction, AuctionState targetState)
    {
        switch (targetState)
        {
            case AuctionState.Active:
                auction.TransitionToActive();
                break;
            case AuctionState.Ended:
                auction.TransitionToActive();
                auction.TransitionToEnded();
                break;
            case AuctionState.Finalized:
                auction.TransitionToActive();
                auction.TransitionToEnded();
                auction.Finalize(Guid.NewGuid(), 150m);
                break;
        }
    }
}