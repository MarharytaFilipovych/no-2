namespace Domain.Users;

public class User
{
    public Guid UserId { get; init; }
    public string? Email { get; init; }
    public DateTime? BannedUntil { get; set; }

    public bool IsBanned(DateTime currentTime)
    {
        return BannedUntil.HasValue && currentTime < BannedUntil.Value;
    }

    public void Ban(DateTime until)
    {
        BannedUntil = until;
    }

    public void Unban()
    {
        BannedUntil = null;
    }
}