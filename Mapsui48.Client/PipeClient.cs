using Mapsui48.Protocol;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Mapsui48.Client
{
    internal class PipeClient : IDisposable
    {
        private NamedPipeClientStream _clientStream;
        private StreamReader _reader;
        private StreamWriter _writer;
        private CancellationTokenSource _cts;
        private readonly ConcurrentDictionary<string, TaskCompletionSource<MapResponse>> _pendingRequests = new ConcurrentDictionary<string, TaskCompletionSource<MapResponse>>();
        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);

        public event Action<MapEvent> OnEventReceived;

        public async Task ConnectAsync(string pipeName, CancellationToken ct)
        {
            _clientStream = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await _clientStream.ConnectAsync(10000, ct);

            _reader = new StreamReader(_clientStream);
            _writer = new StreamWriter(_clientStream) { AutoFlush = true };
            _cts = new CancellationTokenSource();

            _ = Task.Run(() => ReadLoopAsync(_cts.Token));
        }

        private async Task ReadLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && _clientStream.IsConnected)
                {
                    var line = await _reader.ReadLineAsync();
                    if (line == null) break;

                    ProcessIncomingMessage(line);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Console.WriteLine("Pipe read error: " + ex.Message);
            }
        }

        private void ProcessIncomingMessage(string json)
        {
            try
            {
                using (var doc = JsonDocument.Parse(json))
                {
                    // Check if it has a "Type" property to distinguish events from responses
                    if (doc.RootElement.TryGetProperty("Type", out var typeProp))
                    {
                        var type = typeProp.GetString();
                        if (type == "Event")
                        {
                            if (doc.RootElement.TryGetProperty("EventType", out var eventTypeProp))
                            {
                                var eventType = eventTypeProp.GetString();
                                MapEvent evt = null;
                                if (eventType == "MapClicked")
                                    evt = JsonSerializer.Deserialize<MapClickedEvent>(json);
                                else if (eventType == "FeatureClicked")
                                    evt = JsonSerializer.Deserialize<FeatureClickedEvent>(json);
                                else if (eventType == "ViewportChanged")
                                    evt = JsonSerializer.Deserialize<ViewportChangedEvent>(json);
                                else if (eventType == "AreaSelected")
                                    evt = JsonSerializer.Deserialize<AreaSelectedEvent>(json);
                                else if (eventType == "MapPointerMoved")
                                    evt = JsonSerializer.Deserialize<MapPointerMovedEvent>(json);
                                else if (eventType == "MapDoubleClicked")
                                    evt = JsonSerializer.Deserialize<MapDoubleClickedEvent>(json);

                                if (evt != null)
                                    OnEventReceived?.Invoke(evt);
                            }
                            return;
                        }

                    }

                    // It's a response
                    var response = JsonSerializer.Deserialize<MapResponse>(json);
                    if (response != null && response.Id != null && _pendingRequests.TryRemove(response.Id, out var tcs))
                    {
                        tcs.SetResult(response);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to parse message: " + ex.Message);
            }
        }

        public async Task<MapResponse> SendCommandAsync(MapCommand cmd, int timeoutMs = 10000)
        {
            if (_clientStream == null || !_clientStream.IsConnected)
                throw new InvalidOperationException("Pipe is not connected.");

            var tcs = new TaskCompletionSource<MapResponse>();
            _pendingRequests[cmd.Id] = tcs;

            var json = JsonSerializer.Serialize(cmd, cmd.GetType());
            await _writeLock.WaitAsync();
            try
            {
                await _writer.WriteLineAsync(json);
            }
            finally
            {
                _writeLock.Release();
            }

            using (var timeoutCts = new CancellationTokenSource(timeoutMs))
            using (timeoutCts.Token.Register(() => 
            {
                if (_pendingRequests.TryRemove(cmd.Id, out var pending))
                    pending.SetException(new TimeoutException("Command timed out."));
            }))
            {
                return await tcs.Task;
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _writeLock?.Dispose();
            _clientStream?.Dispose();
        }
    }
}
