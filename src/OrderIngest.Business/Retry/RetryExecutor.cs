using Microsoft.Extensions.Logging;

namespace OrderIngest.Business.Retry;

/// <summary>
/// Runs an operation with a bounded number of attempts and linear backoff.
/// A non-retryable failure (per <paramref name="isRetryable"/>) is rethrown
/// as-is on the spot; when all attempts fail, a
/// <see cref="RetryExhaustedException"/> carrying the attempt count wraps
/// the last failure. Cancellation always propagates immediately.
/// </summary>
public class RetryExecutor(ILogger<RetryExecutor> logger)
{
    private static readonly TimeSpan BaseDelay = TimeSpan.FromMilliseconds(200);

    public async Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        string operationName,
        int maxAttempts,
        Func<Exception, bool>? isRetryable = null,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await operation(ct);
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (isRetryable is not null && !isRetryable(ex))
            {
                throw;
            }
            catch (Exception ex)
            {
                if (attempt >= maxAttempts)
                {
                    throw new RetryExhaustedException(operationName, maxAttempts, ex);
                }

                logger.LogWarning(ex,
                    "'{Operation}' failed (attempt {Attempt}/{Max}), retrying",
                    operationName, attempt, maxAttempts);
                await Task.Delay(BaseDelay * attempt, ct);
            }
        }
    }
}
