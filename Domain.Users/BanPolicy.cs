namespace Domain.Users;

public class BanPolicy
{
    public DateTime CalculateBanExpiration(DateTime currentTime, int banDurationDays)
    {
        if (banDurationDays <= 0)
            throw new ArgumentException("Ban duration must be positive", nameof(banDurationDays));

        return currentTime.AddDays(banDurationDays);
    }

    public void BanForPaymentFailure(User user, DateTime currentTime, int banDurationDays)
    {
        var banUntil = CalculateBanExpiration(currentTime, banDurationDays);
        user.Ban(banUntil);
    }
}