using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Veylog.Models;

namespace Veylog.Logging
{
    public class LogFlushBackgroundService : BackgroundService
    {
        private readonly ILogQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly VeylogOptions _options;

        private readonly List<object> _buffer = new();
        private readonly SemaphoreSlim _bufferLock = new(1, 1);

        public LogFlushBackgroundService(
            ILogQueue queue,
            IServiceScopeFactory scopeFactory,
            VeylogOptions options)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _options = options;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var flushInterval = TimeSpan.FromSeconds(_options.FlushIntervalSeconds);
            var maxBatchSize = _options.MaxBatchSize;

            using var timer = new PeriodicTimer(flushInterval);

            var readLoop = ReadLoopAsync(maxBatchSize, stoppingToken);
            var timerLoop = TimerLoopAsync(timer, stoppingToken);

            await Task.WhenAll(readLoop, timerLoop);
        }

        // Flush as soon as the buffer hits max batch size
        private async Task ReadLoopAsync(int maxBatchSize, CancellationToken token)
        {
            await foreach (var item in _queue.ReadAllAsync(token))
            {
                List<object>? toFlush = null;

                await _bufferLock.WaitAsync(token);
                try
                {
                    _buffer.Add(item);
                    if (_buffer.Count >= maxBatchSize)
                    {
                        toFlush = new List<object>(_buffer);
                        _buffer.Clear();
                    }
                }
                finally { _bufferLock.Release(); }

                if (toFlush != null)
                    await FlushAsync(toFlush);
            }
        }

        // Flush on a timer even if batch size wasn't reached (so logs aren't stuck in RAM too long)
        private async Task TimerLoopAsync(PeriodicTimer timer, CancellationToken token)
        {
            try
            {
                while (await timer.WaitForNextTickAsync(token))
                {
                    List<object>? toFlush = null;

                    await _bufferLock.WaitAsync(token);
                    try
                    {
                        if (_buffer.Count > 0)
                        {
                            toFlush = new List<object>(_buffer);
                            _buffer.Clear();
                        }
                    }
                    finally { _bufferLock.Release(); }

                    if (toFlush != null)
                        await FlushAsync(toFlush);
                }
            }
            catch (OperationCanceledException) { /* shutting down */ }
        }

        private async Task FlushAsync(List<object> items)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<LogDbContext>();

                foreach (var item in items)
                {
                    switch (item)
                    {
                        case SqlLog sqlLog:
                        db.SqlLogs.Add(sqlLog);
                        break;
                        case ApiLog apiLog:
                        db.ApiLogs.Add(apiLog);
                        break;
                    }
                }

                await db.SaveChangesAsync();
            }
            catch
            {
                // Never let logging take down the flusher.
                // (Optional: log to a file/console fallback here.)
            }
        }

        // Flush whatever is left when the app shuts down, so you don't lose the tail
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            List<object>? remaining = null;

            await _bufferLock.WaitAsync(cancellationToken);
            try
            {
                if (_buffer.Count > 0)
                {
                    remaining = new List<object>(_buffer);
                    _buffer.Clear();
                }
            }
            finally { _bufferLock.Release(); }

            if (remaining != null)
                await FlushAsync(remaining);

            await base.StopAsync(cancellationToken);
        }
    }
}