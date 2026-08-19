using System.Threading.Tasks;

namespace Veylog
{
    /// <summary>
    /// Implement this and register it in DI (e.g. AddSingleton&lt;IVeylogErrorListener, YourClass&gt;())
    /// to be notified whenever an API request that recorded an exception has
    /// had its log entry saved. No manual subscription needed — any
    /// registered implementation is wired up automatically at startup.
    /// </summary>
    public interface IVeylogErrorListener
    {
        /// <param name="path">The request path that failed.</param>
        /// <param name="id">The database Id of the saved ApiLog entry.</param>
        Task OnErrorAsync(string path, long id);
    }
}