using Mapsui48.Protocol;
using System;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Mapsui48.Host.Services
{
    public class PipeServer : IDisposable
    {
        private readonly string _pipeName;
        private readonly CommandDispatcher _dispatcher;
        private CancellationTokenSource? _cts;
        private NamedPipeServerStream? _serverStream;
        private StreamWriter? _writer;
        private readonly object _writeLock = new();

        public PipeServer(string pipeName, CommandDispatcher dispatcher)
        {
            _pipeName = pipeName;
            _dispatcher = dispatcher;
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            Task.Run(() => ServerLoop(_cts.Token));
        }

        private async Task ServerLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    _serverStream = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    Console.WriteLine($"Waiting for connection on pipe {_pipeName}...");
                    
                    await _serverStream.WaitForConnectionAsync(ct);
                    Console.WriteLine("Client connected.");

                    using var reader = new StreamReader(_serverStream);
                    _writer = new StreamWriter(_serverStream) { AutoFlush = true };

                    while (!ct.IsCancellationRequested && _serverStream.IsConnected)
                    {
                        var line = await reader.ReadLineAsync();
                        if (line == null) break; // Disconnected

                        // Process command
                        var response = _dispatcher.Dispatch(line);
                        
                        // Send response (thread-safe)
                        var jsonResponse = JsonSerializer.Serialize(response);
                        lock (_writeLock)
                        {
                            _writer.WriteLine(jsonResponse);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Pipe server error: {ex.Message}");
                }
                finally
                {
                    _writer = null;
                    _serverStream?.Dispose();
                }
                
                // If we get here, client disconnected. Wait a bit and recreate server to allow reconnect.
                await Task.Delay(100, ct);
            }
        }

        public void SendEvent(MapEvent evt)
        {
            var writer = _writer;
            var stream = _serverStream;
            if (writer != null && stream != null && stream.IsConnected)
            {
                try
                {
                    var json = JsonSerializer.Serialize(evt, evt.GetType());
                    lock (_writeLock)
                    {
                        writer.WriteLine(json);
                    }
                }
                catch
                {
                    // Client might have disconnected
                }
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _serverStream?.Dispose();
        }
    }
}
