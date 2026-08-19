using System;

namespace Mapsui48.Protocol
{
    public class MapCommand
    {
        public string Id { get; set; }
        public string Type { get; set; }

        public MapCommand() { }

        public MapCommand(string type)
        {
            Id = Guid.NewGuid().ToString("N");
            Type = type;
        }
    }

    public class AddPolygonCommand : MapCommand
    {
        public string LayerName { get; set; }
        public string FeatureId { get; set; }
        public Coordinate[] Coordinates { get; set; }
        public string FillColor { get; set; }
        public string OutlineColor { get; set; }
        public double OutlineWidth { get; set; }

        public AddPolygonCommand() : base("AddPolygon") { }
    }

    public class AddPointCommand : MapCommand
    {
        public string LayerName { get; set; }
        public string FeatureId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Label { get; set; }
        public string Color { get; set; }
        public double Scale { get; set; }
        public double? Rotation { get; set; }
        public string IconType { get; set; }

        public AddPointCommand() : base("AddPoint") { }
    }

    public class AddLineCommand : MapCommand
    {
        public string LayerName { get; set; }
        public string FeatureId { get; set; }
        public Coordinate[] Coordinates { get; set; }
        public string Color { get; set; }
        public double Width { get; set; }

        public AddLineCommand() : base("AddLine") { }
    }

    public class RemoveFeatureCommand : MapCommand
    {
        public string LayerName { get; set; }
        public string FeatureId { get; set; }

        public RemoveFeatureCommand() : base("RemoveFeature") { }
    }

    public class ClearLayerCommand : MapCommand
    {
        public string LayerName { get; set; }

        public ClearLayerCommand() : base("ClearLayer") { }
    }

    public class NavigateToCommand : MapCommand
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double? ZoomLevel { get; set; }
        public int? DurationMs { get; set; }

