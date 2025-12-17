namespace Domain.Auctions;

public enum AuctionRole
{
    Admin,
    Participant
}

public static class AuctionPermissions
{
    public static bool CanCreateAuction(AuctionRole role) => 
        role == AuctionRole.Admin;

    public static bool CanFinalizeAuction(AuctionRole role) => 
        role == AuctionRole.Admin;

    public static bool CanProcessPaymentDeadline(AuctionRole role) => 
        role == AuctionRole.Admin;

    public static bool CanConfirmPayment(AuctionRole role) => 
        role == AuctionRole.Admin;
}