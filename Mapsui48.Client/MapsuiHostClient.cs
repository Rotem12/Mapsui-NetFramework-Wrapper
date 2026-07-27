using Mapsui48.Protocol;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Mapsui48.Client
{
    public class MapsuiHostClient : IDisposable
    {
        private readonly MapsuiHostProcess _process;
        private readonly PipeClient _pipe;

        public event EventHandler<MapClickedEvent> MapClicked;
        public event EventHandler<FeatureClickedEvent> FeatureClicked;
        public event EventHandler<ViewportChangedEvent> ViewportChanged;
        public event EventHandler<AreaSelectedEvent> AreaSelected;

        public MapsuiHostClient()
        {
            _process = new MapsuiHostProcess();
            _pipe = new PipeClient();
            _pipe.OnEventReceived += Pipe_OnEventReceived;
        }

        public async Task StartAsync(IntPtr parentHwnd, string hostExePath = null, string mbTilesPath = null, string onlineUrl = null, string cachePath = null)
        {
            _process.Start(hostExePath);
            await _pipe.ConnectAsync(_process.PipeName, CancellationToken.None);

            // Wait a moment for host to be ready
            await Task.Delay(500);

            var ping = await _pipe.SendCommandAsync(new PingCommand());
            if (!ping.Success) throw new Exception("Failed to ping host");

            var attachResponse = await _pipe.SendCommandAsync(new AttachToCommand { ParentHwnd = parentHwnd.ToInt64() });
            if (!attachResponse.Success) throw new Exception("Failed to attach to parent HWND");

            if (mbTilesPath != null || onlineUrl != null)
            {
                await _pipe.SendCommandAsync(new SetTileSourceCommand
                {
                    MBTilesPath = mbTilesPath,
                    OnlineUrl = onlineUrl,
                    CachePath = cachePath
                });
            }
        }

        private void Pipe_OnEventReceived(MapEvent evt)
        {
            if (evt is MapClickedEvent mce)
                MapClicked?.Invoke(this, mce);
            else if (evt is FeatureClickedEvent fce)
                FeatureClicked?.Invoke(this, fce);
            else if (evt is ViewportChangedEvent vce)
                ViewportChanged?.Invoke(this, vce);
            else if (evt is AreaSelectedEvent ase)
                AreaSelected?.Invoke(this, ase);
        }

        public async Task NavigateToAsync(double lat, double lon, double? zoom = null, int? durationMs = null)
        {
            await CheckSuccess(_pipe.SendCommandAsync(new NavigateToCommand { Latitude = lat, Longitude = lon, ZoomLevel = zoom, DurationMs = durationMs }));
        }

        public async Task FlyToAsync(double lat, double lon, double? zoom = null, int? durationMs = null)
        {
            await CheckSuccess(_pipe.SendCommandAsync(new FlyToCommand { Latitude = lat, Longitude = lon, ZoomLevel = zoom, DurationMs = durationMs }));
        }

        public async Task SetZoomAsync(double zoom, int? durationMs = null)
        {
            await CheckSuccess(_pipe.SendCommandAsync(new SetZoomCommand { ZoomLevel = zoom, DurationMs = durationMs }));
        }

        public async Task GoHomeAsync(int? durationMs = null)
        {
            await CheckSuccess(_pipe.SendCommandAsync(new GoHomeCommand { DurationMs = durationMs }));
        }

        public async Task SetTileSourceAsync(string mbTilesPath, string onlineUrl, string cachePath)
        {
            await CheckSuccess(_pipe.SendCommandAsync(new SetTileSourceCommand 
            { 
                MBTilesPath = mbTilesPath, 
                OnlineUrl = onlineUrl, 
                CachePath = cachePath 
            }));
        }

        public async Task StartAreaSelectionAsync()
        {
            await CheckSuccess(_pipe.SendCommandAsync(new BeginAreaSelectionCommand()));
        }

        public async Task LoadVectorTileAsync(string mbTilesPath)
        {
            await CheckSuccess(_pipe.SendCommandAsync(new LoadVectorTileCommand
            {
                MBTilesPath = mbTilesPath
            }));
        }

        public async Task<string> AddPolygonAsync(string layer, double[][] coordinates, string fillColor = "#800000FF", string outlineColor = "#FF0000FF", double outlineWidth = 2)
        {
            var coords = coordinates.Select(c => new Coordinate(c[0], c[1])).ToArray();
            var res = await _pipe.SendCommandAsync(new AddPolygonCommand
            {
                LayerName = layer,
                Coordinates = coords,
                FillColor = fillColor,
                OutlineColor = outlineColor,
                OutlineWidth = outlineWidth
            });
            if (!res.Success) throw new Exception(res.Error);
            using var doc = JsonDocument.Parse(res.Data);
            return doc.RootElement.GetProperty("FeatureId").GetString();
        }

        public async Task<string> AddPointAsync(string layer, double lat, double lon, string label = null, string color = "#FFFF0000", double scale = 1.0)
        {
            var res = await _pipe.SendCommandAsync(new AddPointCommand
            {
                LayerName = layer,
                Latitude = lat,
                Longitude = lon,
                Label = label,
                Color = color,
                Scale = scale
            });
            if (!res.Success) throw new Exception(res.Error);
            using var doc = JsonDocument.Parse(res.Data);
            return doc.RootElement.GetProperty("FeatureId").GetString();
        }

        public async Task<string> AddLineAsync(string layer, double[][] coordinates, string color = "#FF0000FF", double width = 2)
        {
            var coords = coordinates.Select(c => new Coordinate(c[0], c[1])).ToArray();
            var res = await _pipe.SendCommandAsync(new AddLineCommand
            {
                LayerName = layer,
                Coordinates = coords,
                Color = color,
                Width = width
            });
            if (!res.Success) throw new Exception(res.Error);
            using var doc = JsonDocument.Parse(res.Data);
            return doc.RootElement.GetProperty("FeatureId").GetString();
        }

        public async Task RemoveFeatureAsync(string layer, string featureId)
        {
            await CheckSuccess(_pipe.SendCommandAsync(new RemoveFeatureCommand { LayerName = layer, FeatureId = featureId }));
        }

        public async Task ClearLayerAsync(string layer)
        {
            await CheckSuccess(_pipe.SendCommandAsync(new ClearLayerCommand { LayerName = layer }));
        }

        private async Task CheckSuccess(Task<MapResponse> task)
        {
            var res = await task;
            if (!res.Success)
                throw new Exception(res.Error);
        }

        public void Dispose()
        {
            _pipe?.Dispose();
            _process?.Dispose();
        }
    }
}
