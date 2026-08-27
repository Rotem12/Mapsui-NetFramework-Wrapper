using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Layers.AnimatedLayers;

using Mapsui.Nts;
using Mapsui.Nts.Providers;
using Mapsui.Nts.Providers.Shapefile;
using Mapsui.Providers;
using BruTile;
using Mapsui.Styles;
using Mapsui.Styles.Thematics;
using Mapsui.UI.WindowsForms;
using Mapsui.Experimental.VectorTiles;
using Mapsui.Tiling.Fetcher;
using Mapsui.Tiling.Layers;
using SQLite;
using VexTile.Data.Sources;
using Mapsui48.Host.Helpers;
using Mapsui48.Protocol;
using NetTopologySuite.Geometries;
using Mapsui.Rendering;
using Mapsui.Widgets;
using Mapsui.Widgets.ScaleBar;
using Mapsui.Widgets.InfoWidgets;
using Mapsui.Widgets.ButtonWidgets;
using Mapsui.Widgets.BoxWidgets;
using System;
using System.IO;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Mapsui48.Host.Services
{
    public class MapService
    {
        private readonly MapControl _mapControl;
        private readonly Action<MapEvent> _eventPublisher;
        
        private class DynamicLayerState
        {
            public ILayer Layer { get; set; } = null!;
            public ConcurrentDictionary<string, IFeature> Features { get; set; } = new();

            public void UpdateFeatures()
            {
                if (Layer is MemoryLayer ml)
                {
                    ml.Features = Features.Values;
                    ml.FeaturesWereModified();
                }
                else if (Layer is Layer l)
                {
                    l.DataSource = new MemoryProvider(Features.Values);
                }
            }

            public void ClearFeatures()
            {
                Features.Clear();
                if (Layer is MemoryLayer ml)
                {
                    ml.Features = Array.Empty<IFeature>();
                    ml.FeaturesWereModified();
                }
                else if (Layer is Layer l)
                {
                    l.DataSource = new MemoryProvider();
                }
            }
        }
        private readonly ConcurrentDictionary<string, DynamicLayerState> _layers = new();

        private class AnimatedLayerState
        {
            public AnimatedPointLayer Layer { get; set; } = null!;
            public ConcurrentDictionary<string, AnimatedPointFeature> Features { get; set; } = new();
        }
        private readonly ConcurrentDictionary<string, AnimatedLayerState> _animatedLayers = new();

        private DateTime _lastViewportEventTime = DateTime.MinValue;
        private bool _pointerMoveEventsEnabled;

        public MapService(MapControl mapControl, Action<MapEvent> eventPublisher)
        {
            _mapControl = mapControl;
            _eventPublisher = eventPublisher;

            // Set global map background to the OSM Carto sea color for offline fallback
            _mapControl.Map.BackColor = ParseColor("#AAD3DF");

            // Hide the default attribution text and other default widgets
            _mapControl.Map.Widgets.Clear();

            // Hook up events
            _mapControl.Map.Info += Map_Info;
            _mapControl.Map.Navigator.ViewportChanged += Navigator_ViewportChanged;

            _mapControl.MouseDoubleClick += (s, e) =>
            {
                var world = _mapControl.Map.Navigator.Viewport.ScreenToWorld(new Mapsui.Manipulations.ScreenPosition(e.X, e.Y));
                var (lat, lon) = CoordinateHelper.ToWgs84(world.X, world.Y);
                _eventPublisher(new MapDoubleClickedEvent
                {
                    Latitude = lat,
                    Longitude = lon,
                    ScreenX = e.X,
                    ScreenY = e.Y
                });
            };

            _mapControl.MouseMove += (s, e) =>
            {
                if (!_pointerMoveEventsEnabled) return;
                var world = _mapControl.Map.Navigator.Viewport.ScreenToWorld(new Mapsui.Manipulations.ScreenPosition(e.X, e.Y));
                var (lat, lon) = CoordinateHelper.ToWgs84(world.X, world.Y);
                _eventPublisher(new MapPointerMovedEvent
                {
                    Latitude = lat,
                    Longitude = lon,
                    ScreenX = e.X,
                    ScreenY = e.Y
                });
            };
        }




        public void SetTileSource(string mbTilesPath, string onlineUrl, string cachePath)
        {
            var layers = TileService.CreateTileLayers(mbTilesPath, onlineUrl, cachePath);
            
            // Replace existing BaseMap if present
            var existing = _mapControl.Map.Layers.Where(l => l.Name != null && l.Name.StartsWith("BaseMap_")).ToList();
            foreach (var l in existing)
                _mapControl.Map.Layers.Remove(l);

            for (int i = 0; i < layers.Count; i++)
            {
                _mapControl.Map.Layers.Insert(i, layers[i]);
            }

            if (double.IsNaN(_mapControl.Map.Navigator.Viewport.CenterX) || _mapControl.Map.Navigator.Viewport.Width <= 0)
            {
                GoHome(0);
            }
            _mapControl.Refresh();
        }

        public void NavigateTo(double lat, double lon, double? zoomLevel, int? durationMs)
        {
            var (x, y) = CoordinateHelper.ToMercator(lat, lon);
            var mpoint = new MPoint(x, y);

            if (zoomLevel.HasValue)
            {
                var resolution = CoordinateHelper.ZoomLevelToResolution(zoomLevel.Value);
                if (durationMs.HasValue && durationMs.Value > 0)
                    _mapControl.Map.Navigator.CenterOnAndZoomTo(mpoint, resolution, durationMs.Value);
                else
                {
                    _mapControl.Map.Navigator.CenterOn(mpoint);
                    _mapControl.Map.Navigator.ZoomTo(resolution);
                }
            }
            else
            {
                if (durationMs.HasValue && durationMs.Value > 0)
                    _mapControl.Map.Navigator.CenterOn(mpoint, durationMs.Value);
                else
                    _mapControl.Map.Navigator.CenterOn(mpoint);
            }
        }

        public void FlyTo(double lat, double lon, double? zoomLevel, int? durationMs)
        {
            var (x, y) = CoordinateHelper.ToMercator(lat, lon);
            var mpoint = new MPoint(x, y);
            var resolution = zoomLevel.HasValue ? CoordinateHelper.ZoomLevelToResolution(zoomLevel.Value) : _mapControl.Map.Navigator.Viewport.Resolution;
            _mapControl.Map.Navigator.FlyTo(mpoint, resolution, durationMs ?? 1000);
        }

        public void SetZoom(double zoomLevel, int? durationMs)
        {
            var resolution = CoordinateHelper.ZoomLevelToResolution(zoomLevel);
            if (durationMs.HasValue && durationMs.Value > 0)
                _mapControl.Map.Navigator.ZoomTo(resolution, durationMs.Value);
            else
                _mapControl.Map.Navigator.ZoomTo(resolution);
        }

        public void GoHome(int? durationMs)
        {
            // Try to find the offline MBTiles layer first to use its specific extent
            var offlineLayer = _mapControl.Map.Layers.FirstOrDefault(l => l.Name == "BaseMap_Offline");
            var extent = offlineLayer?.Extent;

            // Only zoom to extent if it's a specific regional bounding box (< 1,000 km) rather than global/world extent
            if (extent != null && extent.Width > 0 && extent.Height > 0 && extent.Width < 1000000 && extent.Height < 1000000)
            {
                MRect mercatorExtent = extent;
                // Check if extent is in WGS84 degrees (bounds between -180 and 180)
                if (extent.Min.X >= -180.0 && extent.Max.X <= 180.0 && extent.Min.Y >= -90.0 && extent.Max.Y <= 90.0)
                {
                    var (minX, minY) = CoordinateHelper.ToMercator(extent.Min.Y, extent.Min.X);
                    var (maxX, maxY) = CoordinateHelper.ToMercator(extent.Max.Y, extent.Max.X);
                    mercatorExtent = new MRect(Math.Min(minX, maxX), Math.Min(minY, maxY), Math.Max(minX, maxX), Math.Max(minY, maxY));
                }

                if (durationMs.HasValue && durationMs.Value > 0)
                    _mapControl.Map.Navigator.ZoomToBox(mercatorExtent, Mapsui.MBoxFit.Fit, durationMs.Value);
                else
                    _mapControl.Map.Navigator.ZoomToBox(mercatorExtent, Mapsui.MBoxFit.Fit);
            }
            else
            {
                // Safe Fallback: Center on Israel land (Jerusalem 31.7767, 35.2345, Zoom 12)
                var (cx, cy) = CoordinateHelper.ToMercator(31.7767, 35.2345);
                var res = CoordinateHelper.ZoomLevelToResolution(12);
                if (durationMs.HasValue && durationMs.Value > 0)
                    _mapControl.Map.Navigator.CenterOnAndZoomTo(new MPoint(cx, cy), res, durationMs.Value);
                else
                {
                    _mapControl.Map.Navigator.CenterOn(new MPoint(cx, cy));
                    _mapControl.Map.Navigator.ZoomTo(res);
                }
            }
        }

        public string AddPolygon(AddPolygonCommand cmd)
        {
            var state = GetOrCreateLayer(cmd.LayerName);
            var featureId = string.IsNullOrEmpty(cmd.FeatureId) ? Guid.NewGuid().ToString("N") : cmd.FeatureId;

            var shell = cmd.Coordinates.Select(c => 
            {
                var (x, y) = CoordinateHelper.ToMercator(c.Latitude, c.Longitude);
                return new NetTopologySuite.Geometries.Coordinate(x, y);
            }).ToList();

            // Auto-close the ring if not already closed (NTS requires first == last)
            if (shell.Count > 0 && !shell[0].Equals2D(shell[shell.Count - 1]))
                shell.Add(new NetTopologySuite.Geometries.Coordinate(shell[0].X, shell[0].Y));

            var polygon = new Polygon(new LinearRing(shell.ToArray()));
            var feature = new GeometryFeature(polygon) { ["ID"] = featureId };
            
            var fillColor = ParseColor(cmd.FillColor, "#800000FF");
            var outlineColor = ParseColor(cmd.OutlineColor, "#FF0000FF");
            
            feature.Styles.Add(new VectorStyle
            {
                Fill = new Mapsui.Styles.Brush(fillColor),
                Outline = new Mapsui.Styles.Pen(outlineColor, cmd.OutlineWidth > 0 ? cmd.OutlineWidth : 2)
            });

            state.Features[featureId] = feature;
            state.UpdateFeatures();
            _mapControl.RefreshGraphics();
            return featureId;
        }

        public string AddCircle(AddCircleCommand cmd)
        {
            var state = GetOrCreateLayer(cmd.LayerName);
            var featureId = string.IsNullOrEmpty(cmd.FeatureId) ? Guid.NewGuid().ToString("N") : cmd.FeatureId;

            const double R = 6371000.0;
            int segments = cmd.Segments > 2 ? cmd.Segments : 64;
            double centerLatRad = cmd.CenterLatitude * Math.PI / 180.0;
            double centerLonRad = cmd.CenterLongitude * Math.PI / 180.0;
            double distRatio = cmd.RadiusMeters / R;

            var shell = new List<NetTopologySuite.Geometries.Coordinate>(segments + 1);

            for (int i = 0; i <= segments; i++)
            {
                double angleRad = (i * 360.0 / segments) * Math.PI / 180.0;
                double latRad = Math.Asin(Math.Sin(centerLatRad) * Math.Cos(distRatio) +
                                          Math.Cos(centerLatRad) * Math.Sin(distRatio) * Math.Cos(angleRad));
                double lonRad = centerLonRad + Math.Atan2(Math.Sin(angleRad) * Math.Sin(distRatio) * Math.Cos(centerLatRad),
                                                          Math.Cos(distRatio) - Math.Sin(centerLatRad) * Math.Sin(latRad));

                double latDeg = latRad * 180.0 / Math.PI;
                double lonDeg = lonRad * 180.0 / Math.PI;

                var (x, y) = CoordinateHelper.ToMercator(latDeg, lonDeg);
                shell.Add(new NetTopologySuite.Geometries.Coordinate(x, y));
            }

            var polygon = new Polygon(new LinearRing(shell.ToArray()));
            var feature = new GeometryFeature(polygon) { ["ID"] = featureId };

            var pen = new Mapsui.Styles.Pen(ParseColor(cmd.OutlineColor, "#3B82F6"), cmd.OutlineWidth > 0 ? cmd.OutlineWidth : 2.0);
            if (cmd.DashArray != null && cmd.DashArray.Length > 0)
            {
                pen.DashArray = cmd.DashArray.Select(d => (float)d).ToArray();
            }


            var style = new VectorStyle
            {
                Outline = pen
            };

            if (!string.IsNullOrEmpty(cmd.FillColor))
            {
                style.Fill = new Mapsui.Styles.Brush(ParseColor(cmd.FillColor, "#203B82F6"));
            }

            feature.Styles.Add(style);

            state.Features[featureId] = feature;
            state.UpdateFeatures();
            _mapControl.RefreshGraphics();
            return featureId;
        }


        private static readonly string _iconCacheDir = Path.Combine(Path.GetTempPath(), "Mapsui48_IconCache");
        private static readonly ConcurrentDictionary<string, string> _iconUriCache = new(StringComparer.OrdinalIgnoreCase);

        static MapService()
        {
            try { Directory.CreateDirectory(_iconCacheDir); } catch { }
        }

        private string ResolveIconSource(string iconType, string color, string layerName, string featureId)
        {
            if (!string.IsNullOrEmpty(iconType))
            {
                if (iconType.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
                    iconType.StartsWith("embedded://", StringComparison.OrdinalIgnoreCase) ||
                    iconType.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    iconType.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    return iconType;
                }
            }

            string resolvedKey = MapIconCatalog.ResolveIconKey(iconType, layerName);
            if (string.IsNullOrEmpty(resolvedKey))
                return null;

            string colorHex = color;
            if (string.IsNullOrEmpty(colorHex))
            {
                if (string.Equals(layerName, "Camera", StringComparison.OrdinalIgnoreCase))
                    colorHex = "#00D4FF";
                else if (string.Equals(layerName, "Targets", StringComparison.OrdinalIgnoreCase))
                    colorHex = "#EF4444";
                else
                    colorHex = "#00E5FF";
            }

            string cleanColor = colorHex.Replace("#", "").ToUpperInvariant();
            string cacheKey = $"{resolvedKey}_{cleanColor}";

            return _iconUriCache.GetOrAdd(cacheKey, key =>
            {
                try
                {
                    string svg = MapIconCatalog.GetColorizedSvg(resolvedKey, colorHex, featureId ?? "0");
                    if (string.IsNullOrEmpty(svg)) return null!;

                    string filePath = Path.Combine(_iconCacheDir, $"{cacheKey}.svg");
                    File.WriteAllText(filePath, svg);
                    return new Uri(filePath).AbsoluteUri;
                }
                catch
                {
                    return null!;
                }
            });
        }

        public string AddPoint(AddPointCommand cmd)
        {
            var state = GetOrCreateLayer(cmd.LayerName);
            var featureId = string.IsNullOrEmpty(cmd.FeatureId) ? Guid.NewGuid().ToString("N") : cmd.FeatureId;

            var (x, y) = CoordinateHelper.ToMercator(cmd.Latitude, cmd.Longitude);
            var feature = new PointFeature(new MPoint(x, y)) { ["ID"] = featureId };

            double scale = cmd.Scale > 0 ? cmd.Scale : 1.0;
            string iconSource = ResolveIconSource(cmd.IconType, cmd.Color, cmd.LayerName, featureId);

            if (!string.IsNullOrEmpty(iconSource))
            {
                feature.Styles.Add(new ImageStyle
                {
                    Image = new Mapsui.Styles.Image { Source = iconSource },
                    SymbolScale = scale,
                    SymbolRotation = cmd.Rotation ?? 0,
                    RotateWithMap = true
                });
            }
            else
            {
                var color = ParseColor(cmd.Color, "#FFFF0000");

                // Vector circle symbol rendered cleanly at any scale without white box artifacts
                feature.Styles.Add(new SymbolStyle
                {
                    SymbolType = SymbolType.Ellipse,
                    SymbolScale = scale,
                    SymbolRotation = cmd.Rotation ?? 0,
                    RotateWithMap = true,
                    Fill = new Mapsui.Styles.Brush(color),
                    Outline = new Mapsui.Styles.Pen(Mapsui.Styles.Color.FromArgb(220, 0, 0, 0), Math.Max(1.0, scale * 1.5))
                });
            }

            if (!string.IsNullOrEmpty(cmd.Label))
            {
                double yOffset = !string.IsNullOrEmpty(iconSource) ? (14 * scale + 4) : (-10 * scale - 6);
                feature.Styles.Add(new LabelStyle
                {
                    Text = cmd.Label,
                    BackColor = null, // Transparent background (removes the solid white box!)
                    ForeColor = Mapsui.Styles.Color.White,
                    Halo = new Mapsui.Styles.Pen(Mapsui.Styles.Color.FromArgb(220, 0, 0, 0), 2),
                    Font = new Mapsui.Styles.Font { FontFamily = "Arial", Size = 9.5f, Bold = true },
                    Offset = new Offset(0, yOffset)
                });
            }

            state.Features[featureId] = feature;
            state.UpdateFeatures();
            _mapControl.RefreshGraphics();
            return featureId;
        }

        public string AddLine(AddLineCommand cmd)
        {
            var state = GetOrCreateLayer(cmd.LayerName);
            var featureId = string.IsNullOrEmpty(cmd.FeatureId) ? Guid.NewGuid().ToString("N") : cmd.FeatureId;

            var lineCoords = cmd.Coordinates.Select(c => 
            {
                var (x, y) = CoordinateHelper.ToMercator(c.Latitude, c.Longitude);
                return new NetTopologySuite.Geometries.Coordinate(x, y);
            }).ToArray();

            var lineString = new LineString(lineCoords);
            var feature = new GeometryFeature(lineString) { ["ID"] = featureId };

            var color = ParseColor(cmd.Color, "#FF0000FF");
            feature.Styles.Add(new VectorStyle
            {
                Line = new Mapsui.Styles.Pen(color, cmd.Width > 0 ? cmd.Width : 2)
            });


            state.Features[featureId] = feature;
            state.UpdateFeatures();
            _mapControl.RefreshGraphics();
            return featureId;
        }

        public void RemoveFeature(string layerName, string featureId)
        {
            if (_layers.TryGetValue(layerName, out var state))
            {
                if (state.Features.TryRemove(featureId, out _))
                {
                    state.UpdateFeatures();
                    _mapControl.RefreshGraphics();
                }
            }
        }

        public void ClearLayer(string layerName)
        {
            if (_layers.TryGetValue(layerName, out var state))
            {
                state.ClearFeatures();
                _mapControl.RefreshGraphics();
            }
        }

        private DynamicLayerState GetOrCreateLayer(string layerName)
        {
            return _layers.GetOrAdd(layerName, name =>
            {
                var state = new DynamicLayerState
                {
                    Layer = new MemoryLayer { Name = name, Style = null }
                };
                
                // We're already on the UI thread (CommandDispatcher uses Dispatcher.Invoke),
                // so add the layer synchronously to ensure it's available immediately.
                _mapControl.Map.Layers.Add(state.Layer);
                return state;
            });
        }

        public static DateTime LastRightClickTime { get; set; } = DateTime.MinValue;

        private void Map_Info(object? sender, MapInfoEventArgs e)
        {
            if ((DateTime.UtcNow - LastRightClickTime).TotalMilliseconds < 1500)
            {
                // Suppress Left click if a Right click or drag was just performed
                return;
            }

            var (lat, lon) = CoordinateHelper.ToWgs84(e.WorldPosition.X, e.WorldPosition.Y);
            
            MapInfo? mapInfo = null;
            try
            {
                mapInfo = e.GetMapInfo(e.Map.Layers.Where(l => l is Layer && l.Name != "BaseMap_Offline" && l.Name != "BaseMap_Online" && l.Name != "BaseMap_Land"));
            }
            catch { }

            if (mapInfo?.Feature != null)
            {
                var layerName = mapInfo.Layer?.Name;
                var featureId = mapInfo.Feature["ID"]?.ToString();

                _eventPublisher(new FeatureClickedEvent
                {
                    LayerName = layerName,
                    FeatureId = featureId,
                    Latitude = lat,
                    Longitude = lon
                });
            }
            else
            {
                _eventPublisher(new MapClickedEvent
                {
                    Latitude = lat,
                    Longitude = lon,
                    ScreenX = e.ScreenPosition.X,
                    ScreenY = e.ScreenPosition.Y,
                    Button = "Left"
                });
            }
        }

        private void Navigator_ViewportChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            var now = DateTime.UtcNow;
            // Throttle viewport events during smooth inertia animations to ~20 Hz
            if ((now - _lastViewportEventTime).TotalMilliseconds < 50)
            {
                return;
            }
            _lastViewportEventTime = now;

            var viewport = _mapControl.Map.Navigator.Viewport;
            var (lat, lon) = CoordinateHelper.ToWgs84(viewport.CenterX, viewport.CenterY);
            
            _eventPublisher(new ViewportChangedEvent
            {
                CenterLat = lat,
                CenterLon = lon,
                ZoomLevel = CoordinateHelper.ResolutionToZoomLevel(viewport.Resolution),
                Rotation = viewport.Rotation
            });
        }
        public void RotateTo(double heading, int? durationMs, string? easing)
        {
            _mapControl.Map.Navigator.RotateTo(heading, durationMs ?? 500, ParseEasing(easing));
        }

        public void SetRotationLock(bool locked)
        {
            _mapControl.Map.Navigator.RotationLock = locked;
        }

        public void ZoomToBox(double minLat, double minLon, double maxLat, double maxLon, int? durationMs, string? boxFit)
        {
            var (minX, minY) = CoordinateHelper.ToMercator(minLat, minLon);
            var (maxX, maxY) = CoordinateHelper.ToMercator(maxLat, maxLon);
            var mercatorBox = new MRect(Math.Min(minX, maxX), Math.Min(minY, maxY), Math.Max(minX, maxX), Math.Max(minY, maxY));

            if (durationMs.HasValue && durationMs.Value > 0)
                _mapControl.Map.Navigator.ZoomToBox(mercatorBox, ParseBoxFit(boxFit), durationMs.Value);
            else
                _mapControl.Map.Navigator.ZoomToBox(mercatorBox, ParseBoxFit(boxFit));
        }

        public void SetViewportBounds(double? minLat, double? minLon, double? maxLat, double? maxLon, double? minZoom, double? maxZoom)
        {
            if (minLat.HasValue && minLon.HasValue && maxLat.HasValue && maxLon.HasValue)
            {
                var (minX, minY) = CoordinateHelper.ToMercator(minLat.Value, minLon.Value);
                var (maxX, maxY) = CoordinateHelper.ToMercator(maxLat.Value, maxLon.Value);
                _mapControl.Map.Navigator.OverridePanBounds = new MRect(Math.Min(minX, maxX), Math.Min(minY, maxY), Math.Max(minX, maxX), Math.Max(minY, maxY));
            }
            else
            {
                _mapControl.Map.Navigator.OverridePanBounds = null;
            }

            if (minZoom.HasValue && maxZoom.HasValue)
            {
                var minRes = CoordinateHelper.ZoomLevelToResolution(maxZoom.Value); // Higher zoom = lower resolution
                var maxRes = CoordinateHelper.ZoomLevelToResolution(minZoom.Value); // Lower zoom = higher resolution
                _mapControl.Map.Navigator.OverrideZoomBounds = new MMinMax(Math.Min(minRes, maxRes), Math.Max(minRes, maxRes));
            }
            else
            {
                _mapControl.Map.Navigator.OverrideZoomBounds = null;
            }
        }

        public void SetPanLock(bool locked)
        {
            _mapControl.Map.Navigator.PanLock = locked;
        }

        public void SetZoomLock(bool locked)
        {
            _mapControl.Map.Navigator.ZoomLock = locked;
        }

        // ── Layer Management ─────────────────────────────────────────

        public void SetLayerVisibility(string layerName, bool visible)
        {
            var layer = _mapControl.Map.Layers.FirstOrDefault(l => string.Equals(l.Name, layerName, StringComparison.OrdinalIgnoreCase));
            if (layer != null)
            {
                layer.Enabled = visible;
                _mapControl.RefreshGraphics();
            }
        }

        public void SetLayerOpacity(string layerName, double opacity)
        {
            var layer = _mapControl.Map.Layers.FirstOrDefault(l => string.Equals(l.Name, layerName, StringComparison.OrdinalIgnoreCase));
            if (layer != null)
            {
                layer.Opacity = Math.Clamp(opacity, 0.0, 1.0);
                _mapControl.RefreshGraphics();
            }
        }

        public void SetLayerScaleRange(string layerName, double? minZoom, double? maxZoom)
        {
            var layer = _mapControl.Map.Layers.FirstOrDefault(l => string.Equals(l.Name, layerName, StringComparison.OrdinalIgnoreCase));
            if (layer is Layer l)
            {
                l.MaxVisible = minZoom.HasValue ? CoordinateHelper.ZoomLevelToResolution(minZoom.Value) : double.MaxValue;
                l.MinVisible = maxZoom.HasValue ? CoordinateHelper.ZoomLevelToResolution(maxZoom.Value) : 0;
                _mapControl.RefreshGraphics();
            }
        }

        public void RemoveLayer(string layerName)
        {
            if (_layers.TryRemove(layerName, out var state))
            {
                _mapControl.Map.Layers.Remove(state.Layer);
            }
            else
            {
                var layer = _mapControl.Map.Layers.FirstOrDefault(l => string.Equals(l.Name, layerName, StringComparison.OrdinalIgnoreCase));
                if (layer != null)
                    _mapControl.Map.Layers.Remove(layer);
            }
            _mapControl.RefreshGraphics();
        }

        public List<LayerInfoDto> GetLayers()
        {
            var list = new List<LayerInfoDto>();
            foreach (var l in _mapControl.Map.Layers)
            {
                int count = 0;
                if (_layers.TryGetValue(l.Name ?? "", out var state))
                    count = state.Features.Count;

                list.Add(new LayerInfoDto
                {
                    Name = l.Name ?? "",
                    Enabled = l.Enabled,
                    Opacity = l.Opacity,
                    MinVisible = (l is Layer lyr) ? lyr.MinVisible : 0,
                    MaxVisible = (l is Layer lyr2) ? lyr2.MaxVisible : double.MaxValue,
                    FeatureCount = count
                });
            }
            return list;
        }


        // ── Batch & Advanced Features ────────────────────────────────

        public List<string> AddFeaturesBatch(AddFeaturesBatchCommand cmd)
        {
            var state = GetOrCreateLayer(cmd.LayerName);
            var ids = new List<string>();

            if (cmd.Features == null || cmd.Features.Length == 0)
                return ids;

            foreach (var dto in cmd.Features)
            {
                var featureId = string.IsNullOrEmpty(dto.FeatureId) ? Guid.NewGuid().ToString("N") : dto.FeatureId;
                ids.Add(featureId);

                IFeature feature;

                if (string.Equals(dto.Type, "Polygon", StringComparison.OrdinalIgnoreCase) && dto.Coordinates != null && dto.Coordinates.Length > 2)
                {
                    var shell = dto.Coordinates.Select(c =>
                    {
                        var (x, y) = CoordinateHelper.ToMercator(c.Latitude, c.Longitude);
                        return new NetTopologySuite.Geometries.Coordinate(x, y);
                    }).ToList();

                    if (!shell[0].Equals2D(shell[shell.Count - 1]))
                        shell.Add(new NetTopologySuite.Geometries.Coordinate(shell[0].X, shell[0].Y));

                    var poly = new Polygon(new LinearRing(shell.ToArray()));
                    var gf = new GeometryFeature(poly) { ["ID"] = featureId };

                    var fillColor = ParseColor(dto.FillColor, "#800000FF");
                    var outlineColor = ParseColor(dto.OutlineColor, "#FF0000FF");
                    var pen = new Mapsui.Styles.Pen(outlineColor, dto.OutlineWidth > 0 ? dto.OutlineWidth : 2);
                    if (dto.DashArray != null && dto.DashArray.Length > 0)
                        pen.DashArray = dto.DashArray.Select(d => (float)d).ToArray();

                    gf.Styles.Add(new VectorStyle
                    {
                        Fill = new Mapsui.Styles.Brush(fillColor),
                        Outline = pen
                    });
                    feature = gf;
                }
                else if (string.Equals(dto.Type, "Line", StringComparison.OrdinalIgnoreCase) && dto.Coordinates != null && dto.Coordinates.Length > 1)
                {
                    var lineCoords = dto.Coordinates.Select(c =>
                    {
                        var (x, y) = CoordinateHelper.ToMercator(c.Latitude, c.Longitude);
                        return new NetTopologySuite.Geometries.Coordinate(x, y);
                    }).ToArray();

                    var line = new LineString(lineCoords);
                    var gf = new GeometryFeature(line) { ["ID"] = featureId };

                    var color = ParseColor(dto.Color, "#FF0000FF");
                    var pen = new Mapsui.Styles.Pen(color, dto.OutlineWidth > 0 ? dto.OutlineWidth : 2);
                    if (dto.DashArray != null && dto.DashArray.Length > 0)
                        pen.DashArray = dto.DashArray.Select(d => (float)d).ToArray();

                    gf.Styles.Add(new VectorStyle { Line = pen });
                    feature = gf;
                }
                else
                {
                    // Point Feature
                    double lat = dto.Latitude ?? (dto.Coordinates != null && dto.Coordinates.Length > 0 ? dto.Coordinates[0].Latitude : 0.0);
                    double lon = dto.Longitude ?? (dto.Coordinates != null && dto.Coordinates.Length > 0 ? dto.Coordinates[0].Longitude : 0.0);
                    var (x, y) = CoordinateHelper.ToMercator(lat, lon);
                    var pf = new PointFeature(new MPoint(x, y)) { ["ID"] = featureId };

                    double scale = dto.Scale > 0 ? dto.Scale : 1.0;
                    string iconSource = ResolveIconSource(dto.IconType, dto.Color, cmd.LayerName, featureId);

                    if (!string.IsNullOrEmpty(iconSource))
                    {
                        pf.Styles.Add(new ImageStyle
                        {
                            Image = new Mapsui.Styles.Image { Source = iconSource },
                            SymbolScale = scale,
                            SymbolRotation = dto.Rotation ?? 0,
                            RotateWithMap = true
                        });
                    }
                    else
                    {
                        var color = ParseColor(dto.Color, "#FFFF0000");
                        pf.Styles.Add(new SymbolStyle
                        {
                            SymbolType = SymbolType.Ellipse,
                            SymbolScale = scale,
                            SymbolRotation = dto.Rotation ?? 0,
                            RotateWithMap = true,
                            Fill = new Mapsui.Styles.Brush(color),
                            Outline = new Mapsui.Styles.Pen(Mapsui.Styles.Color.FromArgb(220, 0, 0, 0), Math.Max(1.0, scale * 1.5))
                        });
                    }


                    if (!string.IsNullOrEmpty(dto.Label))
                    {
                        double yOffset = !string.IsNullOrEmpty(iconSource) ? (14 * scale + 4) : (-10 * scale - 6);
                        pf.Styles.Add(new LabelStyle
                        {
                            Text = dto.Label,
                            BackColor = null,
                            ForeColor = Mapsui.Styles.Color.White,
                            Halo = new Mapsui.Styles.Pen(Mapsui.Styles.Color.FromArgb(220, 0, 0, 0), 2),
                            Font = new Mapsui.Styles.Font { FontFamily = "Arial", Size = 9.5f, Bold = true },
                            Offset = new Offset(0, yOffset)
                        });
                    }

                    if (!string.IsNullOrEmpty(dto.CalloutTitle))
                    {
                        pf.Styles.Add(new CalloutStyle
                        {
                            Title = dto.CalloutTitle,
                            Subtitle = dto.CalloutSubtitle ?? "",
                            TitleFont = new Mapsui.Styles.Font { FontFamily = "Segoe UI", Size = 10.5f, Bold = true },
                            SubtitleFont = new Mapsui.Styles.Font { FontFamily = "Segoe UI", Size = 9f },
                            TitleFontColor = Mapsui.Styles.Color.Black,
                            SubtitleFontColor = Mapsui.Styles.Color.Gray,
                            Type = CalloutType.Single,
                            Enabled = true
                        });
                    }

                    feature = pf;
                }

                state.Features[featureId] = feature;
            }

            state.UpdateFeatures();
            _mapControl.RefreshGraphics();
            return ids;
        }

        public void UpdateFeature(UpdateFeatureCommand cmd)
        {
            if (_layers.TryGetValue(cmd.LayerName, out var state))
            {
                if (state.Features.TryGetValue(cmd.FeatureId, out var feature))
                {
                    if (feature is PointFeature pf)
                    {
                        if (cmd.Latitude.HasValue && cmd.Longitude.HasValue)
                        {
                            var (x, y) = CoordinateHelper.ToMercator(cmd.Latitude.Value, cmd.Longitude.Value);
                            pf.Point.X = x;
                            pf.Point.Y = y;
                        }

                        if (cmd.Rotation.HasValue)
                        {
                            foreach (var s in pf.Styles)
                            {
                                if (s is ImageStyle imgStyle) imgStyle.SymbolRotation = cmd.Rotation.Value;
                                else if (s is SymbolStyle symStyle) symStyle.SymbolRotation = cmd.Rotation.Value;
                            }
                        }

                        if (cmd.Scale.HasValue)
                        {
                            foreach (var s in pf.Styles)
                            {
                                if (s is ImageStyle imgStyle) imgStyle.SymbolScale = cmd.Scale.Value;
                                else if (s is SymbolStyle symStyle) symStyle.SymbolScale = cmd.Scale.Value;
                            }
                        }

                        if (!string.IsNullOrEmpty(cmd.Label))
                        {
                            var labelStyle = pf.Styles.OfType<LabelStyle>().FirstOrDefault();
                            if (labelStyle != null)
                                labelStyle.Text = cmd.Label;
                        }

                        state.UpdateFeatures();
                        _mapControl.RefreshGraphics();
                    }
                }
            }
        }

        public void ShowCallout(ShowCalloutCommand cmd)
        {
            if (_layers.TryGetValue(cmd.LayerName, out var state))
            {
                if (state.Features.TryGetValue(cmd.FeatureId, out var feature))
                {
                    var existing = feature.Styles.OfType<CalloutStyle>().FirstOrDefault();
                    if (existing != null)
                    {
                        existing.Enabled = cmd.Enabled;
                        if (!string.IsNullOrEmpty(cmd.Title)) existing.Title = cmd.Title;
                        if (!string.IsNullOrEmpty(cmd.Subtitle)) existing.Subtitle = cmd.Subtitle;
                    }
                    else if (cmd.Enabled)
                    {
                        feature.Styles.Add(new CalloutStyle
                        {
                            Title = cmd.Title ?? "",
                            Subtitle = cmd.Subtitle ?? "",
                            TitleFont = new Mapsui.Styles.Font { FontFamily = "Segoe UI", Size = 10.5f, Bold = true },
                            SubtitleFont = new Mapsui.Styles.Font { FontFamily = "Segoe UI", Size = 9f },
                            TitleFontColor = Mapsui.Styles.Color.Black,
                            SubtitleFontColor = Mapsui.Styles.Color.Gray,
                            Type = CalloutType.Single,
                            Enabled = true
                        });
                    }
                    _mapControl.RefreshGraphics();
                }
            }
        }

        // ── Canvas HUD Widgets ───────────────────────────────────────

        private ScaleBarWidget? _scaleBarWidget;
        private MouseCoordinatesWidget? _mouseWidget;
        private PerformanceWidget? _perfWidget;
        private ZoomInOutWidget? _zoomWidget;
        private RulerWidget? _rulerWidget;

        private void RemoveWidget(Mapsui.Widgets.IWidget? widget)
        {
            if (widget == null) return;
            var remaining = _mapControl.Map.Widgets.Where(w => w != widget).ToList();
            _mapControl.Map.Widgets.Clear();
            foreach (var w in remaining)
                _mapControl.Map.Widgets.Enqueue(w);
        }

        public void SetScaleBarWidget(bool enabled, string position, string mode)
        {
            RemoveWidget(_scaleBarWidget);
            _scaleBarWidget = null;

            if (enabled)
            {
                var (h, v) = ParseAlignment(position);
                var barMode = string.Equals(mode, "Both", StringComparison.OrdinalIgnoreCase) ? ScaleBarMode.Both : ScaleBarMode.Single;

                _scaleBarWidget = new ScaleBarWidget(_mapControl.Map)
                {
                    HorizontalAlignment = h,
                    VerticalAlignment = v,
                    Margin = new MRect(12, 12, 12, 12),
                    ScaleBarMode = barMode
                };
                _mapControl.Map.Widgets.Enqueue(_scaleBarWidget);
            }
            _mapControl.RefreshGraphics();
        }

        public void SetMouseCoordinatesWidget(bool enabled, string position)
        {
            RemoveWidget(_mouseWidget);
            _mouseWidget = null;

            if (enabled)
            {
                var (h, v) = ParseAlignment(position);
                _mouseWidget = new MouseCoordinatesWidget
                {
                    HorizontalAlignment = h,
                    VerticalAlignment = v,
                    Margin = new MRect(10, 10, 10, 10)
                };
                _mapControl.Map.Widgets.Enqueue(_mouseWidget);
            }
            _mapControl.RefreshGraphics();
        }

        public void SetPerformanceWidget(bool enabled, string position)
        {
            RemoveWidget(_perfWidget);
            _perfWidget = null;

            if (enabled)
            {
                var (h, v) = ParseAlignment(position);
                _perfWidget = new PerformanceWidget(_mapControl.Map.Performance)
                {
                    HorizontalAlignment = h,
                    VerticalAlignment = v,
                    Margin = new MRect(10, 10, 10, 10)
                };
                _mapControl.Map.Widgets.Enqueue(_perfWidget);
            }
            _mapControl.RefreshGraphics();
        }

        public void SetZoomButtonsWidget(bool enabled, string position)
        {
            RemoveWidget(_zoomWidget);
            _zoomWidget = null;

            if (enabled)
            {
                var (h, v) = ParseAlignment(position);
                _zoomWidget = new ZoomInOutWidget
                {
                    HorizontalAlignment = h,
                    VerticalAlignment = v,
                    Margin = new MRect(10, 10, 10, 10),
                    Orientation = Mapsui.Widgets.Orientation.Vertical
                };
                _mapControl.Map.Widgets.Enqueue(_zoomWidget);
            }
            _mapControl.RefreshGraphics();
        }

        public void SetRulerWidget(bool enabled)
        {
            RemoveWidget(_rulerWidget);
            _rulerWidget = null;

            if (enabled)
            {
                _rulerWidget = new RulerWidget();
                _mapControl.Map.Widgets.Enqueue(_rulerWidget);
            }
            _mapControl.RefreshGraphics();
        }

        // ── GIS Data Loaders & Formats ────────────────────────────────

        public void LoadGeoJson(LoadGeoJsonCommand cmd)
        {
            string geojson = File.Exists(cmd.GeoJsonOrFilePath)
                ? File.ReadAllText(cmd.GeoJsonOrFilePath)
                : cmd.GeoJsonOrFilePath;

            var provider = new GeoJsonProvider(geojson);
            var fill = ParseColor(cmd.FillColor, "#403B82F6");
            var outline = ParseColor(cmd.OutlineColor, "#3B82F6");

            var layer = new Layer(cmd.LayerName)
            {
                DataSource = provider,
                Style = new VectorStyle
                {
                    Fill = new Mapsui.Styles.Brush(fill),
                    Outline = new Mapsui.Styles.Pen(outline, cmd.OutlineWidth > 0 ? cmd.OutlineWidth : 2)
                }
            };

            var existing = _mapControl.Map.Layers.FirstOrDefault(l => string.Equals(l.Name, cmd.LayerName, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
                _mapControl.Map.Layers.Remove(existing);

            _mapControl.Map.Layers.Add(layer);
            _mapControl.Refresh();
        }

        public void LoadShapefile(LoadShapefileCommand cmd)
        {
            var shapeFile = new ShapeFile(cmd.ShapefilePath, true);
            var fill = ParseColor(cmd.FillColor, "#4010B981");
            var outline = ParseColor(cmd.OutlineColor, "#10B981");

            var layer = new Layer(cmd.LayerName)
            {
                DataSource = shapeFile,
                Style = new VectorStyle
                {
                    Fill = new Mapsui.Styles.Brush(fill),
                    Outline = new Mapsui.Styles.Pen(outline, cmd.OutlineWidth > 0 ? cmd.OutlineWidth : 2)
                }
            };

            var existing = _mapControl.Map.Layers.FirstOrDefault(l => string.Equals(l.Name, cmd.LayerName, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
                _mapControl.Map.Layers.Remove(existing);

            _mapControl.Map.Layers.Add(layer);
            _mapControl.Refresh();
        }

        public void AddWmsLayer(AddWmsLayerCommand cmd)
        {
            var wmsProvider = Mapsui.Providers.Wms.WmsProvider.CreateAsync(cmd.Url).GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(cmd.ServiceLayerName))
                wmsProvider.AddLayer(cmd.ServiceLayerName);
            if (!string.IsNullOrEmpty(cmd.Crs))
                wmsProvider.CRS = cmd.Crs;
            wmsProvider.Transparent = true;

            var layer = new Layer(cmd.LayerName)
            {
                DataSource = wmsProvider
            };

            var existing = _mapControl.Map.Layers.FirstOrDefault(l => string.Equals(l.Name, cmd.LayerName, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
                _mapControl.Map.Layers.Remove(existing);

            _mapControl.Map.Layers.Add(layer);
            _mapControl.Refresh();
        }

        // ── Coordinate Translation & Spatial Queries ─────────────────

        public CoordinateResultDto ScreenToWorld(double screenX, double screenY)
        {
            var world = _mapControl.Map.Navigator.Viewport.ScreenToWorld(new Mapsui.Manipulations.ScreenPosition(screenX, screenY));
            var (lat, lon) = CoordinateHelper.ToWgs84(world.X, world.Y);
            double outLat = double.IsNaN(lat) || double.IsInfinity(lat) ? 0 : lat;
            double outLon = double.IsNaN(lon) || double.IsInfinity(lon) ? 0 : lon;
            return new CoordinateResultDto
            {
                Latitude = outLat,
                Longitude = outLon,
                ScreenX = screenX,
                ScreenY = screenY
            };
        }

        public CoordinateResultDto WorldToScreen(double lat, double lon)
        {
            var (x, y) = CoordinateHelper.ToMercator(lat, lon);
            var screen = _mapControl.Map.Navigator.Viewport.WorldToScreen(x, y);
            double sx = double.IsNaN(screen.X) || double.IsInfinity(screen.X) ? 0 : screen.X;
            double sy = double.IsNaN(screen.Y) || double.IsInfinity(screen.Y) ? 0 : screen.Y;
            return new CoordinateResultDto
            {
                Latitude = lat,
                Longitude = lon,
                ScreenX = sx,
                ScreenY = sy
            };
        }



        public BoundsResultDto? GetLayerBounds(string layerName)
        {
            var layer = _mapControl.Map.Layers.FirstOrDefault(l => string.Equals(l.Name, layerName, StringComparison.OrdinalIgnoreCase));
            if (layer == null) return null;

            var extent = layer.Extent;
            if (extent == null) return null;

            var (lat1, lon1) = CoordinateHelper.ToWgs84(extent.Min.X, extent.Min.Y);
            var (lat2, lon2) = CoordinateHelper.ToWgs84(extent.Max.X, extent.Max.Y);

            return new BoundsResultDto
            {
                MinLat = Math.Min(lat1, lat2),
                MinLon = Math.Min(lon1, lon2),
                MaxLat = Math.Max(lat1, lat2),
                MaxLon = Math.Max(lon1, lon2)
            };
        }

        // ── Animated Glide Tracking ──────────────────────────────────

        public string AddAnimatedPoint(AddAnimatedPointCommand cmd)
        {
            var featureId = string.IsNullOrEmpty(cmd.FeatureId) ? Guid.NewGuid().ToString("N") : cmd.FeatureId;
            var state = _animatedLayers.GetOrAdd(cmd.LayerName, name => new AnimatedLayerState());

            var (x, y) = CoordinateHelper.ToMercator(cmd.Latitude, cmd.Longitude);
            int duration = cmd.DurationMs > 0 ? cmd.DurationMs : 1000;
            var feature = new AnimatedPointFeature(x, y, duration, Mapsui.Animations.Easing.CubicOut, 0)
            {
                ["ID"] = featureId
            };

            double scale = cmd.Scale > 0 ? cmd.Scale : 1.0;
            string iconSource = ResolveIconSource(cmd.IconType, cmd.Color, cmd.LayerName, featureId);

            if (!string.IsNullOrEmpty(iconSource))
            {
                feature.Styles.Add(new ImageStyle
                {
                    Image = new Mapsui.Styles.Image { Source = iconSource },
                    SymbolScale = scale,
                    SymbolRotation = cmd.Rotation ?? 0,
                    RotateWithMap = true
                });
            }
            else
            {
                var color = ParseColor(cmd.Color, "#FFFF0000");
                feature.Styles.Add(new SymbolStyle
                {
                    SymbolType = SymbolType.Ellipse,
                    SymbolScale = scale,
                    SymbolRotation = cmd.Rotation ?? 0,
                    RotateWithMap = true,
                    Fill = new Mapsui.Styles.Brush(color),
                    Outline = new Mapsui.Styles.Pen(Mapsui.Styles.Color.FromArgb(220, 0, 0, 0), Math.Max(1.0, scale * 1.5))
                });
            }

            if (!string.IsNullOrEmpty(cmd.Label))
            {
                double yOffset = !string.IsNullOrEmpty(iconSource) ? (14 * scale + 4) : (-10 * scale - 6);
                feature.Styles.Add(new LabelStyle
                {
                    Text = cmd.Label,
                    BackColor = null,
                    ForeColor = Mapsui.Styles.Color.White,
                    Halo = new Mapsui.Styles.Pen(Mapsui.Styles.Color.FromArgb(220, 0, 0, 0), 2),
                    Font = new Mapsui.Styles.Font { FontFamily = "Arial", Size = 9.5f, Bold = true },
                    Offset = new Offset(0, yOffset)
                });
            }

            state.Features[featureId] = feature;

            if (state.Layer != null)
                _mapControl.Map.Layers.Remove(state.Layer);

            state.Layer = new AnimatedPointLayer(new MemoryProvider(state.Features.Values))
            {
                Name = cmd.LayerName
            };
            _mapControl.Map.Layers.Add(state.Layer);
            _mapControl.Refresh();
            return featureId;
        }


        public void UpdateAnimatedPoint(UpdateAnimatedPointCommand cmd)
        {
            if (!_animatedLayers.TryGetValue(cmd.LayerName, out var state))
                return;

            if (!state.Features.TryGetValue(cmd.FeatureId, out var feature))
                return;

            var (x, y) = CoordinateHelper.ToMercator(cmd.Latitude, cmd.Longitude);
            feature.SetAnimationTarget(new MPoint(x, y));

            if (cmd.DurationMs > 0)
                feature.UpdateAnimation(cmd.DurationMs, Mapsui.Animations.Easing.CubicOut, 0);

            if (cmd.Rotation.HasValue)
            {
                foreach (var style in feature.Styles)
                {
                    if (style is ImageStyle isty) isty.SymbolRotation = cmd.Rotation.Value;
                    if (style is SymbolStyle ssty) ssty.SymbolRotation = cmd.Rotation.Value;
                }
            }

            if (cmd.Scale.HasValue && cmd.Scale.Value > 0)
            {
                foreach (var style in feature.Styles)
                {
                    if (style is ImageStyle isty) isty.SymbolScale = cmd.Scale.Value;
                    if (style is SymbolStyle ssty) ssty.SymbolScale = cmd.Scale.Value;
                }
            }

            if (!string.IsNullOrEmpty(cmd.Label))
            {
                var lbl = feature.Styles.OfType<LabelStyle>().FirstOrDefault();
                if (lbl != null) lbl.Text = cmd.Label;
            }

            _mapControl.Refresh();
        }

        public void SetPointerMoveEvents(bool enabled)
        {
            _pointerMoveEventsEnabled = enabled;
        }

        // ── Snapshot & Utilities ─────────────────────────────────────

        public string GetSnapshot(string format, int quality)
        {
            try
            {
                var renderFormat = (string.Equals(format, "jpeg", StringComparison.OrdinalIgnoreCase) || string.Equals(format, "jpg", StringComparison.OrdinalIgnoreCase))
                    ? RenderFormat.Jpeg
                    : RenderFormat.Png;

                var bytes = _mapControl.GetSnapshot(_mapControl.Map.Layers, renderFormat, Math.Clamp(quality, 10, 100));
                if (bytes == null || bytes.Length == 0)
                    return "";

                return Convert.ToBase64String(bytes);
            }
            catch
            {
                return "";
            }
        }


        public static Mapsui.Styles.Color ParseColor(string? colorHex, string fallbackHex = "#FF0000FF")
        {
            if (string.IsNullOrWhiteSpace(colorHex))
                colorHex = fallbackHex;

            string hex = colorHex.Trim().TrimStart('#');
            if (hex.Length == 8)
            {
                // ARGB format: #AARRGGBB
                if (byte.TryParse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out var a) &&
                    byte.TryParse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out var r) &&
                    byte.TryParse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out var g) &&
                    byte.TryParse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
                {
                    return Mapsui.Styles.Color.FromArgb(a, r, g, b);
                }
            }
            else if (hex.Length == 6)
            {
                if (byte.TryParse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out var r) &&
                    byte.TryParse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out var g) &&
                    byte.TryParse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
                {
                    return Mapsui.Styles.Color.FromArgb(255, r, g, b);
                }
            }
            else if (hex.Length == 3)
            {
                try { return Mapsui.Styles.Color.FromString("#" + hex); } catch { }
            }

            try
            {
                return Mapsui.Styles.Color.FromString(colorHex);
            }
            catch
            {
                return Mapsui.Styles.Color.Red;
            }
        }

        private static Mapsui.Animations.Easing ParseEasing(string? easing)
        {
            if (string.IsNullOrEmpty(easing)) return Mapsui.Animations.Easing.CubicOut;
            return easing.ToLowerInvariant() switch
            {
                "linear" => Mapsui.Animations.Easing.Linear,
                "cubicin" => Mapsui.Animations.Easing.CubicIn,
                "cubicout" => Mapsui.Animations.Easing.CubicOut,
                "cubicinout" => Mapsui.Animations.Easing.CubicInOut,
                "sinin" => Mapsui.Animations.Easing.SinIn,
                "sinout" => Mapsui.Animations.Easing.SinOut,
                "bounceout" => Mapsui.Animations.Easing.BounceOut,
                _ => Mapsui.Animations.Easing.CubicOut
            };
        }

        private static (Mapsui.Widgets.HorizontalAlignment H, Mapsui.Widgets.VerticalAlignment V) ParseAlignment(string? position)
        {
            if (string.IsNullOrEmpty(position)) return (Mapsui.Widgets.HorizontalAlignment.Left, Mapsui.Widgets.VerticalAlignment.Bottom);
            return position.ToLowerInvariant() switch
            {
                "topleft" => (Mapsui.Widgets.HorizontalAlignment.Left, Mapsui.Widgets.VerticalAlignment.Top),
                "topright" => (Mapsui.Widgets.HorizontalAlignment.Right, Mapsui.Widgets.VerticalAlignment.Top),
                "bottomright" => (Mapsui.Widgets.HorizontalAlignment.Right, Mapsui.Widgets.VerticalAlignment.Bottom),
                "bottomleft" => (Mapsui.Widgets.HorizontalAlignment.Left, Mapsui.Widgets.VerticalAlignment.Bottom),
                _ => (Mapsui.Widgets.HorizontalAlignment.Left, Mapsui.Widgets.VerticalAlignment.Bottom)
            };
        }

        private static Mapsui.MBoxFit ParseBoxFit(string? fit)
        {
            if (string.Equals(fit, "Fill", StringComparison.OrdinalIgnoreCase)) return Mapsui.MBoxFit.Fill;
            return Mapsui.MBoxFit.Fit;
        }
    }
}


