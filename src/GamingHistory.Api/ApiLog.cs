namespace GamingHistory.Api;

internal static partial class ApiLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "MOCK ONLY: scenario {Scenario}, frozen clock {Clock}; no external data calls")]
    internal static partial void MockMode(ILogger logger, string scenario, string clock);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Request failed")]
    internal static partial void RequestFailed(ILogger logger, Exception exception);
}
