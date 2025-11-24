namespace Application.Utils;

public class UtcTimeProvider : ITimeProvider
{
    public DateTime Now() => DateTime.UtcNow;
}