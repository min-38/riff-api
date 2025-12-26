namespace api.Exceptions;

public class RateLimitException : Exception
{
    public int RemainingSeconds { get; }

    public RateLimitException(string message, int remainingSeconds)
        : base(message)
    {
        RemainingSeconds = remainingSeconds;
    }
}
