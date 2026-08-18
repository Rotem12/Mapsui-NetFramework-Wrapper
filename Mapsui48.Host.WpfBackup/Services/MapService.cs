using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Providers;
using Mapsui.Styles;
using Mapsui.UI.Wpf;
using Mapsui48.Host.Helpers;
using Mapsui48.Protocol;
using NetTopologySuite.Geometries;
using System;
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
            public Layer Layer { get; set; }
            public ConcurrentDictionary<string, IFeature> Features { get; set; } = new();
        }
        private readonly ConcurrentDictionary<string, DynamicLayerState> _layers = new();

        public MapService(MapControl mapControl, Action<MapEvent> eventPublisher)
        {
            _mapControl = mapControl;
            _eventPublisher = eventPublisher;

            // Set global map background to the OSM Carto sea color for offline fallback
            _mapControl.Map.BackColor = Mapsui.Styles.Color.FromString("#AAD3DF");

            // Hook up events
            _mapControl.Map.Info += Map_Info;
            // Note: Viewport changes will be polled or hooked depending on how Mapsui exposes it. 
            // In Mapsui 5, we can listen to Navigator.ViewportChanged
            _mapControl.Map.Navigator.ViewportChanged += Navigator_ViewportChanged;
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
            var extent = offlineLayer?.Extent ?? _mapControl.Map.Extent;

            if (extent != null)
            {
                if (durationMs.HasValue && durationMs.Value > 0)
                    _mapControl.Map.Navigator.ZoomToBox(extent, Mapsui.MBoxFit.Fit, durationMs.Value);
                else
                    _mapControl.Map.Navigator.ZoomToBox(extent, Mapsui.MBoxFit.Fit);
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
            
            var fillColor = Mapsui.Styles.Color.FromString(cmd.FillColor ?? "#800000FF");
            var outlineColor = Mapsui.Styles.Color.FromString(cmd.OutlineColor ?? "#FF0000FF");
            
            feature.Styles.Add(new VectorStyle
            {
                Fill = new Brush(fillColor),
                Outline = new Pen(outlineColor, cmd.OutlineWidth > 0 ? cmd.OutlineWidth : 2)
            });

            state.Features[featureId] = feature;
            state.Layer.DataSource = new MemoryProvider(state.Features.Values);
            _mapControl.RefreshGraphics();
            return featureId;
        }

        public string AddPoint(AddPointCommand cmd)
        {
            var state = GetOrCreateLayer(cmd.LayerName);
            var featureId = string.IsNullOrEmpty(cmd.FeatureId) ? Guid.NewGuid().ToString("N") : cmd.FeatureId;

            var (x, y) = CoordinateHelper.ToMercator(cmd.Latitude, cmd.Longitude);
            var feature = new PointFeature(new MPoint(x, y)) { ["ID"] = featureId };

            if (!string.IsNullOrEmpty(cmd.Label))
            {
                feature.Styles.Add(new LabelStyle
                {
                    Text = cmd.Label,
                    BackColor = new Brush(Mapsui.Styles.Color.White),
                    ForeColor = Mapsui.Styles.Color.Black,
                    Halo = new Pen(Mapsui.Styles.Color.White, 2),
                    Offset = new Offset(0, -16)
                });
            }

            var color = Mapsui.Styles.Color.FromString(cmd.Color ?? "#FFFF0000");
            feature.Styles.Add(new SymbolStyle
            {
                SymbolScale = cmd.Scale > 0 ? cmd.Scale : 1.0,
                Fill = new Brush(color),
                Outline = new Pen(Mapsui.Styles.Color.Black, 1)
            });

            state.Features[featureId] = feature;
            state.Layer.DataSource = new MemoryProvider(state.Features.Values);
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

            var color = Mapsui.Styles.Color.FromString(cmd.Color ?? "#FF0000FF");
            feature.Styles.Add(new VectorStyle
            {
                Line = new Pen(color, cmd.Width > 0 ? cmd.Width : 2)
            });

            state.Features[featureId] = feature;
            state.Layer.DataSource = new MemoryProvider(state.Features.Values);
            _mapControl.RefreshGraphics();
            return featureId;
        }

        public void RemoveFeature(string layerName, string featureId)
        {
            if (_layers.TryGetValue(layerName, out var state))
            {
                if (state.Features.TryRemove(featureId, out _))
                {
                    state.Layer.DataSource = new MemoryProvider(state.Features.Values);
                    _mapControl.RefreshGraphics();
                }
            }
        }

        public void ClearLayer(string layerName)
        {
            if (_layers.TryGetValue(layerName, out var state))
            {
                state.Features.Clear();
                state.Layer.DataSource = new MemoryProvider();
                _mapControl.RefreshGraphics();
            }
        }

        private DynamicLayerState GetOrCreateLayer(string layerName)
        {
            return _layers.GetOrAdd(layerName, name =>
            {
                var state = new DynamicLayerState
                {
                    Layer = new Layer { Name = name, DataSource = new MemoryProvider() }
                };
                
                // We're already on the UI thread (CommandDispatcher uses Dispatcher.Invoke),
                // so add the layer synchronously to ensure it's available immediately.
                _mapControl.Map.Layers.Add(state.Layer);
                return state;
            });
        }

        private void Map_Info(object sender, MapInfoEventArgs e)
        {
            var (lat, lon) = CoordinateHelper.ToWgs84(e.WorldPosition.X, e.WorldPosition.Y);
            
            var mapInfo = e.GetMapInfo(e.Map.Layers.Where(l => l is Layer && l.Name != "BaseMap_Offline" && l.Name != "BaseMap_Online" && l.Name != "BaseMap_Land"));

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
                    ScreenY = e.ScreenPosition.Y
                });
            }
        }

        private void Navigator_ViewportChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
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
    }
}
