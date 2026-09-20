namespace GamingHistory.Api;

// Development-only, process-local failures make retries reproducible without an upstream service.
public sealed class MockFailurePlan
{
    private readonly object _gate = new();
    private readonly Dictionary<string, int> _currentAttempts;
    private int _rosterAttempts;

    public MockFailurePlan(IConfiguration configuration)
    {
        _currentAttempts = configuration.GetSection("Mock:CurrentFailureAttempts").Get<Dictionary<string, int>>() ?? [];
        _rosterAttempts = configuration.GetValue<int>("Mock:RosterFailureAttempts");

        if (_rosterAttempts < 0 || _currentAttempts.Values.Any(attempts => attempts < 0))
        {
            throw new InvalidOperationException("Mock failure attempt counts must not be negative.");
        }
    }

    public bool FailRoster()
    {
        lock (_gate)
        {
            if (_rosterAttempts == 0)
            {
                return false;
            }

            _rosterAttempts--;

            return true;
        }
    }

    public bool FailCurrentScore(string characterId)
    {
        lock (_gate)
        {
            if (!_currentAttempts.TryGetValue(characterId, out var remaining) || remaining == 0)
            {
                return false;
            }

            _currentAttempts[characterId] = remaining - 1;

            return true;
        }
    }
}
