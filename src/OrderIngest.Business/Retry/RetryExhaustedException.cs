namespace OrderIngest.Business.Retry;

/// <summary>
/// Thrown by <see cref="RetryExecutor"/> when an operation has failed on
/// every attempt. The original failure is preserved as InnerException.
/// </summary>
public class RetryExhaustedException(string operationName, int totalAttempts, Exception innerException)
    : Exception($"'{operationName}' failed after {totalAttempts} attempts", innerException)
{
    public string OperationName { get; } = operationName;
    public int TotalAttempts { get; } = totalAttempts;
}
