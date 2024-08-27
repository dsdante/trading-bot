using System.Diagnostics;
using System.Net;

namespace TradingBot;

[DebuggerDisplay("{DebuggerDisplay}")]
public readonly struct RateLimitResponse
{
    /// <summary> Try parsing rate limits from HTTP headers </summary>
    /// <seealso href="https://ietf.org/archive/id/draft-ietf-httpapi-ratelimit-headers-07.html"/>
    public static bool TryGetFromResponse(HttpResponseMessage response, out RateLimitResponse result)
    {
        var headers = response.Headers;
        bool success = headers.TryGet("x-ratelimit-remaining", out int remaining);

        /*
        if (!headers.TryGet("Date", out string? dateString) ||
            !DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var date))
        {
            date = DateTime.UtcNow;
            success = false;
        }
        */
        // T-Invest may return TooManyRequests if we rely on the Date header.
        var date = DateTime.UtcNow;

        if (!headers.TryGet("x-ratelimit-reset", out int reset))
        {
            reset = defaultWindowSeconds;
            success = false;
        }

        result = new(response.StatusCode, remaining, date.AddSeconds(reset));
        return success;
    }

    private const int defaultWindowSeconds = 60;  // the default fixed window policy at T-Invest

    /// <summary> HTTP status code </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary> The remaining number of requests allowed within the current time window </summary>
    public int Remaining { get; }

    /// <summary> The end of the current time window </summary>
    public DateTime Reset { get; }

    /// <summary> Whether the error code is 2xx </summary>
    public bool IsSuccessStatusCode => (int)StatusCode >= 200 && (int)StatusCode < 300;

    /// <summary> Initialize a RateLimit instance indicating some kind of a problem </summary>
    public RateLimitResponse(HttpStatusCode statusCode)
    {
        StatusCode = statusCode;
        Remaining = 0;
        Reset = DateTime.UtcNow.AddSeconds(defaultWindowSeconds);
    }

    /// <summary> Initialize a RateLimit instance </summary>
    public RateLimitResponse(HttpStatusCode statusCode, int remaining, DateTime timeout)
    {
        StatusCode = statusCode;
        Remaining = remaining;
        Reset = timeout;
    }

    /// <summary> Parse rate limits from HTTP headers </summary>
    /// <exception cref="HttpIOException">A required header is missing</exception>
    public RateLimitResponse(HttpResponseMessage response)
    {
        StatusCode = response.StatusCode;
        var headers = response.Headers;
        Remaining = headers.Get<int>("x-ratelimit-remaining");

        /*
        var date = DateTime.Parse(headers.Get<string>("Date"),
                                  CultureInfo.InvariantCulture,
                                  DateTimeStyles.AdjustToUniversal);
        */
        // T-Invest may return TooManyRequests if we rely on the Date header.
        var date = DateTime.UtcNow;

        Reset = date.AddSeconds(headers.Get<int>("x-ratelimit-reset"));
    }

    /// <summary> Rate limit throttling </summary>
    /// <param name="callback">An action to invoke before waiting (if occurs)</param>
    public async Task WaitAsync(Action<TimeSpan>? callback, CancellationToken cancellation)
    {
        if (Remaining > 0)
            return;
        var rateLimitTimeout = Reset - DateTime.UtcNow;
        if (rateLimitTimeout <= TimeSpan.Zero)
            return;
        callback?.Invoke(rateLimitTimeout);
        await Task.Delay(rateLimitTimeout, cancellation);
    }

    private string DebuggerDisplay =>
        $"[{StatusCode}] Remaining: {Remaining}, Reset: {Reset.ToLocalTime():yyyy-MM-dd hh:mm:ss K}";
}
