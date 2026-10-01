using Microsoft.Extensions.Logging.Abstractions;
using OrderIngest.Business.Retry;

namespace OrderIngest.Tests;

public class RetryExecutorTests
{
    private static readonly RetryExecutor Executor =
        new(NullLogger<RetryExecutor>.Instance);

    [Fact]
    public async Task SucceedsOnFirstAttempt_RunsOnce()
    {
        var calls = 0;
        await Executor.ExecuteAsync(_ => { calls++; return Task.CompletedTask; }, "op", maxAttempts: 3);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task TransientFailure_IsRetriedUntilSuccess()
    {
        var calls = 0;
        await Executor.ExecuteAsync(
            _ => ++calls < 3
                ? throw new InvalidOperationException("transient")
                : Task.CompletedTask,
            "op", maxAttempts: 3);

        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task AllAttemptsFail_ThrowsRetryExhaustedWithAttemptCountAndCause()
    {
        var calls = 0;
        var exhausted = await Assert.ThrowsAsync<RetryExhaustedException>(() =>
            Executor.ExecuteAsync(
                _ => { calls++; throw new InvalidOperationException("still broken"); },
                "doomed-op", maxAttempts: 3));

        Assert.Equal(3, calls);
        Assert.Equal(3, exhausted.TotalAttempts);
        Assert.Equal("doomed-op", exhausted.OperationName);
        Assert.IsType<InvalidOperationException>(exhausted.InnerException);
    }

    [Fact]
    public async Task NonRetryableFailure_IsRethrownImmediately_NotWrapped()
    {
        var calls = 0;
        await Assert.ThrowsAsync<FormatException>(() =>
            Executor.ExecuteAsync(
                _ => { calls++; throw new FormatException("bad input"); },
                "op", maxAttempts: 3,
                isRetryable: ex => ex is not FormatException));

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Cancellation_PropagatesWithoutRetrying()
    {
        using var cts = new CancellationTokenSource();
        var calls = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Executor.ExecuteAsync(
                ct => { calls++; cts.Cancel(); ct.ThrowIfCancellationRequested(); return Task.CompletedTask; },
                "op", maxAttempts: 3, ct: cts.Token));

        Assert.Equal(1, calls);
    }
}
