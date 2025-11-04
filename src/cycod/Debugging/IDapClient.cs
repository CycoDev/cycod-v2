using System.Threading;
using Cycod.Debugging.Protocol;

public interface IDapClient : IDisposable
{
    Task<Response> SendRequestAsync(string command, object? args, CancellationToken ct = default);
    Task SendRequestNoResponseAsync(string command, object? args);
}
