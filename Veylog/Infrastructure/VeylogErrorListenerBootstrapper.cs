using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace Veylog
{
    /// <summary>
    /// Wires the registered IVeylogErrorListener to VeylogOptions.OnError
    /// when the app starts. Consumers never touch this class directly — just
    /// implement IVeylogErrorListener and register it in DI.
    /// </summary>
    public class VeylogErrorListenerBootstrapper : IHostedService
    {
        private readonly VeylogOptions _options;
        private readonly IVeylogErrorListener _listener;

        public VeylogErrorListenerBootstrapper(
            VeylogOptions options,
            IVeylogErrorListener listener)
        {
            _options = options;
            _listener = listener;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _options.OnError += _listener.OnErrorAsync;

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}