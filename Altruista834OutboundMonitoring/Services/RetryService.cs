using System;
using System.Threading;
using System.Threading.Tasks;

namespace Altruista834OutboundMonitoring.Services
{
    public interface IRetryService
    {
        Task ExecuteAsync(Func<Task> action, int maxRetries, TimeSpan delay, ILoggingService logger, CancellationToken cancellationToken);
        Task<T> ExecuteAsync<T>(Func<Task<T>> action, int maxRetries, TimeSpan delay, ILoggingService logger, CancellationToken cancellationToken);
    }

    public sealed class RetryService : IRetryService
    {
        public Task ExecuteAsync(Func<Task> action, int maxRetries, TimeSpan delay, ILoggingService logger, CancellationToken cancellationToken)
        {
            return ExecuteAsync(async () =>
            {
                await action().ConfigureAwait(false);
                return true;
            }, maxRetries, delay, logger, cancellationToken);
        }

        public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, int maxRetries, TimeSpan delay, ILoggingService logger, CancellationToken cancellationToken)
        {
            Exception latest = null;
            for (var i = 0; i <= maxRetries; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    return await action().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    latest = ex;
                    logger?.Warn($"Retry attempt {i + 1} failed: {ex.Message}");
                    if (i < maxRetries)
                    {
                        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            throw latest ?? new InvalidOperationException("Retry failed without exception details.");
        }
    }
}