        public NavigateToCommand() : base("NavigateTo") { }
    }

    public class FlyToCommand : MapCommand
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double? ZoomLevel { get; set; }
        public int? DurationMs { get; set; }

        public FlyToCommand() : base("FlyTo") { }
    }

    public class SetZoomCommand : MapCommand
    {
        public double ZoomLevel { get; set; }
        public int? DurationMs { get; set; }

        public SetZoomCommand() : base("SetZoom") { }
    }

    public class GoHomeCommand : MapCommand
    {
        public int? DurationMs { get; set; }

        public GoHomeCommand() : base("GoHome") { }
    }

    public class SetTileSourceCommand : MapCommand
    {
        public string MBTilesPath { get; set; }
        public string OnlineUrl { get; set; }
        public string CachePath { get; set; }

        public SetTileSourceCommand() : base("SetTileSource") { }
    }

    public class AttachToCommand : MapCommand
    {
        public long ParentHwnd { get; set; }

        public AttachToCommand() : base("AttachTo") { }
    }

    public class PingCommand : MapCommand
    {
        public PingCommand() : base("Ping") { }
    }

    public class LoadVectorTileCommand : MapCommand
    {
        public string MBTilesPath { get; set; }
        public LoadVectorTileCommand() : base("LoadVectorTile") { }
    }

    public class BeginAreaSelectionCommand : MapCommand
    {
        public BeginAreaSelectionCommand() : base("BeginAreaSelection") { }
    }

    // ── Navigation & Camera Controls ─────────────────────────────

    public class RotateToCommand : MapCommand
    {
        public double Heading { get; set; }
        public int? DurationMs { get; set; }
        public string Easing { get; set; }

        public RotateToCommand() : base("RotateTo") { }
    }

    public class SetRotationLockCommand : MapCommand
    {
        public bool Locked { get; set; }

        public SetRotationLockCommand() : base("SetRotationLock") { }
    }

    public class ZoomToBoxCommand : MapCommand
    {
        public double MinLat { get; set; }
        public double MinLon { get; set; }
        public double MaxLat { get; set; }
        public double MaxLon { get; set; }
        public int? DurationMs { get; set; }
        public string BoxFit { get; set; } = "Fit";

        public ZoomToBoxCommand() : base("ZoomToBox") { }
    }

    public class SetViewportBoundsCommand : MapCommand
    {
        public double? MinLat { get; set; }
        public double? MinLon { get; set; }
        public double? MaxLat { get; set; }
        public double? MaxLon { get; set; }
        public double? MinZoom { get; set; }
        public double? MaxZoom { get; set; }

        public SetViewportBoundsCommand() : base("SetViewportBounds") { }
    }

    public class SetPanLockCommand : MapCommand
    {
        public bool Locked { get; set; }

        public SetPanLockCommand() : base("SetPanLock") { }
    }

    public class SetZoomLockCommand : MapCommand
    {
        public bool Locked { get; set; }

        public SetZoomLockCommand() : base("SetZoomLock") { }
    }

    // ── Layer Management Controls ────────────────────────────────

    public class SetLayerVisibilityCommand : MapCommand
    {
        public string LayerName { get; set; }
        public bool Visible { get; set; }

        public SetLayerVisibilityCommand() : base("SetLayerVisibility") { }
    }

    public class SetLayerOpacityCommand : MapCommand
    {
        public string LayerName { get; set; }
        public double Opacity { get; set; }

        public SetLayerOpacityCommand() : base("SetLayerOpacity") { }
    }

    public class SetLayerScaleRangeCommand : MapCommand
    {
        public string LayerName { get; set; }
        public double? MinZoom { get; set; }
        public double? MaxZoom { get; set; }

        public SetLayerScaleRangeCommand() : base("SetLayerScaleRange") { }
    }

    public class RemoveLayerCommand : MapCommand
    {
        public string LayerName { get; set; }

        public RemoveLayerCommand() : base("RemoveLayer") { }
    }

    public class GetLayersCommand : MapCommand
    {
        public GetLayersCommand() : base("GetLayers") { }
    }

    // ── Batch & Advanced Feature Controls ────────────────────────

    public class FeatureDto
    {
        public string FeatureId { get; set; }
        public string Type { get; set; } = "Point"; // Point, Line, Polygon
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public Coordinate[] Coordinates { get; set; }
        public string Label { get; set; }
        public string Color { get; set; }
        public string FillColor { get; set; }
        public string OutlineColor { get; set; }
        public double OutlineWidth { get; set; } = 2.0;
        public double Scale { get; set; } = 1.0;
        public double? Rotation { get; set; }
        public string IconType { get; set; }
        public double[] DashArray { get; set; }
        public string CalloutTitle { get; set; }
        public string CalloutSubtitle { get; set; }
    }

    public class AddFeaturesBatchCommand : MapCommand
    {
        public string LayerName { get; set; }
        public FeatureDto[] Features { get; set; }

        public AddFeaturesBatchCommand() : base("AddFeaturesBatch") { }
    }

    public class UpdateFeatureCommand : MapCommand
    {
        public string LayerName { get; set; }
        public string FeatureId { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? Rotation { get; set; }
        public double? Scale { get; set; }
        public string Label { get; set; }

        public UpdateFeatureCommand() : base("UpdateFeature") { }
    }

    public class ShowCalloutCommand : MapCommand
    {
        public string LayerName { get; set; }
        public string FeatureId { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public bool Enabled { get; set; } = true;

        public ShowCalloutCommand() : base("ShowCallout") { }
    }

    // ── Canvas HUD Widgets ───────────────────────────────────────

    public class SetScaleBarWidgetCommand : MapCommand
    {
        public bool Enabled { get; set; }
        public string Position { get; set; } = "BottomLeft";
        public string Mode { get; set; } = "Single"; // Single, Both

        public SetScaleBarWidgetCommand() : base("SetScaleBarWidget") { }
    }

    public class SetMouseCoordinatesWidgetCommand : MapCommand
    {
        public bool Enabled { get; set; }
        public string Position { get; set; } = "BottomRight";

        public SetMouseCoordinatesWidgetCommand() : base("SetMouseCoordinatesWidget") { }
    }

    public class SetPerformanceWidgetCommand : MapCommand
    {
        public bool Enabled { get; set; }
        public string Position { get; set; } = "TopRight";

        public SetPerformanceWidgetCommand() : base("SetPerformanceWidget") { }
    }

    public class SetZoomButtonsWidgetCommand : MapCommand
    {
        public bool Enabled { get; set; }
        public string Position { get; set; } = "TopLeft";

        public SetZoomButtonsWidgetCommand() : base("SetZoomButtonsWidget") { }
    }

    // ── Snapshot & Utilities ─────────────────────────────────────

    public class GetSnapshotCommand : MapCommand
    {
        public string Format { get; set; } = "Png"; // Png, Jpeg
        public int Quality { get; set; } = 100;

        public GetSnapshotCommand() : base("GetSnapshot") { }
    }

    // ── GIS Data Loaders & Formats ────────────────────────────────

    public class LoadGeoJsonCommand : MapCommand
    {
        public string GeoJsonOrFilePath { get; set; }
        public string LayerName { get; set; } = "GeoJsonLayer";
        public string FillColor { get; set; } = "#403B82F6";
        public string OutlineColor { get; set; } = "#3B82F6";
        public double OutlineWidth { get; set; } = 2.0;

        public LoadGeoJsonCommand() : base("LoadGeoJson") { }
    }

    public class LoadShapefileCommand : MapCommand
    {
        public string ShapefilePath { get; set; }
        public string LayerName { get; set; } = "ShapefileLayer";
        public string FillColor { get; set; } = "#4010B981";
        public string OutlineColor { get; set; } = "#10B981";
        public double OutlineWidth { get; set; } = 2.0;

        public LoadShapefileCommand() : base("LoadShapefile") { }
    }

    public class AddWmsLayerCommand : MapCommand
    {
        public string Url { get; set; }
        public string LayerName { get; set; } = "WmsLayer";
        public string ServiceLayerName { get; set; }
        public string Crs { get; set; } = "EPSG:3857";

        public AddWmsLayerCommand() : base("AddWmsLayer") { }
    }

    // ── Coordinate Translation & Spatial Queries ─────────────────

    public class ScreenToWorldCommand : MapCommand
    {
        public double ScreenX { get; set; }
        public double ScreenY { get; set; }

        public ScreenToWorldCommand() : base("ScreenToWorld") { }
    }

    public class WorldToScreenCommand : MapCommand
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        public WorldToScreenCommand() : base("WorldToScreen") { }
    }

    public class GetLayerBoundsCommand : MapCommand
    {
        public string LayerName { get; set; }

        public GetLayerBoundsCommand() : base("GetLayerBounds") { }
    }

    // ── Measurement & Ruler Widget ───────────────────────────────

    public class SetRulerWidgetCommand : MapCommand
    {
        public bool Enabled { get; set; }

        public SetRulerWidgetCommand() : base("SetRulerWidget") { }
    }

    // ── Animated Glide Tracking ──────────────────────────────────

    public class AddAnimatedPointCommand : MapCommand
    {
        public string LayerName { get; set; } = "AnimatedTracks";
        public string FeatureId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int DurationMs { get; set; } = 1000;
        public string Label { get; set; }
        public string Color { get; set; }
        public double Scale { get; set; } = 1.0;
        public double? Rotation { get; set; }
        public string IconType { get; set; }

        public AddAnimatedPointCommand() : base("AddAnimatedPoint") { }
    }

    public class UpdateAnimatedPointCommand : MapCommand
    {
        public string LayerName { get; set; } = "AnimatedTracks";
        public string FeatureId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int DurationMs { get; set; } = 1000;
        public double? Rotation { get; set; }
        public double? Scale { get; set; }
        public string Label { get; set; }

        public UpdateAnimatedPointCommand() : base("UpdateAnimatedPoint") { }
    }

    // ── Mouse & Pointer Event Controls ───────────────────────────

    public class SetPointerMoveEventsCommand : MapCommand
    {
        public bool Enabled { get; set; }

        public SetPointerMoveEventsCommand() : base("SetPointerMoveEvents") { }
    }

    // ── Circular & Range Ring Geometries ─────────────────────────

    public class AddCircleCommand : MapCommand
    {
        public string LayerName { get; set; }
        public string FeatureId { get; set; }
        public double CenterLatitude { get; set; }
        public double CenterLongitude { get; set; }
        public double RadiusMeters { get; set; }
        public string FillColor { get; set; }
        public string OutlineColor { get; set; } = "#3B82F6";
        public double OutlineWidth { get; set; } = 2.0;
        public double[] DashArray { get; set; }
        public int Segments { get; set; } = 64;

        public AddCircleCommand() : base("AddCircle") { }
    }
}



