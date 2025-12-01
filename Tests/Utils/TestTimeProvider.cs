using Application.Utils;

namespace Tests.Utils;

public class TestTimeProvider : ITimeProvider
{
    private DateTime _currentTime = DateTime.UtcNow;

    public DateTime Now() => _currentTime;

    public void SetTime(DateTime time) => _currentTime = time;

    public void AddTime(TimeSpan span) => _currentTime = _currentTime.Add(span);
}