using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cycod.Debugging.Protocol;

// Minimal DAP client wrapper for netcoredbg process. Events routed via callback.
public class RealDapClient : IDisposable
{
    private readonly Process _process;
    private readonly StreamReader _reader;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Dictionary<int, TaskCompletionSource<Response>> _pendingRequests = new();
    private readonly object _lock = new();
    private int _sequence = 1;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _listenTask;

    private readonly Action<Event> _eventCallback;

    public RealDapClient(string adapterPath, Action<Event> eventCallback)
    {
        _eventCallback = eventCallback;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        var startInfo = new ProcessStartInfo
        {
            FileName = adapterPath,
            Arguments = "--interpreter=vscode",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        Logger.Info($"Starting debug adapter: {adapterPath}");
        _process = Process.Start(startInfo) ?? throw new Exception("Failed to start netcoredbg process");
        _reader = new StreamReader(_process.StandardOutput.BaseStream, Encoding.UTF8);

        _process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data)) Logger.Verbose($"adapter-stderr: {e.Data}");
        };
        _process.BeginErrorReadLine();

        _listenTask = Task.Run(ListenLoop, _cts.Token);
    }

    public async Task<Response> SendRequestAsync(string command, object? args, CancellationToken ct = default)
    {
        int seq;
        var tcs = new TaskCompletionSource<Response>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_lock)
        {
            seq = _sequence++;
            _pendingRequests[seq] = tcs;
        }

        var req = new Request { Seq = seq, Command = command, Arguments = args };
        await SendMessageAsync(req, ct);
        Logger.Verbose($"→ {command} #{seq}");

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
        linked.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            return await tcs.Task.WaitAsync(linked.Token);
        }
        catch (OperationCanceledException)
        {
            lock (_lock) _pendingRequests.Remove(seq);
            throw new TimeoutException($"Request '{command}' timed out");
        }
    }

    public Task SendRequestNoResponseAsync(string command, object? args)
    {
        // Fire and forget
        int seq;
        lock (_lock) seq = _sequence++;
        var req = new Request { Seq = seq, Command = command, Arguments = args };
        Logger.Verbose($"→ {command} #{seq} (no-response)");
        return SendMessageAsync(req, _cts.Token);
    }

    private async Task SendMessageAsync(ProtocolMessage message, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(message, message.GetType(), _jsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        var header = Encoding.UTF8.GetBytes($"Content-Length: {bytes.Length}\r\n\r\n");
        await _process.StandardInput.BaseStream.WriteAsync(header, 0, header.Length, ct);
        await _process.StandardInput.BaseStream.WriteAsync(bytes, 0, bytes.Length, ct);
        await _process.StandardInput.BaseStream.FlushAsync(ct);
    }

    private async Task ListenLoop()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                var node = await ReceiveMessageAsync(_cts.Token);
                if (node == null) break;
                await HandleMessageAsync(node);
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"DAP listen error: {ex.Message}");
        }
        finally
        {
            Logger.Info("DAP listen loop ended");
        }
    }

    private async Task<JsonNode?> ReceiveMessageAsync(CancellationToken ct)
    {
        var headerLine = await _reader.ReadLineAsync(ct);
        if (string.IsNullOrEmpty(headerLine)) return null;
        if (!headerLine.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) return null;
        var len = int.Parse(headerLine.Substring(15).Trim());
        await _reader.ReadLineAsync(ct); // empty line
        var buffer = new char[len];
        var readTotal = 0;
        while (readTotal < len)
        {
            var read = await _reader.ReadAsync(buffer, readTotal, len - readTotal);
            if (read == 0) throw new EndOfStreamException("Unexpected end of stream");
            readTotal += read;
        }
        var json = new string(buffer);
        return JsonNode.Parse(json);
    }

    private Task HandleMessageAsync(JsonNode node)
    {
        var type = node["type"]?.GetValue<string>();
        if (type == "response")
        {
            var resp = node.Deserialize<Response>(_jsonOptions);
            if (resp != null)
            {
                TaskCompletionSource<Response>? tcs = null;
                lock (_lock)
                {
                    if (_pendingRequests.TryGetValue(resp.RequestSeq, out tcs)) _pendingRequests.Remove(resp.RequestSeq);
                }
                tcs?.SetResult(resp);
            }
        }
        else if (type == "event")
        {
            var evt = node.Deserialize<Event>(_jsonOptions);
            if (evt != null) _eventCallback(evt);
        }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        try { _cts.Cancel(); } catch { }
        try { _listenTask.Wait(TimeSpan.FromSeconds(2)); } catch { }
        try { if (!_process.HasExited) _process.Kill(); } catch { }
        _process.Dispose();
        _cts.Dispose();
    }
}
