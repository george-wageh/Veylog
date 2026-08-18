using System.Threading.Channels;

namespace Veylog.Logging
{
    public interface ILogQueue
    {
        void Enqueue(object logEntry);
        IAsyncEnumerable<object> ReadAllAsync(CancellationToken token);
    }

    public class LogQueue : ILogQueue
    {
        private readonly Channel<object> _channel = Channel.CreateUnbounded<object>(
            new UnboundedChannelOptions
            {
                SingleReader = true,   // only the background service reads
                SingleWriter = false   // many requests write concurrently
            });

        public void Enqueue(object logEntry) =>
            _channel.Writer.TryWrite(logEntry); // never blocks, never throws

        public IAsyncEnumerable<object> ReadAllAsync(CancellationToken token) =>
            _channel.Reader.ReadAllAsync(token);
    }
}