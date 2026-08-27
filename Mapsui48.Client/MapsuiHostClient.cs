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
        public event EventHandler<MapDoubleClickedEvent> MapDoubleClicked;
        public event EventHandler<FeatureClickedEvent> FeatureClicked;
        public event EventHandler<ViewportChangedEvent> ViewportChanged;
        public event EventHandler<AreaSelectedEvent> AreaSelected;
        public event EventHandler<MapPointerMovedEvent> PointerMoved;
        public event EventHandler<MapPointerLeftEvent> PointerLeft;

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
            else if (evt is MapDoubleClickedEvent mdce)
                MapDoubleClicked?.Invoke(this, mdce);
            else if (evt is FeatureClickedEvent fce)
                FeatureClicked?.Invoke(this, fce);
            else if (evt is ViewportChangedEvent vce)
                ViewportChanged?.Invoke(this, vce);
            else if (evt is AreaSelectedEvent ase)
                AreaSelected?.Invoke(this, ase);
            else if (evt is MapPointerMovedEvent pme)
                PointerMoved?.Invoke(this, pme);
            else if (evt is MapPointerLeftEvent ple)
                PointerLeft?.Invoke(this, ple);
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

        public async Task<string> AddPolygonAsync(string layer, double[][] coordinates, string fillColor = "#800000FF", string outlineColor = "#FF0000FF", double outlineWidth = 2, string featureId = null)
        {
            var coords = coordinates.Select(c => new Coordinate(c[0], c[1])).ToArray();
            var res = await _pipe.SendCommandAsync(new AddPolygonCommand
            {
                LayerName = layer,
                FeatureId = featureId,
                Coordinates = coords,
                FillColor = fillColor,
                OutlineColor = outlineColor,
                OutlineWidth = outlineWidth
            });
            if (!res.Success) throw new Exception(res.Error);
            using var doc = JsonDocument.Parse(res.Data);
            return doc.RootElement.GetProperty("FeatureId").GetString();
        }

        public async Task<string> AddCircleAsync(string layer, double centerLat, double centerLon, double radiusMeters, string fillColor = null, string outlineColor = "#3B82F6", double outlineWidth = 2.0, double[] dashArray = null, int segments = 64, string featureId = null)

        {
            var res = await _pipe.SendCommandAsync(new AddCircleCommand
            {
                LayerName = layer,
                FeatureId = featureId,
                CenterLatitude = centerLat,
                CenterLongitude = centerLon,
                RadiusMeters = radiusMeters,
                FillColor = fillColor,
                OutlineColor = outlineColor,
                OutlineWidth = outlineWidth,
                DashArray = dashArray,
                Segments = segments
            });
            if (!res.Success) throw new Exception(res.Error);
            using var doc = JsonDocument.Parse(res.Data);
            return doc.RootElement.GetProperty("FeatureId").GetString();
        }

        public async Task<string> AddPointAsync(string layer, double lat, double lon, string label = null, string color = "#FFFF0000", double scale = 1.0, double? rotation = null, string iconType = null, string featureId = null)
        {
            var res = await _pipe.SendCommandAsync(new AddPointCommand
            {
                LayerName = layer,
                FeatureId = featureId,
                Latitude = lat,
                Longitude = lon,
                Label = label,
                Color = color,
                Scale = scale,
                Rotation = rotation,
                IconType = iconType
            });
            if (!res.Success) throw new Exception(res.Error);
            using var doc = JsonDocument.Parse(res.Data);
            return doc.RootElement.GetProperty("FeatureId").GetString();
        }

        public async Task<string> AddLineAsync(string layer, double[][] coordinates, string color = "#FF0000FF", double width = 2, string featureId = null)
        {
            var coords = coordinates.Select(c => new Coordinate(c[0], c[1])).ToArray();
            var res = await _pipe.SendCommandAsync(new AddLineCommand
            {
                LayerName = layer,
                FeatureId = featureId,
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

        // ── Navigation & Camera Controls ─────────────────────────────

        public async Task RotateToAsync(double heading, int? durationMs = null, string easing = null)
        {
            await CheckSuccess(_pipe.SendCommandAsync(new RotateToCommand
            {
                Heading = heading,
                DurationMs = durationMs,
                Easing = easing
            }));
        }

        public async Task SetRotationLockAsync(bool locked)
        {
            await CheckSuccess(_pipe.SendCommandAsync(new SetRotationLockCommand { Locked = locked }));
        }

        public async Task ZoomToBoxAsync(double minLat, double minLon, double maxLat, double maxLon, int? durationMs = null, string boxFit = "Fit")
        {
            await CheckSuccess(_pipe.SendCommandAsync(new ZoomToBoxCommand
            {
                MinLat = minLat,
                MinLon = minLon,
                MaxLat = maxLat,
                MaxLon = maxLon,
                DurationMs = durationMs,
                BoxFit = boxFit
            }));
        }

        public async Task SetViewportBoundsAsync(double? minLat = null, double? minLon = null, double? maxLat = null, double? maxLon = null, double? minZoom = null, double? maxZoom = null)
        {
            await CheckSuccess(_pipe.SendCommandAsync(new SetViewportBoundsCommand
            {
                MinLat = minLat,
                MinLon = minLon,
                MaxLat = maxLat,
                MaxLon = maxLon,
                MinZoom = minZoom,
                MaxZoom = maxZoom
            }));
        }

        public async Task SetPanLockAsync(bool locked)
        {
            await CheckSuccess(_pipe.SendCommandAsync(new SetPanLockCommand { Locked = locked }));
        }

        public async Task SetZoomLockAsync(bool locked)
        {
            await CheckSuccess(_pipe.SendCommandAsync(new SetZoomLockCommand { Locked = locked }));
        }

        // ── Layer Management ─────────────────────────────────────────

        public async Task SetLayerVisibilityAsync(string layer, bool visible)
        {
            await CheckSuccess(_pipe.SendCommandAsync(new SetLayerVisibilityCommand { LayerName = layer, Visible = visible }));
        }

        public async Task SetLayerOpacityAsync(string layer, double opacity)
        {
            await CheckSuccess(_pipe.SendCommandAsync(new SetLayerOpacityCommand { LayerName = layer, Opacity = opacity }));
        }

        public async Task SetLayerScaleRangeAsync(string layer, double? minZoom = null, double? maxZoom = null)
        {
            await CheckSuccess(_pipe.SendCommandAsync(new SetLayerScaleRangeCommand { LayerName = layer, MinZoom = minZoom, MaxZoom = maxZoom }));
        }

        public async Task RemoveLayerAsync(string layer)
        {
            await CheckSuccess(_pipe.SendCommandAsync(new RemoveLayerCommand { LayerName = layer }));
        }

        public async Task<System.Collections.Generic.List<LayerInfoDto>> GetLayersAsync()
        {
            var res = await _pipe.SendCommandAsync(new GetLayersCommand());
            if (!res.Success) throw new Exception(res.Error);
            return JsonSerializer.Deserialize<System.Collections.Generic.List<LayerInfoDto>>(res.Data) ?? new System.Collections.Generic.List<LayerInfoDto>();
        }

        // ── Batch & Advanced Features ────────────────────────────────

        public async Task<System.Collections.Generic.List<string>> AddFeaturesBatchAsync(string layer, System.Collections.Generic.IEnumerable<FeatureDto> features)
        {
            var res = await _pipe.SendCommandAsync(new AddFeaturesBatchCommand
            {
                LayerName = layer,
                Features = features?.ToArray() ?? Array.Empty<FeatureDto>()
            });
            if (!res.Success) throw new Exception(res.Error);
            using var doc = JsonDocument.Parse(res.Data);
            var ids = new System.Collections.Generic.List<string>();
            if (doc.RootElement.TryGetProperty("FeatureIds", out var elem) && elem.ValueKind == JsonValueKind.Array)
            {
                foreach (var id in elem.EnumerateArray())
                    ids.Add(id.GetString());
            }
            return ids;
        }

        public async Task UpdateFeatureAsync(string layer, string featureId, double? lat = null, double? lon = null, double? rotation = null, double? scale = null, string label = null)
        {
            await CheckSuccess(_pipe.SendCommandAsync(new UpdateFeatureCommand
            {
                LayerName = layer,
                FeatureId = featureId,
                Latitude = lat,
                Longitude = lon,
                Rotation = rotation,
                Scale = scale,
                Label = label
            }));
        }

        public async Task ShowCalloutAsync(string layer, string featureId, string title, string subtitle = null, bool enabled = true)
        {
            await CheckSuccess(_pipe.SendCommandAsync(new ShowCalloutCommand
            {
                LayerName = layer,
                FeatureId = featureId,
                Title = title,
                Subtitle = subtitle,
                Enabled = enabled
            }));
        }

        // ── Canvas HUD Widgets ───────────────────────────────────────

        public async Task SetScaleBarWidgetAsync(bool enabled, string position = "BottomLeft", string mode = "Single")
        {
            await CheckSuccess(_pipe.SendCommandAsync(new SetScaleBarWidgetCommand
            {
                Enabled = enabled,
                Position = position,
                Mode = mode
            }));
        }

        public async Task SetMouseCoordinatesWidgetAsync(bool enabled, string position = "BottomRight")
        {
            await CheckSuccess(_pipe.SendCommandAsync(new SetMouseCoordinatesWidgetCommand
            {
                Enabled = enabled,
                Position = position
            }));
        }

        public async Task SetPerformanceWidgetAsync(bool enabled, string position = "TopRight")
        {
            await CheckSuccess(_pipe.SendCommandAsync(new SetPerformanceWidgetCommand
            {
                Enabled = enabled,
                Position = position
            }));
        }

        public async Task SetZoomButtonsWidgetAsync(bool enabled, string position = "TopLeft")
        {
            await CheckSuccess(_pipe.SendCommandAsync(new SetZoomButtonsWidgetCommand
            {
                Enabled = enabled,
                Position = position
            }));
        }

        public async Task SetRulerWidgetAsync(bool enabled)
        {
            await CheckSuccess(_pipe.SendCommandAsync(new SetRulerWidgetCommand
            {
                Enabled = enabled
            }));
        }

        // ── GIS Data Loaders & Formats ────────────────────────────────

        public async Task LoadGeoJsonAsync(string geoJsonOrFilePath, string layerName = "GeoJsonLayer", string fillColor = "#403B82F6", string outlineColor = "#3B82F6", double outlineWidth = 2.0)
        {
            await CheckSuccess(_pipe.SendCommandAsync(new LoadGeoJsonCommand
            {
                GeoJsonOrFilePath = geoJsonOrFilePath,
                LayerName = layerName,
                FillColor = fillColor,
                OutlineColor = outlineColor,
                OutlineWidth = outlineWidth
            }));
        }

        public async Task LoadShapefileAsync(string shapefilePath, string layerName = "ShapefileLayer", string fillColor = "#4010B981", string outlineColor = "#10B981", double outlineWidth = 2.0)
        {
            await CheckSuccess(_pipe.SendCommandAsync(new LoadShapefileCommand
            {
                ShapefilePath = shapefilePath,
                LayerName = layerName,
                FillColor = fillColor,
                OutlineColor = outlineColor,
                OutlineWidth = outlineWidth
            }));
        }

        public async Task AddWmsLayerAsync(string url, string layerName = "WmsLayer", string serviceLayerName = null, string crs = "EPSG:3857")
        {
            await CheckSuccess(_pipe.SendCommandAsync(new AddWmsLayerCommand
            {
                Url = url,
                LayerName = layerName,
                ServiceLayerName = serviceLayerName,
                Crs = crs
            }));
        }

        // ── Coordinate Translation & Spatial Queries ─────────────────

        public async Task<CoordinateResultDto> ScreenToWorldAsync(double screenX, double screenY)
        {
            var res = await _pipe.SendCommandAsync(new ScreenToWorldCommand
            {
                ScreenX = screenX,
                ScreenY = screenY
            });
            if (!res.Success) throw new Exception(res.Error);
            return JsonSerializer.Deserialize<CoordinateResultDto>(res.Data);
        }

        public async Task<CoordinateResultDto> WorldToScreenAsync(double lat, double lon)
        {
            var res = await _pipe.SendCommandAsync(new WorldToScreenCommand
            {
                Latitude = lat,
                Longitude = lon
            });
            if (!res.Success) throw new Exception(res.Error);
            return JsonSerializer.Deserialize<CoordinateResultDto>(res.Data);
        }

        public async Task<BoundsResultDto> GetLayerBoundsAsync(string layerName)
        {
            var res = await _pipe.SendCommandAsync(new GetLayerBoundsCommand
            {
                LayerName = layerName
            });
            if (!res.Success) throw new Exception(res.Error);
            return string.IsNullOrEmpty(res.Data) ? null : JsonSerializer.Deserialize<BoundsResultDto>(res.Data);
        }

        // ── Animated Glide Tracking ──────────────────────────────────

        public async Task<string> AddAnimatedPointAsync(string layerName, double lat, double lon, int durationMs = 1000, string featureId = null, string label = null, string color = null, double scale = 1.0, double? rotation = null, string iconType = null)
        {
            var res = await _pipe.SendCommandAsync(new AddAnimatedPointCommand
            {
                LayerName = layerName,
                Latitude = lat,
                Longitude = lon,
                DurationMs = durationMs,
                FeatureId = featureId,
                Label = label,
                Color = color,
                Scale = scale,
                Rotation = rotation,
                IconType = iconType
            });
            if (!res.Success) throw new Exception(res.Error);
            using var doc = JsonDocument.Parse(res.Data);
            return doc.RootElement.GetProperty("FeatureId").GetString();
        }

        public async Task UpdateAnimatedPointAsync(string layerName, string featureId, double lat, double lon, int durationMs = 1000, double? rotation = null, double? scale = null, string label = null)
        {
            await CheckSuccess(_pipe.SendCommandAsync(new UpdateAnimatedPointCommand
            {
                LayerName = layerName,
                FeatureId = featureId,
                Latitude = lat,
                Longitude = lon,
                DurationMs = durationMs,
                Rotation = rotation,
                Scale = scale,
                Label = label
            }));
        }

        // ── Mouse & Pointer Event Controls ───────────────────────────

        public async Task SetPointerMoveEventsAsync(bool enabled)
        {
            await CheckSuccess(_pipe.SendCommandAsync(new SetPointerMoveEventsCommand
            {
                Enabled = enabled
            }));
        }

        // ── Snapshot & Utilities ─────────────────────────────────────

        public async Task<byte[]> GetSnapshotAsync(string format = "Png", int quality = 100)
        {
            var res = await _pipe.SendCommandAsync(new GetSnapshotCommand
            {
                Format = format,
                Quality = quality
            });
            if (!res.Success) throw new Exception(res.Error);
            using var doc = JsonDocument.Parse(res.Data);
            string base64 = doc.RootElement.GetProperty("Base64Image").GetString();
            return string.IsNullOrEmpty(base64) ? Array.Empty<byte>() : Convert.FromBase64String(base64);
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
